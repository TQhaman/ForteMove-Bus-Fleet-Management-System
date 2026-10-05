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
- `0004_SchedulingAndTrips.sql` creates stable recurring Schedules, versioned operating patterns, generated Trips, and the constraints required for safe regeneration.
- `0005_DriversAndAssignments.sql` adds bus gross vehicle mass, Driver assignment credentials, and append-only Trip assignment history.
- `0006_DriverOperations.sql` adds pre-trip readiness, actual Trip execution, delay and Cannot Proceed histories, Driver defect reports, and auditable Trip status transitions.
- `0007_PassengerWalletAndTicketing.sql` adds Passenger profiles, one wallet per Passenger, an append-only wallet ledger, and whole-Route journey Tickets.

Migration `0003` preserves existing bus rows while renaming `Buses.VinChassisNumber` to `Buses.Vin` and its unique constraint/index to `UQ_Buses_Vin`. It refuses to run against an unexpected or partially changed VIN schema.

Routes derive their origin and destination from their ordered RouteStops. Stops are reusable across routes, while unique constraints prevent duplicate stop order values and prevent the same Stop from appearing twice on one Route. Optional stop coordinates are stored as `DECIMAL(9,6)` values and must be supplied as a valid latitude/longitude pair.

The migration does not seed routes, stops, coordinates, distances, durations, or fares. The approved KuGompo City / East London prototype network remains manual acceptance-test data until its operational values are entered through the application. The approved list contains 14 unique stop names because Terminus Station is shared by two proposed routes.

Migration `0004` does not seed Schedules or Trips. Operating days and departure times are normalized child rows. Each generated Trip is unique for its Route, service date, and departure time; stores its Route-duration and expected-finish snapshots; and begins with controlled status `Unassigned`. Schedule changes create a new internal version, preserve historical Trips, regenerate only safe untouched future Trips, and retain future Trips with operational dependencies for review.

Service dates and times are interpreted as South African local operational time. Audit timestamps and operational-touch timestamps use UTC. Schedule creation is limited to 366 inclusive calendar days and 10,000 synchronously generated Trips per version.

Migration `0005` does not seed Drivers, assignments, or vehicle mass values. Existing buses retain a nullable gross vehicle mass until an administrator records an approved value. New bus registration requires a positive GVM. `DriverProfiles` holds availability, date of birth, licence and passenger PrDP information; `TripAssignments` preserves every assignment decision and permits only one current assignment per Trip.

Assignment recommendations are advisory. The selected Driver, bus, Trip state, credentials, compliance, capacity, conflicts, rowversions, and 15-minute turnaround are revalidated in a serializable transaction before any assignment is committed. A selected batch either saves completely or rolls back completely.

Migration `0006` does not seed operational records. Readiness is bound to the exact current assignment and is invalidated on a safe pre-start reassignment, removal, or cancellation. Actual start/completion timestamps use UTC, while the application converts them to South African operational time for display. Starting and ending odometer readings remain immutable Trip evidence; completing a Trip atomically advances the Bus's authoritative odometer.

Delay, Cannot Proceed, defect, and Trip status records preserve operational history. Open or reviewed Critical defects block new assignments and Trip start/resume without silently changing the Bus's persistent status. Transport Administrators resolve exceptions, review/resolve defects, and are the only actors able to cancel a Trip.

Migration `0007` does not seed Passenger or commercial data. Passenger self-registration creates an active Passenger account, Passenger profile, and R0.00 wallet atomically. Wallet balances and immutable Ticket fare snapshots use `DECIMAL(12,2)`. Simulated top-ups, Ticket purchases, and Trip-cancellation refunds are recorded in the append-only wallet ledger with idempotency tokens.

Ticket sales are available only for an assigned, future, operationally eligible Trip. Sellable capacity comes from the currently assigned Bus. A successful purchase protects the Trip from destructive Schedule regeneration. Administrator cancellation refunds every purchased Ticket to its owning Passenger wallet in the same transaction; Driver completion does not imply boarding and does not change Ticket state.

## Migration policy

Slice 7 is schema-neutral: there is no migration `0008`. Simulated tracking reads existing ordered Stop coordinates and actual Trip execution, delay, and Cannot Proceed timestamps. It never persists positions or changes Trip state. The Stop coordinate editor updates the existing paired `DECIMAL(9,6)` fields with rowversion protection and a transactional `StopCoordinatesUpdated` audit containing old and new values. Corrections affecting started, unfinished Trips require an administrator warning/acknowledgement and are permitted.

There are no Route-geometry snapshots. Historical/Completed Trip rendering uses currently stored Stop coordinates, so later corrections also change those maps. Simulated tracking must not be treated as historical GPS evidence. Coordinates are not inferred or seeded.

- Never edit an applied migration. Add a new, sequentially numbered migration instead.
- Migration files do not contain `GO`; each file is executed and recorded in one SQL transaction.
- Checksums use UTF-8 text with line endings normalized to LF, so Windows checkout line-ending changes do not create false drift.
- The initializer will not adopt an existing unmarked database or overwrite an existing Transport Administrator.
- No routes, stops, drivers, passengers, schedules, buses, or demo identities are seeded by these migrations.

If database creation succeeds but initialization is interrupted before `0000_SchemaMigrations.sql` is recorded, the next run treats the empty database as unknown and stops. Inspect it manually; the initializer will not drop or adopt it automatically.
