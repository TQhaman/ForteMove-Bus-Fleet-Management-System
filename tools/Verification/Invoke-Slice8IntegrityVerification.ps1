param([string]$BaselineCommit='fd09a9b')
$ErrorActionPreference='Stop'
$slice8Root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location $slice8Root
try {
    $slice8Old=Get-ChildItem database/migrations -Filter '*.sql' | Where-Object {$_.Name -match '^000[0-7]_'} | Sort-Object Name
    if($slice8Old.Count -ne 8){throw 'Expected eight immutable previous migrations.'}
    foreach($slice8Migration in $slice8Old) {
        $slice8Path='database/migrations/'+$slice8Migration.Name
        $slice8Expected=& git -c "safe.directory=$slice8Root" rev-parse "${BaselineCommit}:$slice8Path"
        if($LASTEXITCODE -ne 0){throw 'Cannot inspect baseline migration.'}
        $slice8Expected=$slice8Expected.Trim()
        $slice8Actual=(& git -c "safe.directory=$slice8Root" hash-object "--path=$slice8Path" $slice8Path).Trim()
        if($slice8Actual -ne $slice8Expected){throw "Baseline migration changed: $slice8Path"}
    }
    $slice8Sql=New-Object System.Data.SqlClient.SqlConnection 'Data Source=.\SQLEXPRESS;Initial Catalog=ForteMove;Integrated Security=True'
    $slice8Sql.Open()
    try {
        $slice8Command=$slice8Sql.CreateCommand()
        $slice8Command.CommandText='SELECT MigrationName,ChecksumSha256 FROM dbo.SchemaMigrations;'
        $slice8Recorded=@{};$slice8Reader=$slice8Command.ExecuteReader()
        while($slice8Reader.Read()){$slice8Recorded[$slice8Reader.GetString(0)]=$slice8Reader.GetString(1)}
        $slice8Reader.Close()
        foreach($slice8Migration in (Get-ChildItem database/migrations -Filter '*.sql')) {
            $slice8Text=[IO.File]::ReadAllText($slice8Migration.FullName).Replace("`r`n","`n").Replace("`r","`n")
            $slice8Hash=[Security.Cryptography.SHA256]::Create()
            try {$slice8Digest=[BitConverter]::ToString($slice8Hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($slice8Text))).Replace('-','').ToLowerInvariant()}
            finally {$slice8Hash.Dispose()}
            if($slice8Recorded[$slice8Migration.Name] -ne $slice8Digest){throw "Database checksum mismatch: $($slice8Migration.Name)"}
        }
        $slice8Command.CommandText=@'
SELECT N'FuelStations',COUNT_BIG(*) FROM dbo.FuelStations
UNION ALL SELECT N'FuelRequests',COUNT_BIG(*) FROM dbo.FuelRequests
UNION ALL SELECT N'FuelVouchers',COUNT_BIG(*) FROM dbo.FuelVouchers
UNION ALL SELECT N'FuelTransactions',COUNT_BIG(*) FROM dbo.FuelTransactions;
SELECT N'FK',COUNT(*) FROM sys.foreign_keys
WHERE parent_object_id IN(OBJECT_ID(N'dbo.FuelStations'),OBJECT_ID(N'dbo.FuelStationCapabilities'),OBJECT_ID(N'dbo.FuelRequests'),OBJECT_ID(N'dbo.FuelVouchers'),OBJECT_ID(N'dbo.FuelTransactions')) AND (is_disabled=1 OR is_not_trusted=1)
UNION ALL SELECT N'CHECK',COUNT(*) FROM sys.check_constraints
WHERE parent_object_id IN(OBJECT_ID(N'dbo.FuelStations'),OBJECT_ID(N'dbo.FuelStationCapabilities'),OBJECT_ID(N'dbo.FuelRequests'),OBJECT_ID(N'dbo.FuelVouchers'),OBJECT_ID(N'dbo.FuelTransactions')) AND (is_disabled=1 OR is_not_trusted=1);
SELECT name,filter_definition FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.FuelRequests') AND name=N'UX_FuelRequests_PendingAssignment';
SELECT t.name,c.name,ty.name,c.precision,c.scale FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id
WHERE t.name IN(N'FuelVouchers',N'FuelTransactions') AND c.name IN(N'ApprovedQuantity',N'ApprovedAmount',N'Quantity',N'Amount',N'OdometerAtRedemption');
'@
        $slice8Reader=$slice8Command.ExecuteReader()
        do {while($slice8Reader.Read()){ $slice8Values=@();for($slice8Index=0;$slice8Index -lt $slice8Reader.FieldCount;$slice8Index++){$slice8Values+=[string]$slice8Reader.GetValue($slice8Index)};Write-Output ($slice8Values -join ' | ') }} while($slice8Reader.NextResult())
        $slice8Reader.Close()
    } finally {$slice8Sql.Dispose()}
    $slice8Violations=& rg -n 'AddWithValue' src
    if($LASTEXITCODE -eq 0){throw "AddWithValue found: $slice8Violations"}
    $slice8Violations=& rg -n 'System.Data.SqlClient|SqlConnection|SqlCommand|SqlDataReader|SqlTransaction' src/ForteMove.Business src/ForteMove.Web -g '*.cs'
    if($LASTEXITCODE -eq 0){throw "ADO.NET outside Data: $slice8Violations"}
    & git -c "safe.directory=$slice8Root" diff --check
    if($LASTEXITCODE -ne 0){throw 'Whitespace check failed.'}
    Write-Output 'Old migration Git objects unchanged; all nine live checksums match; architecture/whitespace scans pass.'
} finally {Pop-Location}
