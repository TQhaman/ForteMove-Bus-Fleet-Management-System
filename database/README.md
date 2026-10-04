# ForteMove database setup

ForteMove uses SQL Server and immutable, checksum-recorded migrations. The default local target is SQL Server Express at `.\SQLEXPRESS`, with a database named `ForteMove`.

## Prerequisites

- SQL Server Express (or another SQL Server instance) must be running.
- The current Windows account must be able to create the target database and create objects in it.
- Run the initializer with Windows PowerShell 5.1 or PowerShell 7 on Windows.

## Create the database and first administrator

From the repository root:

```powershell
.\tools\Initialize-ForteMoveDatabase.ps1
```

The script prompts for the first Transport Administrator's email, employee number, first name, last name, and password. There are no default credentials. The password is read as a `SecureString`, must be 15–128 characters long, and is stored as a 32-byte PBKDF2-HMAC-SHA256 digest with a unique 32-byte salt and 600,000 iterations.

To target another SQL Server instance or database:

```powershell
.\tools\Initialize-ForteMoveDatabase.ps1 `
    -ServerInstance '.\SQLEXPRESS' `
    -DatabaseName 'ForteMove'
```

Database names are deliberately restricted to a letter followed by letters, digits, or underscores. The initializer never drops a database. If the named database already exists, it is accepted only when it contains ForteMove's migration table and the recorded bootstrap checksum matches the current bootstrap migration.

## Verify migrations without seeding an identity

`-SkipAdministratorSeed` is for non-interactive schema verification and migration reruns only. It does not create credentials and is not an alternative production setup path.

```powershell
.\tools\Initialize-ForteMoveDatabase.ps1 -SkipAdministratorSeed
```

Use this switch for the second idempotency run after the administrator has been created. Applied migrations with matching checksums are skipped. If the contents of an applied migration have changed, initialization stops rather than silently accepting schema drift.

## Current schema migrations

- `0000_SchemaMigrations.sql` creates the checksum-protected migration ledger.
- `0001_IdentityAndAccess.sql` creates roles, accounts, staff profiles, and audit entries.
- `0002_FleetFoundation.sql` creates the fleet catalogues and bus register.
- `0003_RoutesAndStops.sql` standardizes the internal bus VIN column name and creates Routes, Stops, and ordered RouteStops.

Migration `0003` preserves existing bus rows while renaming `Buses.VinChassisNumber` to `Buses.Vin` and its unique constraint/index to `UQ_Buses_Vin`. It refuses to run against an unexpected or partially changed VIN schema.

Routes derive their origin and destination from their ordered RouteStops. Stops are reusable across routes, while unique constraints prevent duplicate stop order values and prevent the same Stop from appearing twice on one Route. Optional stop coordinates are stored as `DECIMAL(9,6)` values and must be supplied as a valid latitude/longitude pair.

The migration does not seed routes, stops, coordinates, distances, durations, or fares. The approved KuGompo City / East London prototype network remains manual acceptance-test data until its operational values are entered through the application. The approved list contains 14 unique stop names because Terminus Station is shared by two proposed routes.

## Migration policy

- Never edit an applied migration. Add a new, sequentially numbered migration instead.
- Migration files do not contain `GO`; each file is executed and recorded in one SQL transaction.
- Checksums use UTF-8 text with line endings normalized to LF, so Windows checkout line-ending changes do not create false drift.
- The initializer will not adopt an existing unmarked database or overwrite an existing Transport Administrator.
- No routes, stops, drivers, passengers, schedules, buses, or demo identities are seeded by these migrations.

If database creation succeeds but initialization is interrupted before `0000_SchemaMigrations.sql` is recorded, the next run treats the empty database as unknown and stops. Inspect it manually; the initializer will not drop or adopt it automatically.
