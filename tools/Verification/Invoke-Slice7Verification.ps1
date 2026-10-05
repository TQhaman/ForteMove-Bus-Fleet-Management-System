param([switch]$IncludeDatabaseChecks)
$ErrorActionPreference = 'Stop'
$slice7Root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$slice7Output = Join-Path $PSScriptRoot 'bin'
$slice7WebBin = Join-Path $slice7Root 'src/ForteMove.Web/bin'
$slice7Compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
New-Item -ItemType Directory -Path $slice7Output -Force | Out-Null
$slice7References = @('/r:System.Data.dll', '/r:System.Web.dll', '/r:System.Web.Extensions.dll')
foreach ($slice7Assembly in @('Models','Business','Data','Web')) {
    $slice7Dll = Join-Path $slice7WebBin ("ForteMove.$slice7Assembly.dll")
    $slice7References += "/r:$slice7Dll"
    Copy-Item -LiteralPath $slice7Dll -Destination $slice7Output
}
$slice7Harnesses = @('Slice7BusinessVerification')
if ($IncludeDatabaseChecks) { $slice7Harnesses += @('Slice7RollbackVerification','Slice7ReadOnlyIntegration') }
foreach ($slice7Harness in $slice7Harnesses) {
    $slice7Exe = Join-Path $slice7Output "$slice7Harness.exe"
    & $slice7Compiler /nologo "/out:$slice7Exe" @slice7References (Join-Path $PSScriptRoot "$slice7Harness.cs")
    if ($LASTEXITCODE -ne 0) { throw "Could not compile $slice7Harness." }
    if ($slice7Harness -eq 'Slice7ReadOnlyIntegration') {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot "$slice7Harness.exe.config") -Destination $slice7Output
    }
    & $slice7Exe
    if ($LASTEXITCODE -ne 0) { throw "$slice7Harness failed." }
}
