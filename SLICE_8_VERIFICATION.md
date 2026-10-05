# Slice 8 — Fuel Voucher implementation and verification

## Delivered and checkpoint

The accepted Slice 7 checkpoint is `fd09a9b1a2a29f85fb3d2eb9b0a934a5b4cc3598`. The separately authorized Slice 7 runtime fixes were committed before implementation. Local `main` and `Slice-7` retain that checkpoint. Slice 8 was developed on `Slice-8`; its implementation checkpoint is on the new `Slice-8-completed` branch, preserving the existing `Slice-8` branch. The manual acceptance workflow and limitations remain documented below.

The four .NET Framework 4.8 projects and existing authentication, page bases, audit, migration, error handling, tracking, Passenger refunds and logo are preserved. There is no fourth authenticated actor or real fuel/payment integration.

Delivered workflow:

Driver's assigned Trip → reason-only Fuel Request → Administrator approval/rejection → one-use Fuel Voucher → owning Driver's local QR/token → Administrator-only Prototype Fuel Station Terminal → one immutable Fuel Transaction.

## Actual baseline and deliberate limits

The seven accepted fleet vehicles are Diesel. The catalogue also contains Petrol, Hybrid and Electric, but no accepted Electric vehicle exists. Nothing is inferred from make/category. Diesel uses litres; Electric uses kWh. Petrol/Hybrid/unknown propulsion is explicitly unsupported.

No Fuel Stations, Requests, Vouchers or Transactions have been seeded. All four tables were empty after verification. No accepted Bus, Driver, Schedule or Trip was invented for tests. The Electric SQL case changes only a rollback fixture. Electric browser acceptance requires a separately approved Electric vehicle and assignment.

The existing system has five Drivers, seven buses, two Routes, ten Stops, one Schedule with two internal versions, 210 Trips and four current assignments at inspection. Test identities and operational records used for committed concurrency actions exist only in a uniquely named disposable database, never in `ForteMove`. Those databases were removed by their runner.

## Rules implemented

- Request ownership, Driver/Bus/supply and exact assignment context are resolved server-side. Driver enters only the reason.
- Allowed assigned nonterminal states: Scheduled, Ready, In Progress and Delayed. Cannot Proceed and Critical defects do not suppress request submission.
- One Pending request per assignment; an unexpired Active Voucher also prevents another request. Rejected/cancelled/redeemed/expired history remains.
- Approval quantity must be positive, have at most two decimals, fit `DECIMAL(10,2)`, and not exceed known applicable capacity. Capacity is not a fabricated current tank/charge level.
- Simulated Rand amount is nonnegative, including R0.00, with two decimals and `DECIMAL(12,2)` support.
- Station must be active and compatible. Administrator may shorten validity, not exceed expected finish plus two hours. Validity must be future on approval; expiry is derived strictly after the deadline.
- Voucher snapshots retain supply/unit, authorized quantity/value, fleet number and Station description.
- Redemption uses current authoritative ownership/assignment, Trip state, Station/capability, expiry and Critical safety facts. It never asks for replacement quantities/value.
- Open/Reviewed Critical defects block redemption. Cannot Proceed does not block it and is not resolved by it.
- Bus status/compliance produce warnings at redemption, not a new hard block. Fuel never authorizes operation or changes Bus status.
- Each Voucher has a unique transaction constraint. Replayed confirmation returns the existing transaction; no second consumption/audit is created.
- Reassignment/removal/completion/cancellation invalidates Pending requests and Active vouchers in the existing operation transaction. Approved request history remains Approved; redeemed transactions are untouched.
- Requests set canonical Trip touch. All request history independently protects Schedule regeneration, including rejected/cancelled history and absent legacy touch metadata.
- Redemption takes the freshly locked Bus odometer snapshot and never updates the Bus odometer.
- No automatic Trip state changes, tracking side effects, Passenger actions, tariffs, invoice/provider ERP, external QR API, camera scanning or anonymous terminal were added.

Canonical operational lock order remains Assignment → PassengerCommerce where needed → FuelVouchers. Scheduling retains its reference-code-lock prefix. Fuel Station maintenance needs only the Fuel lock and never acquires Assignment afterward. Transactions are serializable and use typed parameters, rowversions and row/range locks.

## Migration and package setup

Only `0008_FuelVouchers.sql` was added. `0000`–`0007` retain their baseline Git objects and recorded checksums.

The local `ForteMove` database already has `0008` applied. The initializer was rerun and safely skipped all nine matching migrations:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Initialize-ForteMoveDatabase.ps1 -SkipAdministratorSeed
```

For another collaborator's existing accepted database, run the same command with the correct approved server/database arguments. Never edit an applied migration. A fresh installation still requires the normal securely prompted administrator setup without `-SkipAdministratorSeed`.

Restore QRCoder 1.8.0 through Visual Studio's Restore NuGet Packages or:

```powershell
nuget restore ForteMove.sln
```

It is pinned in Web's `packages.config`, references the compatible `net40` assembly, and generates PNG QR images locally. Package/build outputs are ignored, not source-controlled.

Tokens contain 32 cryptographically random bytes with an opaque versioned encoding. SQL stores SHA-256 lookup hashes plus a MachineKey-protected copy only for the owning Driver's display. No raw token appears in URLs or audits. The terminal carries its review token only in encrypted, user-bound ViewState. Retain secure application protection keys across deployments; web farms need consistently configured keys outside Git. Losing/rotating keys can make existing QR display unavailable rather than silently issuing replacement authorization.

## Executed verification

Observed on 5 October 2026:

| Check | Result |
|---|---|
| Visual Studio MSBuild Debug rebuild | Passed, 0 warnings / 0 errors |
| Visual Studio MSBuild Release rebuild | Passed, 0 warnings / 0 errors |
| ASP.NET Framework 4.8 page precompilation | Passed, no warnings/errors reported |
| Deterministic Slice 8 Business harness | 85 checks passed |
| Existing-database rollback harness | 51 checks passed |
| Disposable-database concurrency/lifecycle harness | 29 checks passed |
| Actual Web Forms rendering/security/QR harness | 62 checks passed |
| Slice 7 Business regression against current assemblies | 55 assertions passed |
| Slice 6 wallet Business regression against current assemblies | 24 assertions passed |
| Old migration Git-object / live checksum verification | Passed, all nine checksums match |
| New Foreign Key / CHECK constraints | Enabled and trusted |
| SQL/ADO.NET outside Data, AddWithValue, whitespace scans | No violations |
| Accepted Fuel table counts after checks | 0 Stations / 0 Requests / 0 Vouchers / 0 Transactions |

The rollback harness exercises production transaction cores within one caller-owned transaction: ownership/role rejection, idempotent submission, Pending/open-Voucher restrictions, stale approval/review, inactive Station, capacity, zero money, expiry, forged token, Critical Open/Reviewed/Resolved behavior, Cannot Proceed preservation, snapshot preservation, fresh odometer, one-use replay, Electric mapping, cancellation history and Schedule dependency classification. SQL identities and global rowversion counters can advance despite rollback; no fixture rows or reference corrections remain.

The disposable harness invokes real production repositories on synthetic data. It verifies concurrent Pending submission, approval, double redemption, rejection/retry, failed-reassignment rollback, successful reassignment/removal invalidation, cancellation with Passenger refund, completion with odometer update and unused Voucher invalidation, cancellation/redemption race and Critical-defect/redemption race. A serialized race may legitimately redeem first and retain that historical transaction, or see cancellation/defect first and reject; it cannot produce contradictory state.

The Web harness renders all sixteen new Fuel pages (empty or unknown-ID states) and existing Dashboard, Fleet, Routes, Trips, Assignments, Defects, Driver and Passenger pages. It exercises production base-page role gates, QR role/ownership responses, MachineKey round-trip/tamper rejection, and local PNG generation. It runs in an isolated copy, leaving the site's DLLs untouched. SimpleWorkerRequest is not the full IIS cookie/session pipeline; **interactive sign-in, populated form postbacks, QR scanner decoding and the complete browser workflow still require the manual acceptance below**. No application login bypass or testing action was introduced.

The first harness attempts needed isolated session-state setup and correct base-page lifecycle handling. These were corrected in the test harness; final tests pass without modifying the accepted authorization bases.

## Rerunning verification

Build first; paths below assume the usual Web `bin` output:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Verification\Invoke-Slice8Verification.ps1 -AssembliesDirectory "$PWD\src\ForteMove.Web\bin"
```

To include rollback and true concurrency/lifecycle checks:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Verification\Invoke-Slice8Verification.ps1 -AssembliesDirectory "$PWD\src\ForteMove.Web\bin" -IncludeDatabaseChecks -IncludeConcurrencyChecks
```

The existing-database rollback harness requires an assigned Scheduled Trip, two Drivers and an active Administrator. The disposable harness requires SQL database-create permissions. It creates only a generated `ForteMove_Slice8_Verification_<guid>` database, uses synthetic non-login credentials and removes that exact database afterward. Never adapt its cleanup to an accepted database name.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Verification\Invoke-Slice8PageRenderVerification.ps1 -AssembliesDirectory "$PWD\src\ForteMove.Web\bin" -Precompile
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Verification\Invoke-Slice8IntegrityVerification.ps1
```

Runtime rendering requires active Administrator, Driver and Passenger principals with completed first-login password change. It reads SQL using Windows authentication and does not know account passwords. All compiled/copied artifacts remain under ignored verification `bin`.

## Minimal manual happy path

1. Stop debugging, restore packages, **Rebuild Solution** in Visual Studio, then start ForteMove.Web at its existing HTTPS IIS Express URL. Verification builds used isolated output, so rebuild your normal startup project before testing.
2. Sign in as Transport Administrator. Open **Fuel → Stations → Create Station**. Enter your approved fictional prototype Station name/area, enable Diesel (or both supported supplies), keep it active and Save. Confirm automatic FS code and success feedback.
3. In a separate browser profile/private session, sign in as an existing Driver whose current assigned Trip is nonterminal. Complete a required first-login password change normally if prompted.
4. Open **Fuel → Request fuel** (also available from Trip Details). Select that Driver's eligible Trip, read the derived Bus/propulsion/capacity, enter only an operational reason, and Submit. Confirm the generated FR code and Pending state.
5. Return to Administrator **Fuel → Requests**. Open the request. Review Driver/Trip/Bus, request and current odometer, rated capacity and safety warnings. Enter your approved simulated quantity within the capacity, a nonnegative Rand amount, compatible Station and valid deadline no later than expected finish plus two hours. Approve.
6. Confirm one FV code and Active state. Return to the owning Driver **Fuel → Active Vouchers**, open it and confirm quantity/unit/value, Station and deadline. The QR and private copyable token should appear.
7. As Administrator, open **Fuel → Prototype Terminal**. Select exactly that approved Station, paste/type the Driver's token, then **Review Voucher**. Verify authoritative details and warnings; no quantity/value entry is offered.
8. Select **Confirm redemption**. Confirm one FTX code, redemption time and odometer snapshot. Driver refresh should show Redeemed and no usable QR/token.
9. Inspect **Fuel → Transactions**, search by transaction/fleet/Station and open details. Compare Bus odometer/status before and after; fuel redemption alone must not change either.
10. Log out and verify protected Fuel pages require authentication. Passenger must have no Fuel access; another Driver must not see the owner's details or QR.

Choose a Trip/Bus with no unresolved Critical defect for this happy path. Do not resolve genuine safety records merely to force a test. If expected finish plus two hours has already passed, use another approved current nonterminal assignment; the application does not extend authorization indefinitely.

## Focused negative and lifecycle acceptance

Use approved test actions/data; changes to real assignments or cancellation remain real operations, not harmless preview actions.

- Submit twice for the same assignment: one Pending request only. While its Active Voucher remains unexpired, another request is rejected.
- Reject with a blank reason: validation. Reject with a reason: Driver can see it and may make a fresh legitimate request.
- Quantity 0, negative or above capacity; negative amount; incompatible/inactive Station; deadline past or beyond maximum: friendly rejection. R0.00 authorization remains valid.
- Change/deactivate the Station between terminal Review and Confirm: stale/compatibility failure; no transaction. Review again after legitimate correction.
- Wrong token/Station, expired/cancelled/redeemed Voucher: readable rejection and no new transaction. Repeated confirmation must never consume twice.
- An open Cannot Proceed may coexist with request/approval/redemption. It remains open until the Administrator resolves it separately. Critical Open and Reviewed defects block redemption; resolving the defect removes only that block, not Bus status.
- Before redemption, safely change/remove the assignment: Active Voucher cancelled with reason; no transfer to replacement Bus/Driver. Pending requests also cancel.
- Complete or cancel the Trip normally: unused Active Voucher/Pending request invalidated, no Fuel transaction invented. Existing redeemed history stays. Cancellation still performs existing Ticket refunds in its same transaction.
- Apply a legitimate future Schedule change: a Trip with any Fuel request history remains protected for review, never destructively regenerated.
- If an approved Electric vehicle/assignment is added separately, repeat with an Electric-compatible Station and confirm kWh and battery-capacity validation. No Electric demo vehicle was created.

## Files and project changes

- Database: new `database/migrations/0008_FuelVouchers.sql`; updated database documentation.
- Models: new Fuel enums, DTOs and request models; `ExistingScheduleTrip.HasFuelHistory`.
- Business: Fuel repository/Station/token-protector contracts, focused persistence exception, policy/token utilities, Driver/Admin Fuel and Station services; FR/FV/FTX/FS identifier formatting.
- Data: Fuel and Station repositories plus transaction-aware lifecycle helper; targeted assignment, operational completion/cancellation and Schedule-protection integrations.
- Web: ten Admin Fuel pages and six Driver Fuel pages, each with code-behind/designer; Driver QR handler; MachineKey protection, local QR renderer and presentation helpers; service composition, navigation, Trip Details action and responsive CSS.
- Projects: explicit classic project entries and pinned QR package configuration.
- Verification: Business, rollback, concurrency and runtime sources plus three runners; root/database/verification documentation.
- No files were deleted. Logo assets, old migrations, accepted authentication bases and Passenger/tracking implementation are unchanged except the stated lifecycle/Schedule integrations.

See `SLICE_8_FILES_CHANGED.txt` for the complete path-by-path worktree manifest.
