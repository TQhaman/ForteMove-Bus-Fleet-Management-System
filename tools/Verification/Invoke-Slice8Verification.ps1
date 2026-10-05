param(
    [Parameter(Mandatory=$true)][string]$AssembliesDirectory,
    [switch]$IncludeDatabaseChecks,
    [switch]$IncludeConcurrencyChecks
)
$ErrorActionPreference='Stop'
$slice8Root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$slice8Output=Join-Path $PSScriptRoot ('bin/Slice8-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $slice8Output | Out-Null
foreach($slice8Assembly in @('Models','Business','Data')) {
    Copy-Item -LiteralPath (Join-Path $AssembliesDirectory "ForteMove.$slice8Assembly.dll") -Destination $slice8Output
}
$slice8Compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$slice8References=@('/r:System.Data.dll')
foreach($slice8Assembly in @('Models','Business','Data')) { $slice8References+="/r:$(Join-Path $slice8Output "ForteMove.$slice8Assembly.dll")" }
$slice8Tests=@('Slice8BusinessVerification')
if($IncludeDatabaseChecks){$slice8Tests+='Slice8RollbackVerification'}
if($IncludeConcurrencyChecks){$slice8Tests+='Slice8ConcurrencyVerification'}
foreach($slice8Test in $slice8Tests) {
    & $slice8Compiler /nologo "/out:$(Join-Path $slice8Output "$slice8Test.exe")" @slice8References (Join-Path $PSScriptRoot "$slice8Test.cs")
    if($LASTEXITCODE -ne 0){throw "$slice8Test compilation failed."}
}
foreach($slice8Test in ($slice8Tests | Where-Object {$_ -ne 'Slice8ConcurrencyVerification'})) {
    & (Join-Path $slice8Output "$slice8Test.exe")
    if($LASTEXITCODE -ne 0){throw "$slice8Test failed."}
}
if($IncludeConcurrencyChecks) {
    # Only this newly generated, disposable database is eligible for cleanup.
    # No accepted database is copied/exported, and no test identities enter ForteMove.
    $slice8Database='ForteMove_Slice8_Verification_'+[Guid]::NewGuid().ToString('N')
    if($slice8Database -notmatch '^ForteMove_Slice8_Verification_[a-f0-9]{32}$'){throw 'Unsafe verification database name.'}
    try {
        & (Join-Path $slice8Root 'tools/Initialize-ForteMoveDatabase.ps1') -DatabaseName $slice8Database -SkipAdministratorSeed
        & (Join-Path $slice8Output 'Slice8ConcurrencyVerification.exe') $slice8Database
        if($LASTEXITCODE -ne 0){throw 'Concurrency/lifecycle verification failed.'}
    }
    finally {
        # Explicit, validated identifier originated in this runner; never a caller database name.
        $slice8Cleanup=New-Object System.Data.SqlClient.SqlConnection 'Data Source=.\SQLEXPRESS;Initial Catalog=master;Integrated Security=True'
        try {
            $slice8Cleanup.Open()
            $slice8Command=$slice8Cleanup.CreateCommand()
            $slice8Command.CommandText="IF DB_ID(@Name) IS NOT NULL BEGIN ALTER DATABASE [$slice8Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$slice8Database]; END;"
            $slice8Command.Parameters.Add('@Name',[System.Data.SqlDbType]::NVarChar,128).Value=$slice8Database
            [void]$slice8Command.ExecuteNonQuery()
            Write-Output "Removed disposable verification database $slice8Database. No accepted database was changed by concurrency tests."
        } finally {$slice8Cleanup.Dispose()}
    }
}
