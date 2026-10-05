# Accepted Slice 6 checkpoint â€” 5 October 2026

The owner manually accepted ForteMove through Slice 6. This checkpoint preserves that application without implementing Slice 7 or changing working application code. See [collaborator setup](COLLABORATOR_SETUP.md) for a fresh Windows development environment.

## History and branch policy

The initial checkout was `Slice-4` at `87249e70a5c7e3f7a5942e44ee728322f8b96981`, with 29 modified tracked files, 41 untracked files and no staged changes. The name did not describe the accepted application's scope.

| Commit | Actual contents |
| --- | --- |
| `61b5d2a` / `d8ed07e` | Authentication, fleet and Routes/Stops foundation; these original main/Slice-2 roots have identical source trees. |
| `b996515` | Slice 3 recurring schedules and generated Trips. |
| `176f778` | Slice 4 Drivers and intelligent Driver/Bus assignments, with collaborator/audit documentation. |
| `87249e7` | Slice 5 Driver Operations and migration `0006`, despite its Slice-4 branch and commit label. |
| `876a447` | Earlier main merge preserving the foundation histories; its tree equals `176f778`. |

After fetching, local `main` matched `origin/main` at `876a447`; there were no remote-only main commits. Main and Slice-4 had a common ancestor at `176f778`. Their differing histories therefore require a normal merge, not rewritten commits or a force-push.

The accepted dirty working tree is preserved in one truthful Slice-6 commit. The already committed Driver Operations remain in its ancestry; Passenger implementation and its cross-slice scheduling/assignment/cancellation integration are committed together. No artificial Slice 5 history is reconstructed.

- `main`: stable accepted system through Slice 6, merged normally with an explicit merge commit.
- `Slice-6`: frozen accepted checkpoint, including migration `0007`.
- `Slice-4`: preserved at its existing commit as historical work. It already contains Driver Operations; do not reset it to make its name appear accurate.
- `Slice-7`: starts from accepted main. This checkpoint adds no Slice 7 application behavior. Develop on this branch or agreed individual feature branches, then review before merging.

## Security, privacy and exclusions

The pre-staging audit inspected tracked, untracked and readable ignored files, fetched branch history, all 379 locally stored Git blobs (including unreachable objects), commit/tag data and 39 Git metadata files. It read 702 working-tree files. No actual passwords, tokens, private keys, credential-bearing connection strings, real account hash/salt exports, database exports or committed Visual Studio/build artifacts were found.

Password-related matches were runtime input variables, algorithm labels or synthetic verification fixtures. The in-memory verification password is not an account credential. SQL rollback harnesses use deliberately synthetic hash/salt bytes, not exported account hashes. Connection strings use Windows integrated authentication. No credential rotation or Git history remediation was indicated by the findings. Existing Git author names/email remain part of commit history; no history was rewritten to remove that metadata.

Five `.vs/ForteMove/FileContentIndex/*.vsidx` files remained locked even after a read-only retry, so their contents could not be audited. They are ignored local Visual Studio indexes, are not tracked and are not included in the checkpoint. This audit is a source-sharing check, not a claim that every local IDE cache was readable or that application security has been independently penetration-tested.

The existing `.gitignore` already excludes `.vs`, `bin`, `obj`, Visual Studio user files, restored packages, local SQL/database backups/exports, credential-bearing publish profiles, environment/secret files, private key containers, logs and temporary OS/editor workspaces. It was retained without unnecessary new exclusions. No inappropriate tracked file needed untracking; `.gitignore` alone would not have removed one.

Personal Windows paths were confined to ignored IDE/build artifacts. Those local files remain on the developer's PC. The SQL database, local IIS Express state and private development certificate are not transferred through Git. Audit scripts/reports and ASP.NET precompiled output were kept in temporary directories outside the repository.

## Baseline verification

- Migrations `0000`â€“`0007` exist. Migrations `0000`â€“`0006` match the initial HEAD using the initializer's normalized-line-ending convention and were not edited.
- Driver Operations models, services, repositories and Web Forms are present; migration `0006` is already committed.
- Passenger models, repository contract, registration/service logic, SQL repositories, public registration and role-isolated Passenger Web Forms are present.
- `ServiceFactory` retains authentication, fleet, routes, dashboard, scheduling, Driver and assignment registrations, plus `DriverOperationsService`, `OperationsAdminService`, `PassengerRegistrationService` and `PassengerService`.
- Every explicitly referenced source, content and project file exists.
- Visual Studio MSBuild Debug rebuild: passed.
- Visual Studio MSBuild Release rebuild: passed.
- .NET Framework ASP.NET precompilation of the complete web application: passed; output is outside Git.

No database migration or integration harness was run against the owner's accepted local database during checkpointing. Fresh-database setup uses the inspected initializer and all eight checked-in migrations; the existing manual acceptance remains the functional baseline. The required build/precompilation checks do not substitute for a new end-to-end acceptance run on another PC.

## Current implementation map

The four .NET Framework 4.8 projects still separate shared models, Business services/contracts, SQL persistence and Web Forms. `src/ForteMove.Web/Infrastructure/ServiceFactory.cs` wires services to parameterized ADO.NET repositories using the named `ForteMove` connection string.

| Area | Actual implementation locations |
| --- | --- |
| Driver Operations | `src/ForteMove.Models/Operations/DriverOperationsModels.cs`; `src/ForteMove.Business/Services/DriverOperationsService.cs`; `src/ForteMove.Business/Services/OperationsAdminService.cs`; `src/ForteMove.Data/Repositories/SqlTripOperationsRepository.cs`; `src/ForteMove.Web/Driver/`; `src/ForteMove.Web/Admin/Operations/`; migration `0006_DriverOperations.sql`. |
| Passenger models/contracts | `src/ForteMove.Models/Passengers/PassengerModels.cs`; `src/ForteMove.Business/Contracts/IPassengerRepository.cs`; `src/ForteMove.Business/Exceptions/PassengerPersistenceException.cs`. |
| Passenger Business rules | `src/ForteMove.Business/Services/PassengerRegistrationService.cs`; `src/ForteMove.Business/Services/PassengerService.cs`. |
| Passenger persistence | `src/ForteMove.Data/Repositories/SqlPassengerRepository.cs`; `src/ForteMove.Data/Internal/SqlPassengerCommerce.cs`. |
| Passenger UI/authorization | `src/ForteMove.Web/Account/RegisterPassenger.aspx` and code-behind/designer; `src/ForteMove.Web/Infrastructure/PassengerPage.cs`; `src/ForteMove.Web/Passenger/` including its Master, Home, Account, Wallet, Journeys, JourneyDetails, Tickets, TicketDetails and role-protecting `Web.config`. |
| Passenger schema | `0007_PassengerWalletAndTicketing.sql`: `PassengerProfiles`, `PassengerWallets`, `Tickets`, `WalletTransactions`, linked to `UserAccounts` and `Trips`. |
| Cross-slice integration | `AssignmentService`/`SqlAssignmentRepository` preserve capacity for purchased Tickets; `SchedulingService`/`SqlSchedulingRepository` protect Ticket history during schedule changes; assignment and operations cancellation paths use `SqlPassengerCommerce` to refund Tickets in the cancellation transaction. |

Passenger registration atomically creates an account, profile and R0.00 wallet. Passenger identities do not require a staff/Driver profile. Wallet top-ups are simulated, purchases snapshot fare and debit the wallet, and administrator cancellation retains/refunds Tickets. Driver completion does not redeem Tickets. The wallet ledger is append-only, monetary values are decimal, and operation tokens/concurrency checks protect repeated or stale submissions.

Existing Driver rules remain: only the assigned Driver operates a Trip; readiness is bound to the current assignment; start is limited to five minutes early; actual times use UTC with South African operational display; completion advances the Bus odometer atomically. Cannot Proceed requires administrator resolution, Critical defects block relevant new assignments/start/resume, and late departures produce warnings without automatic starts or cancellations.

## Portability and collaborator limits

All project references are relative, all projects target .NET Framework 4.8, and there are no NuGet/npm restore dependencies beyond framework tools and checked-in web assets. Collaborators need the VS 2022 web tools/IIS Express, Framework 4.8 targeting support, Windows PowerShell and their own SQL Server Express 2017+ instance. Local defaults `.\SQLEXPRESS`, database `ForteMove` and HTTPS port `44300` may need local adjustment. The development certificate must be configured locally.

`tools/Verification` contains source harnesses, not private database exports. Some assume original local acceptance IDs/dates and are not portable automatic tests on an empty database; [their notes](tools/Verification/README.md) explain the distinction. No demo seed or documented login password was added.

The earlier `REPOSITORY_AUDIT.md`, `BRANCH_WORKFLOW.md`, `DEVELOPER_HANDOFF.md` and `SLICE_5_PLAN.md` describe an earlier Slice 4 checkpoint/planning stage. Use this document, the updated README, collaborator guide and database guide for current scope. Optional GitHub branch protection/rulesets can require review on main and protect frozen checkpoints before granting Write access.

## Accepted uncommitted file inventory

The following files were modified or new at the start of this task. The original two README changes and four verification sources are legitimate documentation/tooling; the remaining changes are Passenger implementation and cross-slice integration. No unrelated tracked modifications were found. The collaborator/checkpoint/harness documentation added by this audit is separate from this initial inventory.

Modified tracked files:

```text
README.md
database/README.md
src/ForteMove.Business/ForteMove.Business.csproj
src/ForteMove.Business/Identifiers/IdentifierCodePolicy.cs
src/ForteMove.Business/Services/AssignmentService.cs
src/ForteMove.Business/Services/SchedulingService.cs
src/ForteMove.Data/ForteMove.Data.csproj
src/ForteMove.Data/Repositories/SqlAssignmentRepository.cs
src/ForteMove.Data/Repositories/SqlAuthenticationRepository.cs
src/ForteMove.Data/Repositories/SqlSchedulingRepository.cs
src/ForteMove.Data/Repositories/SqlTripOperationsRepository.cs
src/ForteMove.Models/Assignments/AssignmentModels.cs
src/ForteMove.Models/ForteMove.Models.csproj
src/ForteMove.Models/Scheduling/SchedulingModels.cs
src/ForteMove.Web/Account/ChangePassword.aspx.cs
src/ForteMove.Web/Account/Login.aspx
src/ForteMove.Web/Account/Login.aspx.cs
src/ForteMove.Web/Account/Login.aspx.designer.cs
src/ForteMove.Web/Admin/Assignments/AssignmentDetails.aspx
src/ForteMove.Web/Admin/Assignments/AssignmentDetails.aspx.cs
src/ForteMove.Web/Admin/Assignments/AssignmentDetails.aspx.designer.cs
src/ForteMove.Web/Admin/Scheduling/ChangeSchedule.aspx
src/ForteMove.Web/Admin/Scheduling/ChangeSchedule.aspx.cs
src/ForteMove.Web/Admin/Scheduling/ChangeSchedule.aspx.designer.cs
src/ForteMove.Web/Content/fortemove.css
src/ForteMove.Web/Default.aspx.cs
src/ForteMove.Web/ForteMove.Web.csproj
src/ForteMove.Web/Infrastructure/SafeRedirects.cs
src/ForteMove.Web/Infrastructure/ServiceFactory.cs
```

New files:

```text
database/migrations/0007_PassengerWalletAndTicketing.sql
src/ForteMove.Business/Contracts/IPassengerRepository.cs
src/ForteMove.Business/Exceptions/PassengerPersistenceException.cs
src/ForteMove.Business/Services/PassengerRegistrationService.cs
src/ForteMove.Business/Services/PassengerService.cs
src/ForteMove.Data/Internal/SqlPassengerCommerce.cs
src/ForteMove.Data/Repositories/SqlPassengerRepository.cs
src/ForteMove.Models/Passengers/PassengerModels.cs
src/ForteMove.Web/Account/RegisterPassenger.aspx
src/ForteMove.Web/Account/RegisterPassenger.aspx.cs
src/ForteMove.Web/Account/RegisterPassenger.aspx.designer.cs
src/ForteMove.Web/Infrastructure/PassengerPage.cs
src/ForteMove.Web/Passenger/Account.aspx
src/ForteMove.Web/Passenger/Account.aspx.cs
src/ForteMove.Web/Passenger/Account.aspx.designer.cs
src/ForteMove.Web/Passenger/Home.aspx
src/ForteMove.Web/Passenger/Home.aspx.cs
src/ForteMove.Web/Passenger/Home.aspx.designer.cs
src/ForteMove.Web/Passenger/JourneyDetails.aspx
src/ForteMove.Web/Passenger/JourneyDetails.aspx.cs
src/ForteMove.Web/Passenger/JourneyDetails.aspx.designer.cs
src/ForteMove.Web/Passenger/Journeys.aspx
src/ForteMove.Web/Passenger/Journeys.aspx.cs
src/ForteMove.Web/Passenger/Journeys.aspx.designer.cs
src/ForteMove.Web/Passenger/Passenger.Master
src/ForteMove.Web/Passenger/Passenger.Master.cs
src/ForteMove.Web/Passenger/Passenger.Master.designer.cs
src/ForteMove.Web/Passenger/TicketDetails.aspx
src/ForteMove.Web/Passenger/TicketDetails.aspx.cs
src/ForteMove.Web/Passenger/TicketDetails.aspx.designer.cs
src/ForteMove.Web/Passenger/Tickets.aspx
src/ForteMove.Web/Passenger/Tickets.aspx.cs
src/ForteMove.Web/Passenger/Tickets.aspx.designer.cs
src/ForteMove.Web/Passenger/Wallet.aspx
src/ForteMove.Web/Passenger/Wallet.aspx.cs
src/ForteMove.Web/Passenger/Wallet.aspx.designer.cs
src/ForteMove.Web/Passenger/Web.config
tools/Verification/Slice6BusinessVerification.cs
tools/Verification/Slice6DatabaseRollback.sql
tools/Verification/Slice6ReadOnlyIntegration.cs
tools/Verification/Slice6RefundRollbackIntegration.cs
```
