# Preliminary Vertical Slice 5 plan — Driver Operations

**Planning only. No Slice 5 source, pages, repository operations, migrations or seed data were implemented.** The owner must approve the technical decisions and scope before work begins. Existing implementation details are in [DEVELOPER_HANDOFF.md](DEVELOPER_HANDOFF.md).

## User experience and ownership

Extend the existing `src/ForteMove.Web/Driver` portal using `Driver.Master` and `DriverPage`. Use a mobile-first Trip list and detail/action page with readable status, Route/endpoints, departure, assigned bus, large controls and short forms. Groups:

- **Today / current:** assigned service-day Trips plus the Driver's still-active overnight Trips; show actionable status and warnings.
- **Upcoming:** future assigned Trips.
- **History:** the Driver's own completed work and appropriate historical involvement; hide other Drivers' identity/assignment history. A historical record grants no mutation authority.

Authenticate from `CurrentPrincipalContext.UserAccountId`, resolve the DriverProfile server-side, and filter every read/mutation by that identity. For operations on a Trip, join `TripAssignments` with `IsCurrent=1` and confirm the Driver still owns it inside the transaction. Treat all posted IDs, rowversions, checkboxes, status strings and client times as untrusted. Reject direct URL access and stale requests after reassignment. The portal role guard alone is insufficient. After-completion defect reporting may use the immutable record of the Driver's own performed Trip; this is a narrow reporting permission, never permission to operate a formerly assigned Trip.

## Lifecycle and actions

Existing generation/assignment transitions remain `Unassigned -> Scheduled`. Proposed Driver execution is `Scheduled -> Ready -> InProgress -> Completed`; only the assigned Driver can perform its own guarded transitions. Preserve actual start/completion times independently of display badges. The existing enum also allows `Delayed` and `Cancelled`, but these values do not currently implement execution or grant cancellation authority.

| Action | Proposed server-side rule and result |
| --- | --- |
| Pre-trip readiness | Current assigned Driver, unstarted Trip, South African service date, no unresolved blocking exception/defect, all practical checks passed, valid starting odometer. Save checklist/reading and move to `Ready` atomically. Readiness occurs before actual departure; confirm any scheduled-departure cutoff policy with the owner. |
| Start Trip | Current assigned Driver, successful readiness for the current assignment/bus, no actual start recorded, no unresolved hold, server operational time at least `ScheduledDepartureLocal - 5 minutes`. Record `ActualStartUtc`, move to `InProgress`. Allow no earlier start. |
| Delay | Assigned Driver may report before departure or during the active Trip. Save reason, optional positive estimated minutes when useful, reporting UTC time and phase. Delay is nonterminal; it must not prevent later start/continuation/completion on its own. |
| Cannot proceed | Record reason/context, phase and server reporting time as an unresolved operational exception requiring administrator attention. Driver cannot permanently cancel the service. Block inappropriate start/readiness until resolved; do not silently cancel or erase the assignment. |
| Report defect | Authorized Driver during pre-trip, active Trip or after their completed Trip; store linked bus, Trip when applicable, Driver and reporting UTC time, category, severity and description. |
| Complete Trip | Current assigned Driver, actual start exists, not already terminal, valid ending reading. Record server completion UTC time and optional note; atomically update accepted bus odometer and `TripStatus=Completed`, append reading/event/audit. |

Checklist (seven short attestations, not a workshop inspection): exterior condition checked; tyres appear safe; lights/indicators checked; no critical dashboard warning; doors operational; emergency equipment present; no new defect preventing operation. Incomplete/failed checks do not mark Ready. Give a clear defect/cannot-proceed reporting route rather than converting a failed checklist into a permanent cancellation.

Readiness must capture **start odometer**, decimal precision compatible with `Buses.OdometerKilometres` (`DECIMAL(12,1)`), nonnegative and at least the bus's latest accepted/verified baseline. Save which TripAssignment/bus the checklist applies to. Reassignment before readiness invalidates stale submissions; after readiness, preserve existing state restrictions instead of allowing a blanket reassignment.

Ending odometer must be at least both the saved starting value and the bus's applicable previous verified reading, read under the same transaction/lock. Reject stale/out-of-order completion that would lower a newer bus reading. Update the bus only monotonically; never overwrite a later accepted reading with an older smaller one. Keep the provenance/reading ledger for future Maintenance and energy calculations. The current registered odometer has no verification-history metadata: migrate it as a clearly labelled legacy/admin baseline rather than inventing a physical verification event.

Use actual timestamps from the server, not hidden fields or phone clocks. Make duplicate POST/retry behavior explicit: one successful readiness/start/completion effect, with a clear already-applied or stale result on repeated requests. Do not allow a second active Trip on the same Driver/bus simply because a preceding Trip's planned finish has passed; actual overruns matter.

## Delay, exception, and defect modeling

Recommended design: keep delay as append-only operational events plus a display badge while preserving the lifecycle state (`Scheduled`, `Ready`, `InProgress`). That avoids losing readiness/start phase when a Trip is delayed. If the owner requires persisted `TripStatus=Delayed`, store enough execution-phase/prior-state information to resume safely, and make actual-start evidence authoritative. Do not infer "started" merely from the Delayed value. No delay choice is implemented in this task.

Cannot-proceed creates a separate exception with resolution status, reason, reporting actor/time and administrator resolution actor/time/note. Surface unresolved exceptions through administrator queries/dashboard on normal requests. The administrator may decide a safe reassignment, hold, replacement service or cancellation through explicitly approved guarded operations; do not let the Driver select a terminal cancellation action. Existing Slice 4 assignment change/remove methods only permit `Scheduled` Trips, so handling Ready/active exceptions requires deliberate administrator workflow design, not weakening those guards globally. Completing a Trip does not automatically resolve an unrelated safety exception.

Defect categories: Engine/drivetrain, Brakes, Tyres, Electrical, Doors, Lights, Body/interior, Other. Severity: Minor, Major, Critical. Store report status/review/resolution actor/time separately from the Driver's severity assessment. The Driver never directly chooses `Buses.BaseOperationalState`.

An **unresolved Critical** defect excludes the bus from **new intelligent recommendations** and from confirmation/reassignment eligibility revalidation. A warning in the UI is insufficient. Extend both `AssignmentService` candidate eligibility and `SqlAssignmentRepository.ValidateAndInsert` (including alternative/change paths). Coordinate defect writes, bus locks/rowversions and assignment confirmation so a Critical defect reported after preview cannot race into a new confirmed assignment. Administrator review must have an explicit outcome; a generic `RequiresReview` clear action must not bypass a still-unresolved Critical defect or cannot-proceed hold. Surface existing affected assignments for review without having the Driver silently change the permanent bus state.

Keep defects bus-centric with an optional Trip link, so later Maintenance can attach work orders/reviews/resolutions without losing the original Driver report. Do not implement work orders or maintenance repair operations within this slice.

## Proposed persistence additions (not existing tables)

Design one or more new migrations beginning at the next approved number, expected `0006`. Preserve `0000–0005` byte-for-byte. Suggested responsibilities, names subject to approval:

- **TripExecution / TripReadiness:** unique execution per Trip, binding to TripAssignment, Driver/bus snapshots, seven checklist fields, start/end odometers, readiness UTC time, actual start/completion UTC times, completion note, rowversion and actor metadata. Require valid start/completion pairing and chronology.
- **TripOperationalEvents / TripDelays:** append-only delay/report events with reason, phase, optional estimate and server UTC time.
- **OperationalExceptions:** cannot-proceed and administrator resolution history; unresolved blockers remain distinguishable from schedule-change review.
- **BusDefectReports:** bus, optional Trip, reporting Driver/time, category/severity/description and controlled review/resolution state. Index unresolved Critical reports by bus.
- **BusOdometerReadings:** reading, kind (legacy baseline/start/completion), source Trip/assignment/Driver, server time, acceptance/provenance and correction linkage if later approved. Keep `Buses.OdometerKilometres` as the compatible latest-value field, updated transactionally.

Use foreign keys, uniqueness/check constraints, bounded text, appropriate decimal/date types and rowversions for mutable aggregates. Preserve history; do not cascade-delete execution, assignments, defects, odometer readings or audit evidence.

## Implementation map

Existing extension directories are `src/ForteMove.Models`, `src/ForteMove.Business/Contracts`, `src/ForteMove.Business/Services`, `src/ForteMove.Data/Repositories`, and `src/ForteMove.Web/Driver`. Proposed `DriverOperations` models, `IDriverOperationsRepository`, `DriverOperationsService`, `SqlDriverOperationsRepository`, and Trip pages are **new names**, not claimed existing files. Register new classes/pages in the explicit `.csproj` item lists and wire services in existing `Infrastructure/ServiceFactory.cs`.

Use `IClock.OperationalNow` for service-date/start thresholds and `UtcNow` for persisted actual events/audits; convert for South African display. Midnight must not hide an ongoing Trip or prevent its later completion. Each write revalidates identity, current assignment, lifecycle, blockers and rowversions in a transaction and writes `SqlAuditWriter` metadata. Coordinate locks with existing `ForteMove.Assignments` and bus/Trip writers in a consistent order. Extend existing scheduling protection logic so no operational dependency can be deleted by future schedule regeneration, even if someone removes the assignment later.

For scheduled departures that have passed without an actual start, **retain the real state** and compute an administrator warning on page/query load. Include Ready-but-not-started and overdue Scheduled Trips, with assigned Driver/bus, lateness and unresolved exceptions. Do not auto-start, auto-cancel or add a background worker for this.

## Acceptance tests to plan before implementation

1. Two Drivers: cross-Driver list/detail/POST access rejected; URL/hidden-field identity tampering rejected; reassignment between GET and POST rejected.
2. Incomplete checklist and below-baseline start odometer rejected; successful same-service-date readiness saved with the assigned bus/assignment.
3. Start at six minutes early rejected; exactly five minutes early accepted when otherwise eligible; readiness required; actual start cannot be forged; duplicate starts do not repeat writes.
4. Delays before start and underway retain phase, capture server time, and permit valid later start/completion.
5. Cannot-proceed reaches administrator attention, leaves cancellation authority with administrator, and cannot be bypassed by a generic review-clear action.
6. Defects in all three phases; all categories/severities; unresolved Critical bus excluded from preview **and** atomic confirmation, including a report arriving after preview. Driver cannot post a bus permanent-state change.
7. Completion without actual start rejected; decreasing/stale odometers rejected; valid completion updates bus/Trip/reading/audit once; transaction failure rolls everything back.
8. Late/missed departure warning does not mutate Trip state; overnight Trip remains visible/operable; actual overrun prevents unsafe simultaneous resource use.
9. Schedule change preserves every Trip with readiness, execution, delay, exception, defect or reading history; previous Slice 1–4 acceptance still passes.
10. Mobile layout, validation text, no cached credential/operational forms, proper HTML encoding, first-password-change enforcement, and stale rowversion feedback.

## Decisions for owner approval before coding

- Confirm delay overlay versus an explicit Delayed persisted phase, and its UI wording.
- Confirm the last permitted late-start date/time and whether readiness may occur after scheduled departure while still before actual departure. No policy should auto-cancel on expiry.
- Define the administrator's safe resolution of Ready/active cannot-proceed cases and review/resolution of Critical defects.
- Confirm how accepted Driver readings become verified operational inputs and how authorised corrections retain provenance.
- Approve the schema/model/page names and the next available migration number with all collaborators.

The approved business direction above is preserved; these are implementation details to settle, not permission to implement Slice 5 now.
