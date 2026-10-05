# Slice 6 acceptance harnesses

These optional source files preserve the checks used during Slice 6 acceptance. They are outside the solution and are not run by the database initializer or ordinary builds.

- `Slice6BusinessVerification.cs` uses a fake repository and fixed clock. Its registration inputs are synthetic in-memory test fixtures, not login credentials or a default account. It checks balances, eligibility, idempotent operations and password-hashing output.
- `Slice6ReadOnlyIntegration.cs` reads a Windows-authenticated local SQL database. It assumes particular Trip, Schedule and account IDs and fixed service dates from the original acceptance dataset; a fresh clone's database will not contain those records.
- `Slice6DatabaseRollback.sql` checks database constraints with synthetic records inside a transaction that is rolled back. It requires an existing Trip.
- `Slice6RefundRollbackIntegration.cs` exercises refund persistence with synthetic transactional records and rolls them back. It requires existing Trip/administrator records and uses Windows authentication.

Review and adapt local instance names, record IDs and dates before use. Use a disposable test database. Rollback can still consume SQL identity values; these harnesses are not a way to populate demonstration data. The checkpoint task did not run these database harnesses against the accepted local database.

Compile C# harnesses separately with the .NET Framework compiler and the required built ForteMove assemblies; keep executables and copied assemblies outside Git. No real account passwords, exported local database, or personal data are needed to share these sources.

## Slice 7 verification

Build the solution first, then run the non-persisting Business checks:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Verification\Invoke-Slice7Verification.ps1
```

To include read-only integration and rollback SQL checks against the existing local `ForteMove` database:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Verification\Invoke-Slice7Verification.ps1 -IncludeDatabaseChecks
```

The process-only execution-policy option does not alter Windows policy. The harnesses use Windows authentication and no account passwords. Executables and copied assemblies go into ignored `tools/Verification/bin`.

- `Slice7BusinessVerification.cs`: 55 deterministic checks using synthetic in-memory geometry and a fixed clock. Covers operational states, merged pauses, resume, duration snapshots, Haversine/timing precedence, terminal behavior, unavailable results, culture-independent numeric JSON, audience projection, and coordinate validation. Nothing is persisted.
- `Slice7RollbackVerification.cs`: 27 checks exercising the production repository transaction cores via reflection, within one caller-owned serializable transaction. Requires an existing assigned unstarted Trip, two Drivers, two Passengers, and an Administrator. Temporary password-gate flags, Ticket, inspection, execution, delay, exception, coordinates, and audit entries are all rolled back. Checks ownership, revocation, active-Trip warning/acknowledgement, stale updates, old/new audit coordinates, existing SQL coordinate constraints, and assignment-lock serialization. SQL identity and database rowversion sequence counters can advance despite rollback; no rows or corrections remain.
- `Slice7ReadOnlyIntegration.cs` and its `.exe.config`: 32 read-only checks on the accepted local dataset, including Fleet/Route/Driver/Trip/Dashboard/Passenger/Defect mappings, tracking batch queries, and the real JSON handler. No login attempts or writes. The checks assume the accepted missing-coordinate baseline and assigned Trips on 6 October 2026; adapt those assertions for another dataset.
- `Slice7BrowserPreview.html` and `Start-Slice7BrowserPreview.ps1`: isolated renderer fixture at `http://localhost:55359/`. Uses synthetic snapshots, not authentication or operational actions, and is outside the production Web project. Start the existing HTTPS IIS Express site first because the fixture loads the actual local CSS, Leaflet, and tracking script from it. `?tiles=failure` deliberately requests nonexistent loopback tiles to verify graceful map-background failure. Stop the helper process when done. It serves only this fixture, never arbitrary repository files.

See [Slice 7 verification and manual workflow](../../SLICE_7_VERIFICATION.md) for observed results and the remaining authenticated operational acceptance walkthrough.
