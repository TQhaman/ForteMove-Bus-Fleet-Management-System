param(
    [Parameter(Mandatory=$true)][string]$AssembliesDirectory,
    [switch]$IncludeDatabaseChecks,
    [switch]$IncludeConcurrencyChecks
)
$ErrorActionPreference='Stop'
$slice9Root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$slice9Output=Join-Path $PSScriptRoot ('bin/Slice9-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $slice9Output | Out-Null
foreach($slice9Assembly in @('Models','Business','Data')) {
    Copy-Item -LiteralPath (Join-Path $AssembliesDirectory "ForteMove.$slice9Assembly.dll") -Destination $slice9Output
}
$slice9Compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$slice9References=@('/r:System.Data.dll')
foreach($slice9Assembly in @('Models','Business','Data')) { $slice9References+="/r:$(Join-Path $slice9Output "ForteMove.$slice9Assembly.dll")" }
$slice9Tests=@('Slice9BusinessVerification')
if($IncludeDatabaseChecks){$slice9Tests+='Slice9RollbackVerification'}
if($IncludeConcurrencyChecks){$slice9Tests+='Slice9ConcurrencyVerification'}
foreach($slice9Test in $slice9Tests) {
    if($slice9Test -eq "Slice9ConcurrencyVerification"){
        & $slice9Compiler /nologo /main:Slice9ConcurrencyVerification "/out:$(Join-Path $slice9Output "$slice9Test.exe")" @slice9References (Join-Path $PSScriptRoot "$slice9Test.cs") (Join-Path $PSScriptRoot "Slice8ConcurrencyVerification.cs")
    } else {
        & $slice9Compiler /nologo "/out:$(Join-Path $slice9Output "$slice9Test.exe")" @slice9References (Join-Path $PSScriptRoot "$slice9Test.cs")
    }
    if($LASTEXITCODE -ne 0){throw "$slice9Test compilation failed."}
}
foreach($slice9Test in ($slice9Tests | Where-Object {$_ -ne 'Slice9ConcurrencyVerification'})) {
    & (Join-Path $slice9Output "$slice9Test.exe")
    if($LASTEXITCODE -ne 0){throw "$slice9Test failed."}
}
if($IncludeConcurrencyChecks) {
    # Only this newly generated, disposable database is eligible for cleanup.
    # No accepted database is copied/exported, and no test identities enter ForteMove.
    $slice9Database='ForteMove_Slice9_Verification_'+[Guid]::NewGuid().ToString('N')
    if($slice9Database -notmatch '^ForteMove_Slice9_Verification_[a-f0-9]{32}$'){throw 'Unsafe verification database name.'}
    try {
        & (Join-Path $slice9Root 'tools/Initialize-ForteMoveDatabase.ps1') -DatabaseName $slice9Database -SkipAdministratorSeed
        & (Join-Path $slice9Output 'Slice9ConcurrencyVerification.exe') $slice9Database
        if($LASTEXITCODE -ne 0){throw 'Concurrency/lifecycle verification failed.'}
        & (Join-Path $PSScriptRoot 'Invoke-Slice9PageRenderVerification.ps1') -AssembliesDirectory $AssembliesDirectory -DatabaseName $slice9Database
    }
    finally {
        # Explicit, validated identifier originated in this runner; never a caller database name.
        $slice9Cleanup=New-Object System.Data.SqlClient.SqlConnection 'Data Source=.\SQLEXPRESS;Initial Catalog=master;Integrated Security=True'
        try {
            $slice9Cleanup.Open()
            $slice9Command=$slice9Cleanup.CreateCommand()
            $slice9Command.CommandText="IF DB_ID(@Name) IS NOT NULL BEGIN ALTER DATABASE [$slice9Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$slice9Database]; END;"
            $slice9Command.Parameters.Add('@Name',[System.Data.SqlDbType]::NVarChar,128).Value=$slice9Database
            [void]$slice9Command.ExecuteNonQuery()
            Write-Output "Removed disposable verification database $slice9Database. No accepted database was changed by concurrency tests."
        } finally {$slice9Cleanup.Dispose()}
    }
}
