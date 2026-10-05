# Slice 9 — Maintenance verification

## Baseline and scope

Implementation was prepared on **Slice-9**, created directly from the accepted Slice 8 checkpoint **c65a0b6d0e435c0fa3a30e095f433c389b3c0129**. Its implementation checkpoint is on the new **Slice-9-completed** branch, preserving the existing Slice-9 branch. Browser acceptance remains documented separately; the checkpoint does not imply that the manual workflow below has been completed.

Four .NET Framework 4.8 projects, authentication, ADO.NET, audits, South African time and brand assets are preserved. Only **0009_MaintenanceManagement.sql** is added. Migrations 0000–0008 have unchanged Git objects and matching live normalized SHA-256 checksums. See [changed files](SLICE_9_CHANGED_FILES.md) and [manual acceptance](SLICE_9_MANUAL_TESTS.md).

Implemented: preventive Plans, derived due attention, Repair Providers, source-aware Work Orders, reviewed Start Maintenance, progress, verified completion, cancellation, separate Return to Service and vehicle-status history. Existing Driver defects/Cannot Proceed remain the intake.

## Rules and transaction boundaries

- Equality with a date/odometer threshold is Due, not Overdue. Combined plans use the most urgent result. Inactive plans are ignored.
- Only active blocking Overdue plans prevent new assignments, readiness/start, new Ticket purchases and Return. They do not independently block an already-started Trip's Resume/completion.
- Explicit first due does not create past service history. Verified initialization uses administrator evidence. Completed baselines cannot be replaced by invented service; interval edits recalculate from recorded completion.
- Creation never changes Bus/Trip status or resolves a defect. An Open linked defect requires review note and protected concurrency token.
- Start requires explicit impact confirmation, rechecking Bus/order versions, affected Trip/assignment versions, purchased-Ticket counts and pending/unused fuel IDs/versions. Changed impact is rejected/refreshed.
- Start rejects Retired buses, genuinely active unfinished Trips and another InProgress order. It sets UnderMaintenance, retains assignments/Tickets, flags unstarted assigned Trips for review and cancels pending Fuel Requests/unused Active Vouchers.
- Completion requires current Work Order/Bus/Plan tokens, work evidence and nondecreasing verified odometer. Overflow/scale failures are normal validation. Explicit defect resolution requires note/current token. Completion leaves the Bus UnderMaintenance.
- Open cancellation leaves Bus status unchanged; InProgress cancellation leaves it UnderMaintenance. Terminal orders cannot reopen.
- Return is a separate noted POST with fresh compliance, GVM/capacity/category, Critical-defect, blocking-plan and InProgress-work checks. Fleet Edit cannot bypass it or reactivate Retired vehicles.
- Keep-current assignment review now revalidates eligibility under locks. Completed/Cancelled execution history no longer permanently occupies resources.
- Tickets survive maintenance. Normal administrator Trip cancellation remains the refund path. Maintenance creates no FuelTransaction, automatic Trip transition or simulated movement.

Writes use Serializable, typed parameters, rowversions and UPDLOCK/HOLDLOCK. Application-lock order is Assignments → PassengerCommerce → FuelVouchers → Maintenance, skipping unused locks. Eligibility writes acquire Assignments first; provider-only writes use Maintenance. Time is captured after locks. Transaction-aware defect/fuel/status helpers share the owning connection/transaction.

## Executed results

Checks completed on 6 October 2026. These are automated results, not human browser acceptance.

| Check | Result |
|---|---|
| MSBuild Debug rebuild | Passed; no compilation warnings/errors reported |
| MSBuild Release rebuild | Passed; no compilation warnings/errors reported |
| Release ASP.NET precompilation | Passed in isolated application copy |
| Slice 9 Business | 44 checks passed |
| Slice 9 SQL rollback | 41 checks passed |
| Slice 9 disposable concurrency/lifecycle | 41 checks passed in recorded run |
| Populated maintenance/runtime/security pages | 104 checks passed |
| Accepted-database read-only runtime pages | 88 checks passed |
| Slice 8 Business/rollback/concurrency regression | 85 / 51 / 29 passed |
| Slice 7 Business/rollback/read-only regression | 55 / 27 / 32 passed |
| Migration/constraint/index/rowversion integrity | Passed |
| Previous migrations and brand Git-object checks | Passed |
| SQL/ADO.NET layer, AddWithValue, project entries, whitespace scans | Passed |

Concurrency counts can vary slightly by race winner; both valid outcomes are checked. Controlled clocks test date/expiry boundaries.

Business coverage: date/km/combined due boundaries, inactive/advisory plans, initialization provenance, immutable completed baseline, threshold overflow, odometer/cost precision/range, inclusive compliance, safety blockers, source rules, code width, assignment/passenger policies.

Rollback coverage: authorization, codes/stale tokens, same-Bus source validation/FK, malformed source/terminal SQL, atomic defect review, idempotency, duplicate active sources, linked-Plan edit prevention, stale impact, reviewed start, preserved assignments, progress, monotonic odometer, Plan reset, explicit defect resolution, terminal immutability and both cancellation paths.

Disposable coverage: duplicate/concurrent creation, Trip Start versus Maintenance Start, completion versus defect resolution, rollback on stale completion, Return expiry during review, keep-current revalidation, Ticket preservation/cancellation refunds, fuel cancellation with redeemed history retained, blocked request/approval/redemption, role/POST tampering, inactive providers, in-trip delay/resume/completion despite overdue service, and populated Open/InProgress/Completed pages.

Actual Web Forms are rendered through BuildManager in an isolated application copy using database-loaded principals. Base-page role gates and production repository authorization are exercised, along with existing QR/protected-token and portal regression checks. This does not emulate the full IIS Forms Authentication cookie pipeline or every browser POST/ViewState interaction.

## Migration and data preservation

0009 is applied to ForteMove; a second initializer checksum-skipped all ten migrations. New checks/FKs are enabled and trusted. Four filtered active-work unique indexes, three mutable rowversions, same-Bus relationships and DECIMAL(12,2)/(12,1) types were verified.

Final counts of RepairProviders, MaintenancePlans, MaintenanceWorkOrders, MaintenanceProgressEntries and BusVehicleStatusHistory are all **zero**. No maintenance/reference/test identities or operational fixtures are left in the accepted database.

During development, stricter NULL-source and terminal-metadata checks were identified after initial application of the new, uncommitted 0009. All five new tables were proved empty; only that empty Slice 9 development schema was transactionally rebuilt and 0009 reapplied. No user records or earlier migrations were removed/changed. The one-off guarded helper is ignored, not a setup procedure. The handed-off 0009 is now checksum-protected and must not be edited after acceptance/application elsewhere.

Rollback tests may advance identity/rowversion counters. Concurrency fixtures use uniquely named disposable databases; cleanup validates/removes only the runner-created database. Generated binaries/application copies are ignored under tools/Verification/bin. No accepted database is dropped.

Initializer warnings about intentionally skipped administrator seeding and Git LF/CRLF notices are expected, not compiler errors or migration drift.

## Reproduce

Run from the repository root with SQL Express running and authorized Windows SQL access. Build current assemblies first.

~~~powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' .\ForteMove.sln /t:Rebuild /p:Configuration=Debug /nologo /v:minimal
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' .\ForteMove.sln /t:Rebuild /p:Configuration=Release /nologo /v:minimal
.\tools\Initialize-ForteMoveDatabase.ps1 -SkipAdministratorSeed
.\tools\Verification\Invoke-Slice9Verification.ps1 -AssembliesDirectory .\src\ForteMove.Web\bin -IncludeDatabaseChecks -IncludeConcurrencyChecks
.\tools\Verification\Invoke-Slice9PageRenderVerification.ps1 -AssembliesDirectory .\src\ForteMove.Web\bin -Precompile
.\tools\Verification\Invoke-Slice9IntegrityVerification.ps1
.\tools\Verification\Invoke-Slice8Verification.ps1 -AssembliesDirectory .\src\ForteMove.Web\bin -IncludeDatabaseChecks -IncludeConcurrencyChecks
.\tools\Verification\Invoke-Slice7Verification.ps1 -IncludeDatabaseChecks
~~~

Accepted-database rollback tests require an active administrator and existing unstarted assigned Trip. Live page tests additionally require active Driver/Passenger accounts with first-login changes completed. They do not create missing accounts. Disposable concurrency tests create synthetic fixtures only in their own database.

## Outstanding acceptance and prototype limits

Complete [manual browser acceptance](SLICE_9_MANUAL_TESTS.md) for real HTTPS cookie login/logout, postbacks, confirmation refreshes, navigation and mobile feedback. Do not call that acceptance complete based solely on these harnesses.

Provider code/name and preventive thresholds are Work Order snapshots; contact details remain current references. Recorded workshop time differs from vehicle downtime until Return. Pre-Slice-9 downtime is unknown, not backfilled. Verified service odometer is evidence, not inferred distance. Cost is optional recording, not billing/procurement. Photos/uploads, provider accounts, external notifications and formal reports are deferred. Tracking retains the current-coordinate limitation and is not historical GPS evidence.
