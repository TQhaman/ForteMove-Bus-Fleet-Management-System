param([Parameter(Mandatory=$true)][string]$AssembliesDirectory)
$ErrorActionPreference='Stop'
$slice7Root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$slice7Web=Join-Path $slice7Root 'src/ForteMove.Web'
$slice7Output=Join-Path $PSScriptRoot ('bin/PageRender-'+[Guid]::NewGuid().ToString('N'))
$slice7Bin=Join-Path $slice7Output 'bin'
New-Item -ItemType Directory -Path $slice7Bin -Force | Out-Null
# Copy only the application into ignored verification output. The running site's files stay untouched.
foreach($slice7Item in @('Admin','Driver','Passenger','Controls','Tracking','Account','Errors','Content','Scripts','Web.config')) {
    Copy-Item -LiteralPath (Join-Path $slice7Web $slice7Item) -Destination $slice7Output -Recurse
}
$slice7References=@('/r:System.Data.dll','/r:System.Configuration.dll','/r:System.Web.dll')
foreach($slice7Assembly in @('Models','Business','Data','Web')) {
    $slice7Dll=Join-Path $AssembliesDirectory "ForteMove.$slice7Assembly.dll"
    Copy-Item -LiteralPath $slice7Dll -Destination $slice7Bin
    $slice7References+="/r:$slice7Dll"
}
$slice7Exe=Join-Path $slice7Bin 'Slice7PageRenderVerification.exe'
$slice7Compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $slice7Compiler /nologo "/out:$slice7Exe" @slice7References (Join-Path $PSScriptRoot 'Slice7PageRenderVerification.cs')
if($LASTEXITCODE -ne 0){throw 'Runtime verification compilation failed.'}
& $slice7Exe $slice7Output
if($LASTEXITCODE -ne 0){throw 'Runtime page rendering verification failed.'}
