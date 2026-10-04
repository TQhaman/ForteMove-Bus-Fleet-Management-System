# ForteMove developer handoff: accepted Slice 4

This describes the inspected implementation, not future functionality. No Codex installation is needed. Setup is in [COLLABORATOR_SETUP.md](COLLABORATOR_SETUP.md); proposed Driver Operations is in [SLICE_5_PLAN.md](SLICE_5_PLAN.md).

## Architecture and entry points

`ForteMove.sln` contains four classic C# projects targeting .NET Framework 4.8, C# 6. Project files explicitly enumerate source files; when adding a class or page, include it in the appropriate `.csproj`. Web Forms pages need their `.aspx`, `.aspx.cs`, and `.aspx.designer.cs` entries.

| Existing location | Responsibility / important classes |
| --- | --- |
| `src/ForteMove.Models` | Shared entities, request/result DTOs, enums; `Common/ServiceResult.cs` and `Common/ValidationError.cs` communicate expected validation failures. |
| `src/ForteMove.Business/Services` | `AuthenticationService`, `BusService`, `RouteService`, `SchedulingService`, `DriverService`, `AssignmentService`. Business rules live here, not in page handlers. |
| `src/ForteMove.Business/Contracts` | `IAuthenticationRepository`, `IBusRepository`, `IRouteRepository`, `IDashboardRepository`, `ISchedulingRepository`, `IDriverRepository`, `IAssignmentRepository`. These define persistence boundaries. |
| `src/ForteMove.Data/Repositories` | Corresponding `Sql*Repository` classes use parameterized ADO.NET and explicit transactions. |
| `src/ForteMove.Data/Internal/SqlAuditWriter.cs` | Writes `AuditEntries`, optionally using the same transaction as the business operation. |
| `src/ForteMove.Web/Infrastructure/ServiceFactory.cs` | Composition root: reads connection string `ForteMove`, creates each SQL repository, injects it into its service. No dependency-injection framework. |
| `src/ForteMove.Business/Time/IClock.cs`, `SystemClock.cs` | Injectable clock, UTC audit time, and South African operational dates/times. |
| `src/ForteMove.Business/Identifiers/IdentifierCodePolicy.cs` | Formats controlled identifiers. Sequence acquisition/uniqueness are also enforced in repositories. |

Presentation already lives in `src/ForteMove.Web/Admin` (fleet/dashboard), `Admin/Routes`, `Admin/Scheduling`, `Admin/Drivers`, and `Admin/Assignments`. Existing Driver presentation is `src/ForteMove.Web/Driver/Account.aspx`, `Driver.Master`, and `Web.config`. **There are no Driver Trip execution pages or repository operations yet.** Shared styling is `Content/fortemove.css`; Bootstrap CSS/JS is vendored under `Content/vendor` and `Scripts/vendor`.

## Database relationships

The migration files are the authoritative column/constraint definitions.

| Migration | Existing tables / purpose |
| --- | --- |
| `0000_SchemaMigrations.sql` | `SchemaMigrations`: immutable migration names, checksums and application timestamps. |
| `0001_IdentityAndAccess.sql` | `Roles`, `UserAccounts`, `StaffProfiles`, `AuditEntries`. |
| `0002_FleetFoundation.sql` | `BusCategories`, `PropulsionTypes`, `Buses`. `Buses.OdometerKilometres` is currently a registered value, not a reading-history ledger. |
| `0003_RoutesAndStops.sql` | `Routes`, `Stops`, `RouteStops`; renames bus VIN column. Ordered RouteStops determine origin/destination. |
| `0004_SchedulingAndTrips.sql` | `RouteSchedules`, `RouteScheduleVersions`, `ScheduleOperatingDays`, `ScheduleDepartureTimes`, `Trips`. |
| `0005_DriversAndAssignments.sql` | Adds `Buses.GrossVehicleMassKg`; creates `DriverProfiles` and `TripAssignments`. |

Identity chain: `Roles.RoleId -> UserAccounts.RoleId`; `StaffProfiles.UserAccountId` is a unique foreign key to `UserAccounts`; `DriverProfiles.StaffProfileId` is a unique foreign key to `StaffProfiles`. The UserAccount owns login/email, salted password material and account flags; StaffProfile owns employee number, names/contact and employment status; DriverProfile owns birth date, availability, licence and passenger PrDP. `PrincipalContext` contains the **UserAccountId**, not the DriverProfileId. Resolve it through `DriverService.GetDriverByUserAccount` / `SqlDriverRepository.GetDriverByUserAccount`; never accept a posted DriverProfileId as the authenticated identity.

Schedule chain: `Routes -> RouteSchedules -> RouteScheduleVersions -> Trips`. Pattern days/times belong to a version. Trips store their Route, service date, scheduled time, duration and expected-finish snapshots. Each `(RouteId, ServiceDate, ScheduledDepartureTime)` is unique.

`TripAssignments` links `TripId`, `DriverProfileId`, and `BusId`. The filtered unique index `UX_TripAssignments_CurrentTrip` permits one `IsCurrent=1` assignment per Trip. Change/remove operations end the old record with reason, actor and timestamp; they retain history. This is retained decision history, not strictly immutable rows: ending an assignment updates its end fields. The Trip has no separate current bus/Driver foreign keys; join through the current assignment.

## Current state machine

`TripStatus` in `Models/Scheduling/SchedulingModels.cs` and the SQL CHECK constraint already allow `Unassigned`, `Scheduled`, `Ready`, `InProgress`, `Delayed`, `Completed`, `Cancelled`.

- Schedule generation creates `Unassigned` Trips.
- Assignment confirmation changes `Unassigned -> Scheduled`, records operational-touch metadata and audits the decision.
- A current `Scheduled` assignment can be changed; status remains `Scheduled`.
- Removing a current `Scheduled` assignment returns it to `Unassigned`, retaining its assignment history and operational touch.
- Schedule changes replace only safe future Trips satisfying `ExistingScheduleTrip.IsUntouched`: Unassigned, no review flag, no operational touch, and no assignment history. Other affected future Trips are retained and marked `RequiresReview`.
- `Ready/InProgress/Delayed/Completed/Cancelled` are allowed values; Driver transitions to those states are **not implemented through Slice 4**. Do not mistake enum values for completed features.

`RequiresReview` is an orthogonal review flag. Buses persist only `BaseOperationalState` (`Operational`, `OutOfService`, `UnderMaintenance`, `Retired`); assignment/trip activity does not become a competing permanent bus state.

## Authentication and authorization

`Account/Login.aspx.cs` calls `AuthenticationService.Authenticate`. `Security/PasswordHasher.cs` uses PBKDF2-HMAC-SHA256, a random 32-byte salt, a 32-byte hash, and 600,000 iterations. `PasswordPolicy` accepts 15–128 characters for new passwords. Five failed logins trigger a 15-minute account lockout. Dummy verification material is used for timing resistance, not as an account credential.

Login issues a protected, nonpersistent Forms Authentication cookie containing the UserAccountId. Cookies require HTTPS and are HttpOnly with SameSite Lax. `Global.asax.cs` decrypts the ticket and reloads account/role state on each authenticated request; `ForteMovePrincipal` supplies role checks. New Driver creation sets `MustChangePassword=1`; protected requests route to `Account/ChangePassword.aspx`, which verifies the temporary password and stores a fresh salted hash.

Existing tickets are not bound to a password/security version, so a password change does not revoke other already-authenticated tickets. Account/role deactivation is checked on the next request. See the audit for this unimplemented session-invalidation follow-up; do not assume password change terminates every session.

`Admin/Web.config` plus `Infrastructure/AdminPage.cs` restrict administrators. `Driver/Web.config` plus `Infrastructure/DriverPage.cs` restrict Drivers. Base pages bind `ViewStateUserKey` to the authenticated user and disable caching. These establish role access, **not Trip ownership**. Future Driver services/repositories must enforce current-assignment ownership again inside each write transaction. Existing services assume their protected Web caller; they are not a safe generic API authorization layer for arbitrary callers.

## Assignment and concurrency conventions

`AssignmentService` ranks eligible buses by category/capacity fit and workload, then Drivers by workload/availability and route familiarity. Eligibility includes active identity/role/employment, availability, age/credentials, GVM/licence compatibility, vehicle compliance/capacity and a 15-minute resource turnaround. Recommendations are advisory.

`SqlAssignmentRepository.ConfirmAssignments` revalidates a selected batch atomically in a serializable transaction, acquires application lock `ForteMove.Assignments`, and checks Trip, bus, Driver, Staff and User rowversions. `ChangeAssignment` shares that lock and retains history. `RemoveAssignment` uses guarded state and rowversion checks. Do not rely on posted recommendation fingerprints or hidden fields as authorization.

SQL `ROWVERSION` is an opaque eight-byte concurrency token, **not a time or secret**. Include expected tokens in mutation DTOs and `WHERE` predicates, check affected-row counts, and reject stale submissions with a refresh message. Driver profile updates check all three identity/profile rowversions. `SqlBusRepository.UpdateBusEligibility` protects bus edits. Future Trip execution must coordinate with these writers to prevent reassign/readiness/start races; keep a consistent lock order and preserve the whole transaction on failures.

## Auditing, migrations, and time

Use `SqlAuditWriter.Write` with actor, event type, entity, id, bounded detail and UTC timestamp. Existing events include `DriverCreated`, `DriverUpdated`, `DriverAvailabilityChanged`, `BusUpdated`, `BusGvmUpdated`, `AssignmentConfirmed`, `AssignmentOverridden`, `AssignmentChanged`, `AssignmentRemoved`, `AssignmentReviewResolved`, and authentication/password events. Store an operational mutation and its audit entry in the same transaction. Do not put passwords, hashes/salts, cookies or unnecessary personal detail in audit text.

Never edit migrations `0000–0005`. Add the next sequential file under `database/migrations` (proposed Slice 5 would start at `0006`, subject to approval and other developers' work). Files contain no `GO`; the initializer executes each file and records its normalized UTF-8/LF SHA-256 checksum in one transaction. Foreign keys must preserve history; avoid cascading deletion of operational records. Extend scheduling's protected-trip logic when adding new operational dependencies, including defensive checks in `ReadExistingTrips`, `MarkProtectedTrips`, and `DeleteUntouchedTrips`.

`SystemClock` uses Windows time-zone id `South Africa Standard Time`. `OperationalNow`/`Today`, service dates, scheduled departure times and `ExpectedFinishLocal` are operational local time. Audit and operational-touch timestamps are UTC. Proposed actual start/completion/report times should be recorded in UTC and converted for display. Do not compare a local scheduled DateTime directly to UTC. `en-ZA` formatting is configured, but culture does not set a timezone. Existing Assignment Queue default date uses host `DateTime.Today`; use the injected operational clock for new business rules. Other `DateTime.Today.Add(time)` calls merely format `HH:mm`; Forms Authentication uses host-local ticket time.

## Where Slice 5 additions belong (proposals only)

Extend the existing Models, Business/Contracts, Business/Services, Data/Repositories and Driver directories above. Proposed new `DriverOperations` model group, `IDriverOperationsRepository`, `DriverOperationsService`, `SqlDriverOperationsRepository`, and Driver Trip pages do **not** exist yet. Wire them through the existing `ServiceFactory`, extend existing `Driver.Master` navigation, and register every new file in the relevant project. Keep checklist, ownership, time/state and odometer validation out of page-only logic. Use [the plan](SLICE_5_PLAN.md) before designing migration `0006`.
