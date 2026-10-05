param([string]$BaselineCommit='c65a0b6')
$ErrorActionPreference='Stop'
$slice9Root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location $slice9Root
try {
    $slice9Old=Get-ChildItem database/migrations -Filter '*.sql' | Where-Object {$_.Name -match '^000[0-8]_'} | Sort-Object Name
    if($slice9Old.Count -ne 9){throw 'Expected nine immutable previous migrations.'}
    foreach($slice9Migration in $slice9Old) {
        $slice9Path='database/migrations/'+$slice9Migration.Name
        $slice9Expected=& git -c "safe.directory=$slice9Root" rev-parse "${BaselineCommit}:$slice9Path"
        if($LASTEXITCODE -ne 0){throw 'Cannot inspect baseline migration.'}
        $slice9Expected=$slice9Expected.Trim()
        $slice9Actual=(& git -c "safe.directory=$slice9Root" hash-object "--path=$slice9Path" $slice9Path).Trim()
        if($slice9Actual -ne $slice9Expected){throw "Baseline migration changed: $slice9Path"}
    }
    $slice9Sql=New-Object System.Data.SqlClient.SqlConnection 'Data Source=.\SQLEXPRESS;Initial Catalog=ForteMove;Integrated Security=True'
    $slice9Sql.Open()
    try {
        $slice9Command=$slice9Sql.CreateCommand()
        $slice9Command.CommandText='SELECT MigrationName,ChecksumSha256 FROM dbo.SchemaMigrations;'
        $slice9Recorded=@{};$slice9Reader=$slice9Command.ExecuteReader()
        while($slice9Reader.Read()){$slice9Recorded[$slice9Reader.GetString(0)]=$slice9Reader.GetString(1)}
        $slice9Reader.Close()
        foreach($slice9Migration in (Get-ChildItem database/migrations -Filter '*.sql')) {
            $slice9Text=[IO.File]::ReadAllText($slice9Migration.FullName).Replace("`r`n","`n").Replace("`r","`n")
            $slice9Hash=[Security.Cryptography.SHA256]::Create()
            try {$slice9Digest=[BitConverter]::ToString($slice9Hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($slice9Text))).Replace('-','').ToLowerInvariant()}
            finally {$slice9Hash.Dispose()}
            if($slice9Recorded[$slice9Migration.Name] -ne $slice9Digest){throw "Database checksum mismatch: $($slice9Migration.Name)"}
        }
        $slice9Command.CommandText=@'
SELECT N'RepairProviders',COUNT_BIG(*) FROM dbo.RepairProviders
UNION ALL SELECT N'MaintenancePlans',COUNT_BIG(*) FROM dbo.MaintenancePlans
UNION ALL SELECT N'MaintenanceWorkOrders',COUNT_BIG(*) FROM dbo.MaintenanceWorkOrders
UNION ALL SELECT N'MaintenanceProgressEntries',COUNT_BIG(*) FROM dbo.MaintenanceProgressEntries
UNION ALL SELECT N'BusVehicleStatusHistory',COUNT_BIG(*) FROM dbo.BusVehicleStatusHistory;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id IN(OBJECT_ID(N'dbo.RepairProviders'),OBJECT_ID(N'dbo.MaintenancePlans'),OBJECT_ID(N'dbo.MaintenanceWorkOrders'),OBJECT_ID(N'dbo.MaintenanceProgressEntries'),OBJECT_ID(N'dbo.BusVehicleStatusHistory')) AND (is_disabled=1 OR is_not_trusted=1)) THROW 51303,'Untrusted maintenance foreign key.',1;
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id IN(OBJECT_ID(N'dbo.RepairProviders'),OBJECT_ID(N'dbo.MaintenancePlans'),OBJECT_ID(N'dbo.MaintenanceWorkOrders'),OBJECT_ID(N'dbo.MaintenanceProgressEntries'),OBJECT_ID(N'dbo.BusVehicleStatusHistory')) AND (is_disabled=1 OR is_not_trusted=1)) THROW 51304,'Untrusted maintenance check.',1;
IF (SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.MaintenanceWorkOrders') AND is_unique=1 AND has_filter=1)<>4 THROW 51305,'Missing maintenance filtered uniqueness.',1;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id IN(OBJECT_ID(N'dbo.RepairProviders'),OBJECT_ID(N'dbo.MaintenancePlans'),OBJECT_ID(N'dbo.MaintenanceWorkOrders')) AND system_type_id=189)<>3 THROW 51306,'Missing maintenance rowversion.',1;
IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.MaintenanceWorkOrders') AND name=N'CompletedCost' AND precision=12 AND scale=2) THROW 51307,'Incorrect cost money type.',1;
SELECT name,filter_definition FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.MaintenanceWorkOrders') AND has_filter=1;
SELECT name FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.MaintenanceWorkOrders');
'@
        $slice9Reader=$slice9Command.ExecuteReader()
        do {while($slice9Reader.Read()){ $slice9Values=@();for($slice9Index=0;$slice9Index -lt $slice9Reader.FieldCount;$slice9Index++){$slice9Values+=[string]$slice9Reader.GetValue($slice9Index)};Write-Output ($slice9Values -join ' | ') }} while($slice9Reader.NextResult())
        $slice9Reader.Close()
    } finally {$slice9Sql.Dispose()}
    $slice9Violations=& rg -n 'AddWithValue' src
    if($LASTEXITCODE -eq 0){throw "AddWithValue found: $slice9Violations"}
    $slice9Violations=& rg -n 'System.Data.SqlClient|SqlConnection|SqlCommand|SqlDataReader|SqlTransaction' src/ForteMove.Business src/ForteMove.Web -g '*.cs'
    if($LASTEXITCODE -eq 0){throw "ADO.NET outside Data: $slice9Violations"}
    $slice9LogoPaths=& git -c "safe.directory=$slice9Root" ls-tree -r --name-only $BaselineCommit src/ForteMove.Web/Content
    foreach($slice9Logo in ($slice9LogoPaths | Where-Object {$_ -match '/Brand/'})) {
        $slice9Expected=(& git -c "safe.directory=$slice9Root" rev-parse "${BaselineCommit}:$slice9Logo").Trim()
        $slice9Actual=(& git -c "safe.directory=$slice9Root" hash-object "--path=$slice9Logo" $slice9Logo).Trim()
        if($slice9Expected -ne $slice9Actual){throw "Logo changed: $slice9Logo"}
    }
    $slice9Violations=& rg -n '\b(SELECT\s+.+\s+FROM|INSERT\s+INTO|UPDATE\s+dbo\.|DELETE\s+FROM|EXEC\s+sys\.)' src/ForteMove.Business src/ForteMove.Web -g '*.cs'
    if($LASTEXITCODE -eq 0){throw "SQL outside Data: $slice9Violations"}
    foreach($slice9Project in (Get-ChildItem src -Recurse -Filter 'ForteMove.*.csproj')) {
        [xml]$slice9ProjectXml=Get-Content -LiteralPath $slice9Project.FullName -Raw
        $slice9Entries=@($slice9ProjectXml.SelectNodes("//*[local-name()='Compile' or local-name()='Content']") | ForEach-Object {$_.Include.Replace('\','/')})
        foreach($slice9Source in (Get-ChildItem -LiteralPath $slice9Project.DirectoryName -Recurse -File | Where-Object {$_.Extension -in '.cs','.aspx','.ascx','.master','.ashx'})) {
            $slice9Relative=$slice9Source.FullName.Substring($slice9Project.DirectoryName.Length+1).Replace('\','/')
            if($slice9Relative -match '^(bin|obj)/'){continue}
            if($slice9Relative -notin $slice9Entries){throw "Unregistered source/page: $slice9Relative"}
        }
    }
    & git -c "safe.directory=$slice9Root" diff --check
    if($LASTEXITCODE -ne 0){throw 'Whitespace check failed.'}
    Write-Output 'Old migration Git objects unchanged; all ten live checksums match; architecture/project-entry/brand/whitespace scans pass.'
} finally {Pop-Location}
