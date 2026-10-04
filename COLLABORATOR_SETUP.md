# ForteMove collaborator setup (accepted through Slice 4)

Start development on a feature branch based on the owner's published, accepted `main`. The Slice 4 checkpoint and its merge were prepared locally on 4 October 2026; until the owner explicitly publishes them, GitHub may still contain an older `main`. Confirm publication before starting. Read [the branch workflow](BRANCH_WORKFLOW.md), [the developer handoff](DEVELOPER_HANDOFF.md), and [the preliminary Slice 5 plan](SLICE_5_PLAN.md).

## Prerequisites

- Windows, Git, and your own GitHub account with repository access.
- Visual Studio 2022 with **ASP.NET and web development**, classic .NET Framework web project tools, MSBuild, and IIS Express. **.NET desktop development** is useful for the class-library projects; ensure the .NET Framework tools are installed even if using only the web workload.
- **.NET Framework 4.8 Developer Pack / targeting pack and SDK**. Installing only a modern .NET SDK or the Framework runtime is insufficient to build these projects. See [Microsoft's developer-pack guidance](https://learn.microsoft.com/en-us/dotnet/framework/install/guide-for-developers).
- SQL Server Express **2017 or later**, installed and running with Windows authentication. Scheduling queries use ordered `STRING_AGG`; the database compatibility level must be at least 110. See [Microsoft's SQL requirements](https://learn.microsoft.com/en-us/sql/t-sql/functions/string-agg-transact-sql?view=sql-server-2017).
- SSMS is recommended for verifying the instance, database, and migration ledger.
- Windows PowerShell **5.1** is the simplest setup path. The existing database guide also supports PowerShell 7 on Windows; this audit did not independently test that runtime.
- Your Windows account needs permission to create its new database and schema. IIS Express normally uses that same account. Do not share the owner's SQL administrator credentials.

## Setup

1. Clone the repository into your own directory:

   ```powershell
   git clone https://github.com/TQhaman/ForteMove-Bus-Fleet-Management-System.git
   cd ForteMove-Bus-Fleet-Management-System
   git fetch origin
   git switch main
   git pull --ff-only origin main
   ```

2. Create the agreed collaborator branch from updated `main`:

   ```powershell
   git switch -c Slice-5-driver-operations
   ```

   If the owner has already published that branch, use `git switch --track origin/Slice-5-driver-operations` instead. Do not create a competing branch with the same name. Do not develop directly on `main` or the frozen `Slice-4` checkpoint.

3. Open **ForteMove.sln** in Visual Studio 2022.
4. Use Restore NuGet Packages if Visual Studio offers it, then **Build Solution**. There are currently no `PackageReference`, `packages.config`, npm, or external package-restore dependencies. The four projects use .NET Framework assemblies, relative project references, and checked-in Bootstrap 5.2.3 assets. Missing `Microsoft.WebApplication.targets` means the web tools/workload are missing; missing Framework reference assemblies means the targeting pack is missing. From a VS Developer PowerShell prompt, `MSBuild.exe ForteMove.sln /t:Build /p:Configuration=Debug` is also valid.
5. Confirm the SQL instance in SSMS using Windows authentication. The default is `.\SQLEXPRESS`, database `ForteMove`; a default unnamed instance may instead be `.`. Verify that the intended database name is **new on your own PC**. Do not run setup against the original developer's database.
6. From the repository root, run:

   ```powershell
   .\tools\Initialize-ForteMoveDatabase.ps1 -ServerInstance '.\SQLEXPRESS' -DatabaseName 'ForteMove'
   ```

   If PowerShell blocks a downloaded script, review it first and follow your machine's script policy; do not disable execution policy globally. Use a different fresh name such as `ForteMove_Dev` if `ForteMove` already exists. The initializer refuses to adopt an unrelated database, never drops a database, and checks applied migration checksums.
7. Interactively create **your own first Transport Administrator**: email, employee number, names, and a private 15–128 character password. There are no default login credentials. Password entry is masked; only its salted hash is stored in your local SQL database. Do not save credentials in Git, screenshots, logs, tickets, or this document. An optional second run with `-SkipAdministratorSeed` verifies migration idempotency without creating another account.
8. Set **ForteMove.Web** as the Startup Project. In `src/ForteMove.Web/Web.config`, confirm the `ForteMove` connection string targets the same instance/database used in step 6. It uses `Integrated Security=True`. If your local names differ, change only those non-secret settings locally and review `git diff` before every commit; the existing configuration does not automatically load `.env` or `*.local.config` files.
9. Start **HTTPS IIS Express**. The project defaults to `https://localhost:44300/`; if occupied, choose another IIS Express HTTPS port in Visual Studio's web settings. Let Visual Studio configure/trust its local IIS Express development certificate. Do not copy the owner's certificate or export its private key. Secure authentication cookies require HTTPS. `dotnet dev-certs` is not this classic Web Forms project's certificate setup workflow.
10. Sign in with the administrator you created. Confirm dashboard access, and that signed-out requests to Admin pages require login.
11. Walk through the accepted functionality using locally entered fictional test data:

    - Slice 1: authentication/logout, protected dashboard, bus registration, fleet search/filtering and details; record compliant dates, capacity, explicit vehicle state, and GVM.
    - Slice 2: create reusable Stops and an ordered Route; inspect Route List/Details and derived endpoints.
    - Slice 3: preview/create a recurring Schedule with future departures, inspect generated Trips, preview a future schedule change, and confirm protected/history Trips are retained for review.
    - Slice 4: create/edit a Driver with licence, passenger PrDP, availability and a private temporary password; sign in as that Driver and complete the mandatory password change; confirm the Driver Account page works and Admin pages are denied. Return as administrator, inspect/confirm assignment recommendations and change/remove a Scheduled assignment with a reason; inspect retained history and stale-update rejection.

The original developer's local SQL database **is not part of Git**. A fresh initialization creates schema, reference roles/categories/propulsion types, the migration ledger, and your interactive administrator. It does **not** reproduce manually entered demonstration buses, Stops, Routes, Schedules, generated Trips, Drivers, or Assignments. No demo seed was added. A future demo seed requires separate approval.

For consistent existing screen defaults, use South African local time on the development PC. Core operational calculations use `South Africa Standard Time` explicitly; the Assignment Queue's initial date still uses the host's `DateTime.Today` (see the audit).
