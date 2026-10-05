# ForteMove

ForteMove is an ASP.NET Web Forms bus-depot and scheduled-transit management application. The accepted baseline is Vertical Slices 1–7. Vertical Slice 8 adds Fuel Voucher Management, with its implementation checkpoint on `Slice-8-completed`. See the Slice 8 verification report for the manual acceptance workflow.

1. secure Transport Administrator authentication and logout;
2. role-protected administrator workspace and dashboard;
3. bus registration with business validation and an editable automatic fleet-number suggestion;
4. a searchable, filterable fleet list;
5. database-backed fleet and active-route dashboard counts; and
6. transactional Route creation with reusable ordered Stops, Route List, and Route Details; and
7. recurring Schedule creation, synchronous dated Trip generation, Schedule history, safe future changes, and operational Trip browsing.
8. Transport Administrator Driver management with secure temporary passwords and mandatory first-login password change; and
9. GVM-aware, explainable Driver-and-bus recommendations with atomic assignment confirmation and retained decision history.
10. mobile-first assigned-Driver Trip readiness, start, delay, resume, Cannot Proceed, defect reporting, completion, and history;
11. administrator exception resolution, Trip cancellation, defect review, and live operational attention counts; and
12. Passenger self-registration, simulated wallet top-ups, journey discovery, Ticket purchase, retained Ticket history, and automatic cancellation refunds.
13. audience-protected, simulated Trip tracking using ordered Stop coordinates and actual operational timestamps;
14. Driver Fuel Requests, administrator authorization, one-use QR Fuel Vouchers, approved Station management, and protected prototype redemption with immutable transaction history.

No demo identities, buses, routes, stops, schedules, trips, assignments, drivers, passengers, wallets, tickets, tracking records, maintenance work orders, or fuel/energy records are seeded.

## Architecture

The solution targets .NET Framework 4.8 and deliberately separates presentation, business rules, persistence, and shared models:

- `src/ForteMove.Web` — ASP.NET Web Forms presentation and composition root.
- `src/ForteMove.Business` — application services, validation, password verification, and repository contracts.
- `src/ForteMove.Data` — parameterised ADO.NET repositories and transactional persistence.
- `src/ForteMove.Models` — entities, requests, results, DTOs, and enums.
- `database/migrations` — immutable SQL Server migrations with recorded SHA-256 checksums.
- `tools/Initialize-ForteMoveDatabase.ps1` — safe, idempotent database initialization and first-administrator seed.

The scheduling domain preserves `Route -> Recurring Schedule -> Trip -> Trip Assignment`. A bus stores only its persistent vehicle state (`Operational`, `OutOfService`, `UnderMaintenance`, or `Retired`); assignment/trip activity is derived rather than persisted as a competing state.

## Local setup

Prerequisites:

- Visual Studio 2022 with ASP.NET and .NET Framework development tools;
- .NET Framework 4.8 targeting pack; and
- SQL Server Express 2017 or later available as `.\SQLEXPRESS` (or an explicitly supplied SQL Server instance).

Initialize the schema and securely create the first Transport Administrator from a PowerShell prompt at the repository root:

```powershell
.\tools\Initialize-ForteMoveDatabase.ps1
```

The initializer asks locally for the administrator identity and final passphrase. It has no default credentials and never replaces an existing administrator. See [`database/README.md`](database/README.md) for configuration, idempotency, and migration details.

Open `ForteMove.sln`, set `ForteMove.Web` as the startup project, and launch its HTTPS IIS Express URL. The authentication cookie is intentionally marked Secure, so an HTTP-only URL cannot retain a login.

Restore NuGet packages before building (Visual Studio: right-click the solution → Restore NuGet Packages, or `nuget restore ForteMove.sln`). Slice 8 pins QRCoder 1.8.0 in `ForteMove.Web/packages.config`; QR images are generated locally without an external QR service. The ignored `packages` directory is not committed.

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
12. Create a Driver with a temporary password and complete the mandatory password change during the Driver's first login.
13. Record approved GVM values for eligible fleet vehicles.
14. Use Assignment Queue to review and atomically confirm eligible Driver-and-bus recommendations.
15. Review assignment history, change/remove a safe pre-start assignment, or cancel a nonterminal Trip with a recorded reason.
16. Sign in as an assigned Driver and use Today to complete the safety checklist, start, report delays or blockers, resume, report defects, and complete the Trip with an ending odometer.
17. Use the administrator Operations views to resolve Cannot Proceed reports and review or resolve vehicle defects.
18. Use the POST-backed Logout action when finished.
19. Create a Passenger account from the login page, sign in, and top up the simulated wallet.
20. Find an assigned future journey, review its live fare and capacity, purchase a Ticket, and inspect it under Tickets.
21. If an administrator cancels the Trip, verify that the Ticket is retained as Refunded and its exact fare returns to the Passenger wallet.
22. Enter approved Stop coordinates from Route Details, then open Operations > Live tracking as Administrator, Trip Details as the assigned Driver, or Track trip from the Passenger's own Ticket Details.
23. Create a fictional approved Fuel Station as Administrator. As the assigned Driver, submit a Fuel Request, then approve it as Administrator and view the one-use Voucher/QR as its Driver.
24. Use the protected Prototype Fuel Station Terminal as Administrator to review the token at the approved Station and confirm one redemption. Inspect Fuel Transactions; no real payment or supply-provider integration occurs.

Vehicle status is an explicit administrator choice. An `Operational` request is rejected when the licence, roadworthy certificate, or insurance is expired; the administrator must correct the compliance information or deliberately select `OutOfService`. Assignment eligibility independently checks compliance even when a stored vehicle status is Operational.

A Driver must be at least 21 to be created or retained after a Date of Birth update. Expired licence or PrDP information may be retained with a warning, but the Driver remains ineligible for assignment until it is renewed.

Route and Stop codes are assigned automatically. New Stops entered in Create Route are not persisted if the administrator abandons the draft or the aggregate save fails. Origins and destinations are always derived from the first and final ordered RouteStops; they are not stored separately.

Schedule codes such as `FM-S01` remain stable across internal history. Generated Trips use codes such as `TR-000001`, begin as `Unassigned`, and store South African local service time plus an expected-finish snapshot. Audit timestamps remain UTC. A Schedule starting today generates only departures strictly later than the current South African operational time.

Driver operations keep planned service times unchanged and store actual operational timestamps in UTC for South African local display. Readiness is tied to the exact assignment, a five-minute early-start window is enforced, and late Trips never auto-start or auto-cancel. Completion preserves immutable start/end readings and atomically advances the Bus odometer. Open Cannot Proceed reports block operational actions, while unresolved Critical defects independently block assignment and Trip start/resume.

Passenger Ticket sales use the current assigned Bus capacity and operational eligibility rather than assignment recommendation rankings. Purchases debit the Passenger wallet, snapshot the Route fare, and protect the Trip as an operational dependency. Simulated top-ups do not use a payment gateway. Trip completion leaves a purchased Ticket as a past Ticket because boarding/redemption is intentionally outside this slice; administrator Trip cancellation performs the full atomic refund instead.

## Collaborator handoff

- [Slice 8 Fuel Voucher rules, verification results, and manual workflow](SLICE_8_VERIFICATION.md)

- [Slice 7 tracking implementation, verification results, and manual workflow](SLICE_7_VERIFICATION.md)

- [Collaborator setup and Slice 1–6 walkthrough](COLLABORATOR_SETUP.md)
- [Slice 6 checkpoint audit, history and current implementation map](SLICE_6_CHECKPOINT.md)
- [Earlier repository security, privacy and portability audit](REPOSITORY_AUDIT.md)
- [Historical Slice 4 branch workflow](BRANCH_WORKFLOW.md)
- [Slice 4 developer architecture and implementation map](DEVELOPER_HANDOFF.md)
- [Historical Slice 5 Driver Operations design](SLICE_5_PLAN.md)
