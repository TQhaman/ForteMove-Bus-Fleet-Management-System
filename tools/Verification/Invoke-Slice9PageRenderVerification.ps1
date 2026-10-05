param([Parameter(Mandatory=$true)][string]$AssembliesDirectory,[switch]$Precompile,[string]$DatabaseName)
$ErrorActionPreference='Stop'
$slice9Root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$slice9Web=Join-Path $slice9Root 'src/ForteMove.Web'
$slice9Output=Join-Path $PSScriptRoot ('bin/Slice9Pages-'+[Guid]::NewGuid().ToString('N'))
$slice9Bin=Join-Path $slice9Output 'bin'
New-Item -ItemType Directory -Path $slice9Bin -Force | Out-Null
foreach($slice9Item in @('Admin','Driver','Passenger','Controls','Tracking','Account','Errors','Content','Scripts','Web.config','Default.aspx','Global.asax')) {
    Copy-Item -LiteralPath (Join-Path $slice9Web $slice9Item) -Destination $slice9Output -Recurse
}
if($DatabaseName) {
    if($DatabaseName -notmatch '^ForteMove_Slice9_Verification_[a-f0-9]{32}$'){throw 'Use only a runner-owned disposable database for populated-page verification.'}
    # Change only the generated isolated application copy, never production configuration.
    $slice9ConfigPath=Join-Path $slice9Output 'Web.config'
    [xml]$slice9Config=Get-Content -LiteralPath $slice9ConfigPath -Raw
    $slice9ConnectionNode=$slice9Config.configuration.connectionStrings.add | Where-Object {$_.name -eq 'ForteMove'}
    $slice9ConnectionNode.connectionString="Data Source=.\SQLEXPRESS;Initial Catalog=$DatabaseName;Integrated Security=True"
    $slice9Config.Save($slice9ConfigPath)
}
Get-ChildItem -LiteralPath $AssembliesDirectory -Filter '*.dll' | Copy-Item -Destination $slice9Bin
$slice9References=@('/r:System.Data.dll','/r:System.Configuration.dll','/r:System.Web.dll')
foreach($slice9Assembly in @('Models','Business','Data','Web')) { $slice9References+="/r:$(Join-Path $slice9Bin "ForteMove.$slice9Assembly.dll")" }
$slice9Compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$slice9Exe=Join-Path $slice9Bin 'Slice9PageRenderVerification.exe'
& $slice9Compiler /nologo "/out:$slice9Exe" @slice9References (Join-Path $PSScriptRoot 'Slice9PageRenderVerification.cs')
if($LASTEXITCODE -ne 0){throw 'Runtime verification compilation failed.'}
& $slice9Exe $slice9Output
if($LASTEXITCODE -ne 0){throw 'Runtime page verification failed.'}
if($Precompile) {
    $slice9Precompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/aspnet_compiler.exe'
    & $slice9Precompiler -v / -p $slice9Output -f ($slice9Output+'-Precompiled')
    if($LASTEXITCODE -ne 0){throw 'ASP.NET page precompilation failed.'}
    Write-Output 'ASP.NET page precompilation succeeded.'
}


