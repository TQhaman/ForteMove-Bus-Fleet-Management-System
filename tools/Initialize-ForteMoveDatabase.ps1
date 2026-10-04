[CmdletBinding()]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$ServerInstance = '.\SQLEXPRESS',

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$DatabaseName = 'ForteMove',

    [Parameter()]
    [switch]$SkipAdministratorSeed
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$passwordAlgorithm = 'PBKDF2-HMAC-SHA256'
$passwordIterations = 600000
$derivedKeyLength = 32
$saltLength = 32
$bootstrapMigrationName = '0000_SchemaMigrations.sql'
$migrationLockResource = 'ForteMove.DatabaseInitialization'

function New-ForteMoveConnectionString {
    param(
        [Parameter(Mandatory = $true)]
        [string]$InitialCatalog
    )

    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $builder['Data Source'] = $ServerInstance
    $builder['Initial Catalog'] = $InitialCatalog
    $builder['Integrated Security'] = $true
    $builder['Encrypt'] = $true
    $builder['TrustServerCertificate'] = $true
    $builder['Application Name'] = 'ForteMove Database Initializer'
    $builder['Connect Timeout'] = 15
    return $builder.ConnectionString
}

function Quote-SqlIdentifier {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    return '[' + $Value.Replace(']', ']]') + ']'
}

function Get-CanonicalMigrationContent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $content = [System.IO.File]::ReadAllText($Path)
    return $content.Replace("`r`n", "`n").Replace("`r", "`n")
}

function Get-Sha256Hex {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Content
    )

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Content)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $digest = $sha256.ComputeHash($bytes)
        return [System.BitConverter]::ToString($digest).Replace('-', '').ToLowerInvariant()
    }
    finally {
        if ($null -ne $bytes) {
            [System.Array]::Clear($bytes, 0, $bytes.Length)
        }

        if ($null -ne $sha256) {
            $sha256.Dispose()
        }
    }
}

function Get-LocalMigrations {
    param(
        [Parameter(Mandatory = $true)]
        [string]$MigrationDirectory
    )

    if (-not (Test-Path -LiteralPath $MigrationDirectory -PathType Container)) {
        throw "Migration directory was not found: $MigrationDirectory"
    }

    $files = @(Get-ChildItem -LiteralPath $MigrationDirectory -Filter '*.sql' -File | Sort-Object Name)
    if ($files.Count -eq 0) {
        throw "No migration files were found in $MigrationDirectory."
    }

    if ($files[0].Name -cne $bootstrapMigrationName) {
        throw "The first migration must be $bootstrapMigrationName."
    }

    $seenNames = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    $migrations = New-Object System.Collections.Generic.List[object]

    foreach ($file in $files) {
        if ($file.Name -notmatch '^\d{4}_[A-Za-z0-9][A-Za-z0-9_-]*\.sql$') {
            throw "Migration file '$($file.Name)' does not use the required NNNN_Name.sql format."
        }

        if (-not $seenNames.Add($file.Name)) {
            throw "Duplicate migration filename '$($file.Name)' was found."
        }

        $content = Get-CanonicalMigrationContent -Path $file.FullName
        if ([string]::IsNullOrWhiteSpace($content)) {
            throw "Migration file '$($file.Name)' is empty."
        }

        if ($content -match '(?im)^\s*GO\s*(?:--.*)?$') {
            throw "Migration file '$($file.Name)' contains GO, which SqlCommand cannot execute."
        }

        $migrations.Add([pscustomobject]@{
            Name = $file.Name
            FullName = $file.FullName
            Content = $content
            Checksum = Get-Sha256Hex -Content $content
        })
    }

    return $migrations.ToArray()
}

function New-OpenSqlConnection {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ConnectionString
    )

    $connection = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
    try {
        $connection.Open()
        return $connection
    }
    catch {
        $connection.Dispose()
        throw
    }
}

function Enter-MigrationLock {
    param(
        [Parameter(Mandatory = $true)]
        [System.Data.SqlClient.SqlConnection]$Connection
    )

    $command = $Connection.CreateCommand()
    try {
        $command.CommandText = @'
DECLARE @Result INT;
EXEC @Result = sys.sp_getapplock
    @Resource = @Resource,
    @LockMode = N'Exclusive',
    @LockOwner = N'Session',
    @LockTimeout = 15000;
SELECT @Result;
'@
        [void]$command.Parameters.Add('@Resource', [System.Data.SqlDbType]::NVarChar, 255)
        $command.Parameters['@Resource'].Value = $migrationLockResource
        $result = [int]$command.ExecuteScalar()
        if ($result -lt 0) {
            throw "Could not acquire the ForteMove migration lock (result $result)."
        }
    }
    finally {
        $command.Dispose()
    }
}

function Exit-MigrationLock {
    param(
        [Parameter(Mandatory = $true)]
        [System.Data.SqlClient.SqlConnection]$Connection
    )

    if ($Connection.State -ne [System.Data.ConnectionState]::Open) {
        return
    }

    $command = $Connection.CreateCommand()
    try {
        $command.CommandText = @'
DECLARE @Result INT;
EXEC @Result = sys.sp_releaseapplock
    @Resource = @Resource,
    @LockOwner = N'Session';
SELECT @Result;
'@
        [void]$command.Parameters.Add('@Resource', [System.Data.SqlDbType]::NVarChar, 255)
        $command.Parameters['@Resource'].Value = $migrationLockResource
        [void]$command.ExecuteScalar()
    }
    finally {
        $command.Dispose()
    }
}

function Test-SchemaMigrationsTable {
    param(
        [Parameter(Mandatory = $true)]
        [System.Data.SqlClient.SqlConnection]$Connection
    )

    $command = $Connection.CreateCommand()
    try {
        $command.CommandText = @'
SELECT CASE
    WHEN OBJECT_ID(N'dbo.SchemaMigrations', N'U') IS NOT NULL
     AND COL_LENGTH(N'dbo.SchemaMigrations', N'MigrationName') IS NOT NULL
     AND COL_LENGTH(N'dbo.SchemaMigrations', N'ChecksumSha256') IS NOT NULL
     AND COL_LENGTH(N'dbo.SchemaMigrations', N'AppliedUtc') IS NOT NULL
    THEN 1 ELSE 0 END;
'@
        return ([int]$command.ExecuteScalar() -eq 1)
    }
    finally {
        $command.Dispose()
    }
}

function Get-RecordedMigrations {
    param(
        [Parameter(Mandatory = $true)]
        [System.Data.SqlClient.SqlConnection]$Connection
    )

    $recorded = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([System.StringComparer]::OrdinalIgnoreCase)
    $command = $Connection.CreateCommand()
    try {
        $command.CommandText = 'SELECT MigrationName, ChecksumSha256 FROM dbo.SchemaMigrations ORDER BY MigrationName;'
        $reader = $command.ExecuteReader()
        try {
            while ($reader.Read()) {
                $name = $reader.GetString(0)
                $checksum = $reader.GetString(1).Trim()
                if ($recorded.ContainsKey($name)) {
                    throw "Duplicate recorded migration '$name' was found."
                }

                $recorded.Add($name, $checksum)
            }
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $command.Dispose()
    }

    return $recorded
}

function Assert-MigrationLedger {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$LocalMigrations,

        [Parameter(Mandatory = $true)]
        [System.Collections.Generic.Dictionary[string,string]]$RecordedMigrations,

        [Parameter(Mandatory = $true)]
        [bool]$DatabaseCreatedThisRun
    )

    $localByName = New-Object 'System.Collections.Generic.Dictionary[string,object]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($migration in $LocalMigrations) {
        $localByName.Add($migration.Name, $migration)
    }

    foreach ($entry in $RecordedMigrations.GetEnumerator()) {
        if (-not $localByName.ContainsKey($entry.Key)) {
            throw "Database contains migration '$($entry.Key)', but the local migration file is missing."
        }

        $local = $localByName[$entry.Key]
        if (-not [System.StringComparer]::OrdinalIgnoreCase.Equals($entry.Value, $local.Checksum)) {
            throw "Checksum mismatch for applied migration '$($entry.Key)'. Applied migrations must not be edited."
        }
    }

    if (-not $DatabaseCreatedThisRun) {
        if (-not $RecordedMigrations.ContainsKey($bootstrapMigrationName)) {
            throw "Database '$DatabaseName' is not a trusted ForteMove database because its bootstrap migration is not recorded."
        }

        $bootstrap = $localByName[$bootstrapMigrationName]
        if (-not [System.StringComparer]::OrdinalIgnoreCase.Equals(
            $RecordedMigrations[$bootstrapMigrationName],
            $bootstrap.Checksum)) {
            throw "Database '$DatabaseName' has an unexpected ForteMove bootstrap checksum."
        }
    }

    $missingEarlierMigration = $false
    foreach ($migration in $LocalMigrations) {
        $isRecorded = $RecordedMigrations.ContainsKey($migration.Name)
        if (-not $isRecorded) {
            $missingEarlierMigration = $true
        }
        elseif ($missingEarlierMigration) {
            throw "Migration ledger has an ordering gap before '$($migration.Name)'."
        }
    }
}

function Invoke-Migration {
    param(
        [Parameter(Mandatory = $true)]
        [System.Data.SqlClient.SqlConnection]$Connection,

        [Parameter(Mandatory = $true)]
        [object]$Migration
    )

    $transaction = $Connection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
    try {
        $migrationCommand = $Connection.CreateCommand()
        try {
            $migrationCommand.Transaction = $transaction
            $migrationCommand.CommandTimeout = 120
            $migrationCommand.CommandText = $Migration.Content
            [void]$migrationCommand.ExecuteNonQuery()
        }
        finally {
            $migrationCommand.Dispose()
        }

        $recordCommand = $Connection.CreateCommand()
        try {
            $recordCommand.Transaction = $transaction
            $recordCommand.CommandText = @'
INSERT dbo.SchemaMigrations (MigrationName, ChecksumSha256)
VALUES (@MigrationName, @ChecksumSha256);
'@
            [void]$recordCommand.Parameters.Add('@MigrationName', [System.Data.SqlDbType]::NVarChar, 260)
            [void]$recordCommand.Parameters.Add('@ChecksumSha256', [System.Data.SqlDbType]::Char, 64)
            $recordCommand.Parameters['@MigrationName'].Value = $Migration.Name
            $recordCommand.Parameters['@ChecksumSha256'].Value = $Migration.Checksum
            [void]$recordCommand.ExecuteNonQuery()
        }
        finally {
            $recordCommand.Dispose()
        }

        $transaction.Commit()
    }
    catch {
        try {
            $transaction.Rollback()
        }
        catch {
            Write-Warning "Migration rollback also failed: $($_.Exception.Message)"
        }

        throw
    }
    finally {
        $transaction.Dispose()
    }
}

function Assert-RoleCatalogue {
    param(
        [Parameter(Mandatory = $true)]
        [System.Data.SqlClient.SqlConnection]$Connection
    )

    $command = $Connection.CreateCommand()
    try {
        $command.CommandText = @'
SELECT
    COUNT_BIG(*) AS TotalRoleCount,
    SUM(CASE WHEN RoleCode IN
        (N'TransportAdministrator', N'Driver', N'Passenger') AND IsActive = 1
        THEN CONVERT(BIGINT, 1) ELSE CONVERT(BIGINT, 0) END) AS ExpectedActiveRoleCount
FROM dbo.Roles;
'@
        $reader = $command.ExecuteReader()
        try {
            if (-not $reader.Read()) {
                throw 'Could not verify the role catalogue.'
            }

            $totalRoleCount = $reader.GetInt64(0)
            $expectedActiveRoleCount = $reader.GetInt64(1)
            if ($totalRoleCount -ne 3 -or $expectedActiveRoleCount -ne 3) {
                throw 'Role catalogue must contain exactly the active TransportAdministrator, Driver, and Passenger roles.'
            }
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $command.Dispose()
    }
}

function Test-TransportAdministratorExists {
    param(
        [Parameter(Mandatory = $true)]
        [System.Data.SqlClient.SqlConnection]$Connection
    )

    $command = $Connection.CreateCommand()
    try {
        $command.CommandText = @'
SELECT COUNT_BIG(*)
FROM dbo.UserAccounts AS account
INNER JOIN dbo.Roles AS role ON role.RoleId = account.RoleId
WHERE role.RoleCode = @RoleCode;
'@
        [void]$command.Parameters.Add('@RoleCode', [System.Data.SqlDbType]::NVarChar, 50)
        $command.Parameters['@RoleCode'].Value = 'TransportAdministrator'
        return ([long]$command.ExecuteScalar() -gt 0)
    }
    finally {
        $command.Dispose()
    }
}

function Read-RequiredText {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Prompt,

        [Parameter(Mandatory = $true)]
        [int]$MaximumLength
    )

    while ($true) {
        $value = (Read-Host -Prompt $Prompt).Trim()
        if ($value.Length -eq 0) {
            Write-Warning 'A value is required.'
            continue
        }

        if ($value.Length -gt $MaximumLength) {
            Write-Warning "The value cannot exceed $MaximumLength characters."
            continue
        }

        return $value
    }
}

function Read-AdministratorEmail {
    while ($true) {
        $email = Read-RequiredText -Prompt 'Administrator email' -MaximumLength 254
        try {
            $address = New-Object System.Net.Mail.MailAddress $email
            if (-not [string]::Equals($address.Address, $email, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw 'Display names are not accepted.'
            }

            return $email
        }
        catch {
            Write-Warning 'Enter a single valid email address without a display name.'
        }
    }
}

function Convert-SecureStringToUtf8Bytes {
    param(
        [Parameter(Mandatory = $true)]
        [System.Security.SecureString]$Value
    )

    $pointer = [IntPtr]::Zero
    $characters = New-Object 'System.Char[]' $Value.Length
    try {
        $pointer = [System.Runtime.InteropServices.Marshal]::SecureStringToGlobalAllocUnicode($Value)
        for ($index = 0; $index -lt $Value.Length; $index++) {
            $characters[$index] = [char][System.Runtime.InteropServices.Marshal]::ReadInt16($pointer, $index * 2)
        }

        return ,([System.Text.Encoding]::UTF8.GetBytes($characters))
    }
    finally {
        if ($characters.Length -gt 0) {
            [System.Array]::Clear($characters, 0, $characters.Length)
        }

        if ($pointer -ne [IntPtr]::Zero) {
            [System.Runtime.InteropServices.Marshal]::ZeroFreeGlobalAllocUnicode($pointer)
        }
    }
}

function Test-ByteArraysEqual {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$First,

        [Parameter(Mandatory = $true)]
        [byte[]]$Second
    )

    $difference = $First.Length -bxor $Second.Length
    $maximumLength = [System.Math]::Max($First.Length, $Second.Length)
    for ($index = 0; $index -lt $maximumLength; $index++) {
        $firstByte = 0
        $secondByte = 0
        if ($index -lt $First.Length) {
            $firstByte = $First[$index]
        }

        if ($index -lt $Second.Length) {
            $secondByte = $Second[$index]
        }

        $difference = $difference -bor ($firstByte -bxor $secondByte)
    }

    return ($difference -eq 0)
}

function Read-ConfirmedPasswordBytes {
    while ($true) {
        $password = Read-Host -Prompt 'Administrator password (15-128 characters)' -AsSecureString
        if ($password.Length -lt 15 -or $password.Length -gt 128) {
            Write-Warning 'The password must be between 15 and 128 characters.'
            $password.Dispose()
            continue
        }

        $confirmation = Read-Host -Prompt 'Confirm administrator password' -AsSecureString
        $passwordBytes = $null
        $confirmationBytes = $null
        $accepted = $false
        try {
            $passwordBytes = Convert-SecureStringToUtf8Bytes -Value $password
            $confirmationBytes = Convert-SecureStringToUtf8Bytes -Value $confirmation

            if (-not (Test-ByteArraysEqual -First $passwordBytes -Second $confirmationBytes)) {
                Write-Warning 'The password confirmation does not match.'
                continue
            }

            $accepted = $true
            return ,$passwordBytes
        }
        finally {
            $password.Dispose()
            $confirmation.Dispose()

            if ($null -ne $confirmationBytes) {
                [System.Array]::Clear($confirmationBytes, 0, $confirmationBytes.Length)
            }

            if (-not $accepted -and $null -ne $passwordBytes) {
                [System.Array]::Clear($passwordBytes, 0, $passwordBytes.Length)
            }
        }
    }
}

function New-PasswordMaterial {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$PasswordBytes
    )

    $salt = New-Object 'System.Byte[]' $saltLength
    $random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $deriver = $null
    $succeeded = $false
    try {
        $random.GetBytes($salt)
        $deriver = [System.Security.Cryptography.Rfc2898DeriveBytes]::new(
            $PasswordBytes,
            $salt,
            $passwordIterations,
            [System.Security.Cryptography.HashAlgorithmName]::SHA256)
        $hash = $deriver.GetBytes($derivedKeyLength)
        $succeeded = $true
        return [pscustomobject]@{
            Salt = $salt
            Hash = $hash
        }
    }
    finally {
        if ($null -ne $deriver) {
            $deriver.Dispose()
        }

        $random.Dispose()

        if (-not $succeeded) {
            [System.Array]::Clear($salt, 0, $salt.Length)
        }
    }
}

function Add-InitialAdministrator {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ConnectionString,

        [Parameter(Mandatory = $true)]
        [string]$Email,

        [Parameter(Mandatory = $true)]
        [string]$EmployeeNumber,

        [Parameter(Mandatory = $true)]
        [string]$FirstName,

        [Parameter(Mandatory = $true)]
        [string]$LastName,

        [Parameter(Mandatory = $true)]
        [byte[]]$PasswordHash,

        [Parameter(Mandatory = $true)]
        [byte[]]$PasswordSalt
    )

    $connection = New-OpenSqlConnection -ConnectionString $ConnectionString
    try {
        $transaction = $connection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
        try {
            $roleCommand = $connection.CreateCommand()
            try {
                $roleCommand.Transaction = $transaction
                $roleCommand.CommandText = @'
SELECT RoleId
FROM dbo.Roles WITH (UPDLOCK, HOLDLOCK)
WHERE RoleCode = @RoleCode AND IsActive = 1;
'@
                [void]$roleCommand.Parameters.Add('@RoleCode', [System.Data.SqlDbType]::NVarChar, 50)
                $roleCommand.Parameters['@RoleCode'].Value = 'TransportAdministrator'
                $roleResult = $roleCommand.ExecuteScalar()
                if ($null -eq $roleResult -or $roleResult -is [System.DBNull]) {
                    throw 'The active TransportAdministrator role was not found.'
                }

                $roleId = [int]$roleResult
            }
            finally {
                $roleCommand.Dispose()
            }

            $existingCommand = $connection.CreateCommand()
            try {
                $existingCommand.Transaction = $transaction
                $existingCommand.CommandText = @'
SELECT COUNT_BIG(*)
FROM dbo.UserAccounts WITH (UPDLOCK, HOLDLOCK)
WHERE RoleId = @RoleId;
'@
                [void]$existingCommand.Parameters.Add('@RoleId', [System.Data.SqlDbType]::Int)
                $existingCommand.Parameters['@RoleId'].Value = $roleId
                if ([long]$existingCommand.ExecuteScalar() -gt 0) {
                    throw 'A Transport Administrator already exists. The initializer will not overwrite it or add another seeded administrator.'
                }
            }
            finally {
                $existingCommand.Dispose()
            }

            $accountCommand = $connection.CreateCommand()
            try {
                $accountCommand.Transaction = $transaction
                $accountCommand.CommandText = @'
INSERT dbo.UserAccounts
(
    RoleId,
    Email,
    NormalizedEmail,
    PasswordAlgorithm,
    PasswordHash,
    PasswordSalt,
    PasswordIterations,
    IsActive,
    MustChangePassword,
    FailedLoginCount
)
VALUES
(
    @RoleId,
    @Email,
    @NormalizedEmail,
    @PasswordAlgorithm,
    @PasswordHash,
    @PasswordSalt,
    @PasswordIterations,
    1,
    0,
    0
);
SELECT CONVERT(BIGINT, SCOPE_IDENTITY());
'@
                [void]$accountCommand.Parameters.Add('@RoleId', [System.Data.SqlDbType]::Int)
                [void]$accountCommand.Parameters.Add('@Email', [System.Data.SqlDbType]::NVarChar, 254)
                [void]$accountCommand.Parameters.Add('@NormalizedEmail', [System.Data.SqlDbType]::NVarChar, 254)
                [void]$accountCommand.Parameters.Add('@PasswordAlgorithm', [System.Data.SqlDbType]::NVarChar, 50)
                [void]$accountCommand.Parameters.Add('@PasswordHash', [System.Data.SqlDbType]::VarBinary, 64)
                [void]$accountCommand.Parameters.Add('@PasswordSalt', [System.Data.SqlDbType]::VarBinary, 64)
                [void]$accountCommand.Parameters.Add('@PasswordIterations', [System.Data.SqlDbType]::Int)
                $accountCommand.Parameters['@RoleId'].Value = $roleId
                $accountCommand.Parameters['@Email'].Value = $Email
                $accountCommand.Parameters['@NormalizedEmail'].Value = $Email.ToUpperInvariant()
                $accountCommand.Parameters['@PasswordAlgorithm'].Value = $passwordAlgorithm
                $accountCommand.Parameters['@PasswordHash'].Value = $PasswordHash
                $accountCommand.Parameters['@PasswordSalt'].Value = $PasswordSalt
                $accountCommand.Parameters['@PasswordIterations'].Value = $passwordIterations
                $userAccountId = [long]$accountCommand.ExecuteScalar()
            }
            finally {
                $accountCommand.Dispose()
            }

            $staffCommand = $connection.CreateCommand()
            try {
                $staffCommand.Transaction = $transaction
                $staffCommand.CommandText = @'
INSERT dbo.StaffProfiles
(
    UserAccountId,
    EmployeeNumber,
    FirstName,
    LastName,
    EmploymentStatus
)
VALUES
(
    @UserAccountId,
    @EmployeeNumber,
    @FirstName,
    @LastName,
    @EmploymentStatus
);
'@
                [void]$staffCommand.Parameters.Add('@UserAccountId', [System.Data.SqlDbType]::BigInt)
                [void]$staffCommand.Parameters.Add('@EmployeeNumber', [System.Data.SqlDbType]::NVarChar, 50)
                [void]$staffCommand.Parameters.Add('@FirstName', [System.Data.SqlDbType]::NVarChar, 100)
                [void]$staffCommand.Parameters.Add('@LastName', [System.Data.SqlDbType]::NVarChar, 100)
                [void]$staffCommand.Parameters.Add('@EmploymentStatus', [System.Data.SqlDbType]::NVarChar, 30)
                $staffCommand.Parameters['@UserAccountId'].Value = $userAccountId
                $staffCommand.Parameters['@EmployeeNumber'].Value = $EmployeeNumber
                $staffCommand.Parameters['@FirstName'].Value = $FirstName
                $staffCommand.Parameters['@LastName'].Value = $LastName
                $staffCommand.Parameters['@EmploymentStatus'].Value = 'Active'
                [void]$staffCommand.ExecuteNonQuery()
            }
            finally {
                $staffCommand.Dispose()
            }

            $auditCommand = $connection.CreateCommand()
            try {
                $auditCommand.Transaction = $transaction
                $auditCommand.CommandText = @'
INSERT dbo.AuditEntries
(
    ActorUserAccountId,
    EventType,
    EntityType,
    EntityId,
    Detail,
    ClientIpAddress
)
VALUES
(
    @ActorUserAccountId,
    @EventType,
    @EntityType,
    @EntityId,
    @Detail,
    NULL
);
'@
                [void]$auditCommand.Parameters.Add('@ActorUserAccountId', [System.Data.SqlDbType]::BigInt)
                [void]$auditCommand.Parameters.Add('@EventType', [System.Data.SqlDbType]::NVarChar, 100)
                [void]$auditCommand.Parameters.Add('@EntityType', [System.Data.SqlDbType]::NVarChar, 100)
                [void]$auditCommand.Parameters.Add('@EntityId', [System.Data.SqlDbType]::NVarChar, 100)
                [void]$auditCommand.Parameters.Add('@Detail', [System.Data.SqlDbType]::NVarChar, 1000)
                $auditCommand.Parameters['@ActorUserAccountId'].Value = $userAccountId
                $auditCommand.Parameters['@EventType'].Value = 'AdministratorSeeded'
                $auditCommand.Parameters['@EntityType'].Value = 'UserAccount'
                $auditCommand.Parameters['@EntityId'].Value = $userAccountId.ToString([System.Globalization.CultureInfo]::InvariantCulture)
                $auditCommand.Parameters['@Detail'].Value = 'Initial Transport Administrator account created by the database initializer.'
                [void]$auditCommand.ExecuteNonQuery()
            }
            finally {
                $auditCommand.Dispose()
            }

            $transaction.Commit()
            return $userAccountId
        }
        catch {
            try {
                $transaction.Rollback()
            }
            catch {
                Write-Warning "Administrator seed rollback also failed: $($_.Exception.Message)"
            }

            throw
        }
        finally {
            $transaction.Dispose()
        }
    }
    finally {
        $connection.Dispose()
    }
}

if ($DatabaseName -notmatch '^[A-Za-z][A-Za-z0-9_]{0,127}$') {
    throw 'DatabaseName must start with a letter and contain only letters, digits, or underscores (maximum 128 characters).'
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$migrationDirectory = Join-Path $repositoryRoot 'database\migrations'
$migrations = @(Get-LocalMigrations -MigrationDirectory $migrationDirectory)

$masterConnectionString = New-ForteMoveConnectionString -InitialCatalog 'master'
$targetConnectionString = New-ForteMoveConnectionString -InitialCatalog $DatabaseName
$databaseCreatedThisRun = $false

Write-Host "Checking SQL Server instance '$ServerInstance'..."
$masterConnection = New-OpenSqlConnection -ConnectionString $masterConnectionString
try {
    $existsCommand = $masterConnection.CreateCommand()
    try {
        $existsCommand.CommandText = 'SELECT CASE WHEN DB_ID(@DatabaseName) IS NULL THEN 0 ELSE 1 END;'
        [void]$existsCommand.Parameters.Add('@DatabaseName', [System.Data.SqlDbType]::NVarChar, 128)
        $existsCommand.Parameters['@DatabaseName'].Value = $DatabaseName
        $databaseExists = ([int]$existsCommand.ExecuteScalar() -eq 1)
    }
    finally {
        $existsCommand.Dispose()
    }

    if (-not $databaseExists) {
        $createCommand = $masterConnection.CreateCommand()
        try {
            $quotedDatabaseName = Quote-SqlIdentifier -Value $DatabaseName
            $createCommand.CommandTimeout = 120
            $createCommand.CommandText = "CREATE DATABASE $quotedDatabaseName;"
            [void]$createCommand.ExecuteNonQuery()
            $databaseCreatedThisRun = $true
            Write-Host "Created database '$DatabaseName'."
        }
        finally {
            $createCommand.Dispose()
        }
    }
}
finally {
    $masterConnection.Dispose()
}

$administratorAlreadyExists = $false
$targetConnection = New-OpenSqlConnection -ConnectionString $targetConnectionString
$migrationLockHeld = $false
try {
    Enter-MigrationLock -Connection $targetConnection
    $migrationLockHeld = $true

    $stateCommand = $targetConnection.CreateCommand()
    try {
        $stateCommand.CommandText = "SELECT CONVERT(NVARCHAR(60), DATABASEPROPERTYEX(DB_NAME(), 'Updateability'));"
        $updateability = [string]$stateCommand.ExecuteScalar()
        if (-not [string]::Equals($updateability, 'READ_WRITE', [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Database '$DatabaseName' is not read-write."
        }
    }
    finally {
        $stateCommand.Dispose()
    }

    $hasMigrationTable = Test-SchemaMigrationsTable -Connection $targetConnection
    if (-not $hasMigrationTable -and -not $databaseCreatedThisRun) {
        throw "Database '$DatabaseName' already exists but is not a recognized ForteMove database. No changes were made."
    }

    if ($hasMigrationTable) {
        $recordedMigrations = Get-RecordedMigrations -Connection $targetConnection
    }
    else {
        $recordedMigrations = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([System.StringComparer]::OrdinalIgnoreCase)
    }

    Assert-MigrationLedger `
        -LocalMigrations $migrations `
        -RecordedMigrations $recordedMigrations `
        -DatabaseCreatedThisRun $databaseCreatedThisRun

    foreach ($migration in $migrations) {
        if ($recordedMigrations.ContainsKey($migration.Name)) {
            Write-Host "Migration $($migration.Name) is already applied."
            continue
        }

        Write-Host "Applying migration $($migration.Name)..."
        Invoke-Migration -Connection $targetConnection -Migration $migration
        $recordedMigrations.Add($migration.Name, $migration.Checksum)
    }

    Assert-RoleCatalogue -Connection $targetConnection
    $administratorAlreadyExists = Test-TransportAdministratorExists -Connection $targetConnection
}
finally {
    if ($migrationLockHeld) {
        try {
            Exit-MigrationLock -Connection $targetConnection
        }
        catch {
            Write-Warning "Could not explicitly release the migration lock; closing the SQL connection will release it. $($_.Exception.Message)"
        }
    }

    $targetConnection.Dispose()
}

Write-Host "Database migrations are current for '$DatabaseName'."

if ($SkipAdministratorSeed) {
    Write-Warning 'Administrator seeding was skipped. This switch is intended only for schema verification and migration reruns.'
    return
}

if ($administratorAlreadyExists) {
    Write-Host 'A Transport Administrator already exists. It was left unchanged and no second administrator was seeded.'
    return
}

Write-Host 'No Transport Administrator exists. Enter the final credentials for the initial account.'
$administratorEmail = Read-AdministratorEmail
$employeeNumber = Read-RequiredText -Prompt 'Employee number' -MaximumLength 50
$firstName = Read-RequiredText -Prompt 'First name' -MaximumLength 100
$lastName = Read-RequiredText -Prompt 'Last name' -MaximumLength 100
$passwordBytes = Read-ConfirmedPasswordBytes
$passwordMaterial = $null
try {
    Write-Host 'Deriving the password hash. This intentionally takes a moment...'
    $passwordMaterial = New-PasswordMaterial -PasswordBytes $passwordBytes
    $newUserAccountId = Add-InitialAdministrator `
        -ConnectionString $targetConnectionString `
        -Email $administratorEmail `
        -EmployeeNumber $employeeNumber `
        -FirstName $firstName `
        -LastName $lastName `
        -PasswordHash $passwordMaterial.Hash `
        -PasswordSalt $passwordMaterial.Salt

    Write-Host "Created the initial Transport Administrator account (UserAccountId $newUserAccountId)."
}
finally {
    if ($null -ne $passwordBytes) {
        [System.Array]::Clear($passwordBytes, 0, $passwordBytes.Length)
    }

    if ($null -ne $passwordMaterial) {
        [System.Array]::Clear($passwordMaterial.Hash, 0, $passwordMaterial.Hash.Length)
        [System.Array]::Clear($passwordMaterial.Salt, 0, $passwordMaterial.Salt.Length)
    }
}
