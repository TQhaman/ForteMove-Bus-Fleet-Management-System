param([Parameter(Mandatory=$true)][string]$AssembliesDirectory,[switch]$Precompile)
$ErrorActionPreference='Stop'
$slice8Root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$slice8Web=Join-Path $slice8Root 'src/ForteMove.Web'
$slice8Output=Join-Path $PSScriptRoot ('bin/Slice8Pages-'+[Guid]::NewGuid().ToString('N'))
$slice8Bin=Join-Path $slice8Output 'bin'
New-Item -ItemType Directory -Path $slice8Bin -Force | Out-Null
foreach($slice8Item in @('Admin','Driver','Passenger','Controls','Tracking','Account','Errors','Content','Scripts','Web.config','Default.aspx','Global.asax')) {
    Copy-Item -LiteralPath (Join-Path $slice8Web $slice8Item) -Destination $slice8Output -Recurse
}
Get-ChildItem -LiteralPath $AssembliesDirectory -Filter '*.dll' | Copy-Item -Destination $slice8Bin
$slice8References=@('/r:System.Data.dll','/r:System.Configuration.dll','/r:System.Web.dll')
foreach($slice8Assembly in @('Models','Business','Data','Web')) { $slice8References+="/r:$(Join-Path $slice8Bin "ForteMove.$slice8Assembly.dll")" }
$slice8Compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$slice8Exe=Join-Path $slice8Bin 'Slice8PageRenderVerification.exe'
& $slice8Compiler /nologo "/out:$slice8Exe" @slice8References (Join-Path $PSScriptRoot 'Slice8PageRenderVerification.cs')
if($LASTEXITCODE -ne 0){throw 'Runtime verification compilation failed.'}
& $slice8Exe $slice8Output
if($LASTEXITCODE -ne 0){throw 'Runtime page verification failed.'}
if($Precompile) {
    $slice8Precompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/aspnet_compiler.exe'
    & $slice8Precompiler -v / -p $slice8Output -f ($slice8Output+'-Precompiled')
    if($LASTEXITCODE -ne 0){throw 'ASP.NET page precompilation failed.'}
    Write-Output 'ASP.NET page precompilation succeeded.'
}
