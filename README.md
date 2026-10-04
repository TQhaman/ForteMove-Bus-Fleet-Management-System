# ForteMove

ForteMove is an ASP.NET Web Forms bus-depot and scheduled-transit management application. This repository currently contains the platform foundation and the first three Sprint 1/2 vertical slices:

1. secure Transport Administrator authentication and logout;
2. role-protected administrator workspace and dashboard;
3. bus registration with business validation and an editable automatic fleet-number suggestion;
4. a searchable, filterable fleet list;
5. database-backed fleet and active-route dashboard counts; and
6. transactional Route creation with reusable ordered Stops, Route List, and Route Details; and
7. recurring Schedule creation, synchronous dated Trip generation, Schedule history, safe future changes, and operational Trip browsing.

No demo identities, buses, routes, stops, schedules, trips, assignments, drivers, passengers, wallets, tickets, tracking records, maintenance work orders, or fuel/energy records are seeded.

## Architecture

The solution targets .NET Framework 4.8 and deliberately separates presentation, business rules, persistence, and shared models:

- `src/ForteMove.Web` — ASP.NET Web Forms presentation and composition root.
- `src/ForteMove.Business` — application services, validation, password verification, and repository contracts.
- `src/ForteMove.Data` — parameterised ADO.NET repositories and transactional persistence.
- `src/ForteMove.Models` — entities, requests, results, DTOs, and enums.
- `database/migrations` — immutable SQL Server migrations with recorded SHA-256 checksums.
- `tools/Initialize-ForteMoveDatabase.ps1` — safe, idempotent database initialization and first-administrator seed.

The future scheduling domain must preserve `Route -> Recurring Schedule -> Trip -> Trip Assignment`. A bus stores only its base state (`Operational`, `OutOfService`, `UnderMaintenance`, or `Retired`); future assignment/trip activity will be derived rather than persisted as a competing state.

## Local setup

Prerequisites:

- Visual Studio 2022 with ASP.NET and .NET Framework development tools;
- .NET Framework 4.8 targeting pack; and
- SQL Server Express available as `.\SQLEXPRESS` (or an explicitly supplied SQL Server instance).

Initialize the schema and securely create the first Transport Administrator from a PowerShell prompt at the repository root:

```powershell
.\tools\Initialize-ForteMoveDatabase.ps1
```

The initializer asks locally for the administrator identity and final passphrase. It has no default credentials and never replaces an existing administrator. See [`database/README.md`](database/README.md) for configuration, idempotency, and migration details.

Open `ForteMove.sln`, set `ForteMove.Web` as the startup project, and launch its HTTPS IIS Express URL. The authentication cookie is intentionally marked Secure, so an HTTP-only URL cannot retain a login.

## Implemented workflow

1. Browse to an Admin page while signed out and return to the login page.
2. Sign in with the administrator created by the initializer.
3. Use the Dashboard action to open Register Bus.
4. Enter approved vehicle and compliance information.
5. After registration, inspect the resulting row in Fleet List.
6. Open Create Route, add existing Stops or keep new Stops in the in-progress route draft, and arrange them with Move Up and Move Down.
7. Save the Route. New Stops, the Route, its ordered RouteStops, and audit entries are committed together.
8. Inspect the saved Route in Route List and Route Details.
9. Open Create Schedule, choose an active Route, operating days, departure times, and an effective period, then review the exact future Trip count.
10. Create the Schedule and inspect it through Schedule List, Schedule Details, and Trips.
11. Use Change Schedule to review the impact of a future-dated recurring-pattern change before applying it.
12. Use the POST-backed Logout action when finished.

An expired licence, roadworthy certificate, or insurance date is accepted for record completeness, but the bus is saved as `OutOfService` with a warning. A compliant bus is initially `Operational`.

Route and Stop codes are assigned automatically. New Stops entered in Create Route are not persisted if the administrator abandons the draft or the aggregate save fails. Origins and destinations are always derived from the first and final ordered RouteStops; they are not stored separately.

Schedule codes such as `FM-S01` remain stable across internal history. Generated Trips use codes such as `TR-000001`, begin as `Unassigned`, and store South African local service time plus an expected-finish snapshot. Audit timestamps remain UTC. A Schedule starting today generates only departures strictly later than the current South African operational time.
