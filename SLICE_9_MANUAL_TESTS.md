# Slice 9 — Manual browser acceptance

Automated results: [SLICE_9_VERIFICATION.md](SLICE_9_VERIFICATION.md). This checklist is outstanding human acceptance. Use only approved fictional East London providers, service intervals, source records and verified odometers. Do not invent vehicle evidence or send real provider notifications.

## Prepare

1. Rerun tools/Initialize-ForteMoveDatabase.ps1 -SkipAdministratorSeed; all ten migrations should be current.
2. Rebuild in Visual Studio, launch ForteMove.Web over HTTPS, sign in as Transport Administrator.
3. Open Maintenance > Overview. Counts reflect stored data; no providers/plans/orders were seeded.
4. Select an approved Bus. Check Fleet Details for odometer/GVM/compliance and legitimate Critical defects.
5. Use approved unstarted assigned test Trips, Driver and Passenger for integration cases. Generate applicable future Trips through the existing Schedule workflow if needed, not by changing timestamps.
6. Keep a second administrator tab for stale-review tests. Browser changes persist; use disposable approved records.

## Core workflow

1. **Repair Providers:** create an approved fictional provider with name/area. Phone/email optional. Verify automatic RP- code and editing. Add a second provider if testing reference changes.
2. **Preventive Plans:** choose Bus, service name and positive day/km interval(s). Select either verified previous-service evidence or explicit approved first-due thresholds with previous-service fields empty. Choose Active/blocking behavior deliberately.
3. Verify thresholds and derived status in Plans. First due must not masquerade as previous service.
4. From Driver Defect Details choose **Create work order**, or use Work Orders > Create work order. Choose provider, type, trigger, applicable source, requested work and optional target. An Open defect needs a review note.
5. Save and verify MWO- code, provider snapshot and source context. Bus/Trip states, Tickets and refunds are unchanged; review is not resolution.
6. In Work Order Details review Bus status, affected unstarted assigned Trips including late departures, purchased-Ticket counts and unused fuel counts.
7. Explicitly confirm and **Start maintenance**. An active execution blocks workshop entry; a changed impact set requires refreshed confirmation.
8. Verify In progress work and Under maintenance Bus. Assignments/Tickets remain; Trips require review. Pending Requests/unused Active Vouchers are cancelled, redeemed history preserved.
9. Append progress; refresh and confirm retained history.
10. **Complete work order:** enter approved verified odometer at least current reading, work performed and completion note. Optional cost/reference; R0.00 cost is valid. Leave linked defect unresolved initially when testing the blocker.
11. Complete and verify evidence, higher odometer when applicable, linked Plan reset and Bus still Under maintenance. Compliance is not automatically renewed.
12. **Fleet Details > Return to Service:** confirm real blockers are explained. Correct/resolve through proper workflows, refresh, enter note and confirm.
13. Verify Operational status and vehicle-status history. Resolve retained Trip reviews separately through Assignment Details using fresh eligibility.
14. Service History/Work Order Details must preserve provider snapshots, progress, open/start/completion times and separate Return evidence.

## Boundaries and safety

- Date tomorrow / today / yesterday: Not due / Due / Overdue.
- Odometer above / equal / below current reading: Not due / Due / Overdue.
- Combined thresholds use the most urgent result and show relevant reasons.
- Inactive plan: no block. Advisory Overdue plan: attention without block.
- Active blocking Overdue plan excludes new assignment/readiness/start/purchase/Return; exact Due alone does not.
- After recorded completion, interval edits recalculate from that baseline. Linked active work prevents Plan edits.
- Invalid precision/range, negative odometer/cost and threshold overflow: friendly errors, no partial records.
- Fleet Edit cannot set a non-operational Bus Operational, leave UnderMaintenance during active work or reactivate Retired.
- Expiry equal to today is valid for Return; Trip checks still require coverage through expected finish.
- Return checks Critical defects, InProgress work, blocking overdue service, compliance and assignment-readiness data. It does not clear Trip reviews.

## Cross-workflow regression

1. Use the existing Driver defect flow, administrator review and linked Corrective work. Optionally link a Plan only when its service is genuinely performed.
2. UnderMaintenance prevents readiness/start/new fuel requests and new Ticket sales. Defect/Cannot Proceed reporting remains available.
3. Tickets remain Purchased during maintenance. Allowed pre-start assignment removal retains Tickets and its warning.
4. Fuel approval/redemption must fail for UnderMaintenance/cancelled authorizations. Redeemed FuelTransactions remain; maintenance creates none.
5. A genuinely started Trip blocks Maintenance Start. Approved administrator cancellation uses the normal refund path; cancelled execution history must not permanently occupy Driver/Bus.
6. Already-started Trip whose Plan becomes overdue: Resume/completion still permitted subject to existing Critical-defect/Cannot Proceed rules.
7. Maintenance never auto-starts/cancels/moves a Trip. Tracking polling/unavailable-coordinate handling remains unchanged.
8. Schedule-change preview preserves assignment/operational/Ticket/fuel dependent Trips for review.
9. Regression-test password-change login, Fleet, Routes, Schedules, Trips and operational Dashboard.

## Concurrency, sources and cancellation

- Two completion tabs: first succeeds; stale second rejects.
- Bus odometer/compliance change during review: stale evidence is not silently accepted.
- Assignment/Ticket/fuel impact changes during Start review: refreshed review and renewed confirmation.
- Concurrent linked-defect resolution: stale completion resolution fails atomically.
- Repeated submission creates no duplicate order; duplicate active source is rejected.
- Different issues may have multiple Open orders, but only one InProgress per Bus.
- Wrong-Bus linked source is rejected. Service triggers need their Plan threshold; Defect needs a defect; Breakdown needs evidence. External reported need uses Manual with explanatory work.
- Compliance trigger requires licence/roadworthy/insurance selection.
- Deactivated provider unavailable for new work, but old work completable. Renames preserve old snapshots.
- Cancel Open with reason: Bus unchanged. Cancel InProgress: Bus stays UnderMaintenance. Completed/Cancelled cannot reopen.
- Anonymous/Driver/Passenger Admin URLs and forged writes are denied. Check real HTTPS login/logout and Back behavior.

## Acceptance evidence

Record approved Bus/Plan/Work Order/Trip codes, expected versus actual result and errors. Do not record passwords, voucher tokens or full Driver credentials.

These manual writes are not automatically cleaned up. Request explicit cleanup authorization; retain terminal history rather than deleting it. Browser acceptance is complete only after applicable cases are checked.

Workshop time and persistent vehicle downtime differ; no historical downtime is fabricated. Cost is not payment/procurement. Photos/uploads and provider logins are not implemented.
