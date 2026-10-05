# Vertical Slice 7 — simulated tracking

Implemented on `Slice-7`, starting from clean commit `e73a527f1dbc34955578a869f88ce3e4abfcc0e7`. HEAD remains unchanged. No branch switch, commit, staging, push, operational seed, or migration `0008` was performed. The existing logo is unchanged.

## Delivered behavior

- Operations > Live tracking lists assigned services with departure, actual start, Bus, Driver, status, simulated segment, and attention messages. Older unfinished and ongoing overnight services remain visible. Completed services can be included explicitly.
- Administrator Trip Tracking and the assigned Driver's Trip Details use the shared tracking panel. Passenger Ticket Details links to Track trip only for a Purchased Ticket on a non-cancelled Trip, including Completed Trips.
- Every JSON refresh checks the current authenticated account/role and Data-layer ownership. Driver access requires their current assignment; Passenger access requires their own Ticket. Refunded Tickets return a terminal structured result without geometry. Passenger snapshots omit Driver identity, credentials, reasons, notes, assignment identifiers, and rowversions.
- `TrackingCalculator` derives motion from actual UTC start and the persisted local expected-duration snapshot. Complete monotonic Stop timings take precedence; otherwise Haversine segment lengths determine relative weights. Movement interpolates straight between stored Stops. Browser time never determines position.
- Overlapping in-trip Delay/Cannot Proceed intervals are merged before subtraction. Open intervals freeze movement, and resolution resumes without counting paused time. A Critical defect alone does not freeze motion. Unfinished progress is capped at 99%; actual completion snaps to the final Stop. No automatic operational transitions occur.
- Missing/invalid coordinates or unusable timelines/geometry return ordinary structured unavailable results. Nonterminal polling continues so coordinate corrections can become visible without reloading. Ten-second polling has one request in flight, bounded retry after failure, hidden-tab suspension, and terminal/unauthorized stopping.
- Stop Coordinates is accessible from each Route Details itinerary item. Coordinates are paired, range-checked, limited to six decimal places, and protected by rowversion. The save transaction rechecks started unfinished Trips under the existing assignment lock. A correction affecting them requires a warning and acknowledgement, but is not prohibited. Audit details retain old and new coordinate values, including explicit NULLs.
- Leaflet 1.9.4 JS/CSS/images and its license are bundled locally. OSM tiles are optional; failure does not remove the local route, marker, itinerary, or status. `TrackingTileUrl` can be set to an empty value to disable external tiles. No geolocation, geocoding, tracking-position writes, background trackers, real GPS, or new operational workflow exists.

Tracking uses **current** Stop geography, including on historical/Completed Trips. There are no geometry snapshots. Coordinate corrections affect old map rendering; this is not historical GPS evidence or road routing.

Dependency references: [Leaflet 1.9.4 documentation](https://leafletjs.com/reference.html), [OpenStreetMap tile policy](https://operations.osmfoundation.org/policies/tiles/). The normal browser tile requests contain no ForteMove Passenger/Driver identity or operational notes; normal origin/referrer and browser headers remain present.

## Verification executed on 5 October 2026

| Verification | Result |
| --- | --- |
| Debug Rebuild, Visual Studio MSBuild | Passed, 0 warnings / 0 errors |
| Release Rebuild, Visual Studio MSBuild | Passed, 0 warnings / 0 errors |
| ASP.NET page/control precompilation | Passed, no diagnostics |
| Deterministic Business verification | 55 assertions passed, no persistence |
| Repository/constraint/ownership rollback verification | 27 assertions passed, all changes rolled back |
| Read-only repository regression and real JSON-handler checks | 32 checks passed, no writes or login attempts |
| Existing migration initializer rerun twice | All eight migrations checksum-skipped safely |
| `0000`–`0007` versus HEAD and live checksum ledger | Unchanged / matching |
| Static layering checks | No SQL/ADO.NET in Web or Business; no `AddWithValue` in production C# |
| Anonymous HTTPS JSON request | 401 JSON, no login redirect |
| Anonymous Admin, Driver, Passenger tracking/correction pages | 302 to Login |
| Browser renderer fixture | Missing-coordinate recovery, repeated polling, desktop/mobile layout, stale refresh warning, terminal marker removal and polling stop passed |
| Loopback tile failure fixture | Map-background warning with route/marker/progress/itinerary retained |

The 114 automated checks include actual Data-layer ownership queries and transactional coordinate writes/audits, not just substitute repository behavior. Transactional verification uses small internal repository cores under the caller's rollback transaction; no test action is exposed in the application.

Browser layout checks used 1280px desktop and 390px mobile viewports. Map heights were respectively 360px and 260px, without horizontal document overflow. The non-failing fixture logged no JavaScript warnings/errors. After Cancelled, the marker was absent and the request count remained unchanged. The failed-tile fixture intentionally produces HTTP 404 image requests.

Initial sandbox restrictions prevented MSBuild SDK access and SQL integrated authentication; the checks succeeded with approved installed-tool/Windows-account access. PowerShell's local script policy required a process-only `-ExecutionPolicy Bypass`; no persistent policy setting changed. An intermediate helper-refactor brace error was fixed; final builds are clean. Git may print normal LF-to-CRLF notices; these are not compilation warnings.

### Database preservation

The accepted database still contains 42 Trips and no committed TripExecutions, TripDelayEvents, Cannot Proceed reports, or Tickets. All 10 Stops still have no stored coordinates. There are no committed `StopCoordinatesUpdated` audits from implementation/testing. Existing coordinate checks remain enabled and trusted. No coordinates or new operational identities were fabricated. Rollback tests can consume SQL identity/rowversion sequence values, but leave no fixture rows or corrections.

### What was not claimed as tested

No real account password was supplied or changed. A full authenticated Administrator → Driver start/delay/completion → Passenger purchased-Ticket browser workflow was not performed against committed data, because the accepted database has no executions, Tickets, or approved coordinates. Business clocks, rollback SQL, read-only endpoint calls, and a synthetic renderer fixture verify those components. Complete the authorized manual workflow below with genuine approved inputs. OSM network-tile availability is environmental; the failure path was verified without transmitting fixture coordinates to an external provider.

## Manual acceptance workflow

1. Open `ForteMove.sln`, rebuild, and start `ForteMove.Web` at its existing HTTPS IIS Express URL. Signed-out visits to Admin Tracking, Driver Trip Details, Passenger TrackTrip, and Stop Coordinates must require Login; the JSON handler must return 401 JSON.
2. Sign in as Transport Administrator. Open Route List → Route Details. Each itinerary item shows coordinate readiness and an Update coordinates action. Enter only approved latitude/longitude pairs for each Stop. Test one missing value, latitude outside -90..90, longitude outside -180..180, and more than six decimal places: each must be rejected cleanly. Blank pairs are permitted. Leave an unsaved edit and confirm no value changed.
3. Open Operations → Live tracking, choose a service date with an approved current assignment, then Track Trip. Without complete geography, expect a specific unavailable warning and a usable itinerary. Correct the missing coordinates in another tab; the open nonterminal tracking page should recover on a later refresh.
4. Before actual start, a Scheduled Trip must not move just because departure passed. Ready/pre-start Delayed may show a clearly simulated origin marker. Use an operationally eligible Bus/Driver and an approved same-day service within the existing start window; existing compliance, review, Cannot Proceed and defect rules still apply.
5. Sign in as that assigned Driver. Open Today → Trip Details. Confirm readiness and Start through the existing Slice 5 actions. The informational tracking panel should use the current assigned Bus and begin movement only after actual start. A different Driver must not access this Trip's tracking URL or JSON.
6. Report an in-trip delay. Confirm that the marker stops advancing. Resume legitimately, then confirm it advances from the paused position rather than jumping by the delay duration. Repeat to verify multiple delay intervals.
7. Report Cannot Proceed after start. Confirm movement freezes; resolve it legitimately through Admin Exceptions with the required note and no unresolved Critical defect. Confirm continuation does not include paused time. Overlapping delay/exception periods must not be subtracted twice.
8. While a Trip is started and unfinished, edit a Stop it uses. Review the warning identifying affected Trips and current/proposed coordinates; acknowledge the correction and save. The open map should reflect the correction on a later refresh. Inspect `StopCoordinatesUpdated` audit details for old and new values. Two editors using the same old rowversion must not overwrite each other silently.
9. Use an approved Passenger account and legitimate Purchased Ticket obtained through the existing wallet/journey workflow. Ticket Details → Track trip must show only that Ticket's Trip. Another Passenger's ticketId and a passenger-supplied tripId must be rejected without disclosing the booking or Driver private information. Assignment changes before start retain the same Trip tracking identity/current Bus.
10. After nominal duration, an unfinished Trip must remain short of destination (99%). Complete through the Driver's existing ending-odometer workflow: the map should snap to destination, say Completed and stop automatic polling. The Passenger Ticket remains Purchased/Past ticket, not Used/Travelled. Completed maps use current coordinates.
11. Test a legitimate administrator cancellation and Ticket refund through existing operations. There must be no moving marker or continued polling, and Passenger TrackTrip must return to the owned Ticket/refund details. Completed/Cancelled Trips remain terminal under existing rules.
12. Disconnect tile access or set `TrackingTileUrl` empty: local geometry and progress remain usable with a map-background message. Interrupt snapshot access: the last position is marked stale, not advanced by browser time. Restore access and check recovery. Hide the tab to suspend polling; show it again for refresh. Sign out/deactivate access and confirm polling no longer exposes tracking.
13. Regression-check Login/forced password change, Fleet, Routes, Schedules, assignments, review behavior, Driver actions, defects/exceptions, completion/cancellation, and wallet/Ticket behavior with approved data. Tracking itself must create no operational records or refresh audit events.

## Developer verification commands

Run from the repository root (paths are installation-specific):

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' ForteMove.sln /t:Rebuild /p:Configuration=Debug /nologo /v:minimal /clp:Summary
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' ForteMove.sln /t:Rebuild /p:Configuration=Release /nologo /v:minimal /clp:Summary
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\aspnet_compiler.exe' -v / -p "$PWD\src\ForteMove.Web" -errorstack
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Verification\Invoke-Slice7Verification.ps1 -IncludeDatabaseChecks
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Initialize-ForteMoveDatabase.ps1 -SkipAdministratorSeed
git diff --exit-code HEAD -- database/migrations
git diff --check
```

Review test dataset assumptions before running database harnesses elsewhere. No database setup change is required for Slice 7 beyond verifying existing migrations.

## Files added/updated

- Models: `Tracking/TrackingModels.cs`, `Routing/StopCoordinateModels.cs`, coordinate fields on `RouteStopDetails`, project entries.
- Business: `ITrackingRepository`; `TrackingService`; `TrackingCalculator`; shared `Routing/CoordinatePolicy`; coordinate methods on `RouteService`/`IRouteRepository`; focused coordinate persistence exception; project entries.
- Data: `SqlTrackingRepository`; `SqlRouteRepository.Coordinates`; Route Details coordinate mapping; project entries. Tracking queries are read-only, batched, explicitly parameterized. Coordinate writes are audited transactions using the existing assignment lock.
- Web: `Tracking/Snapshot.ashx`; shared `Controls/TripTrackingPanel`; `Admin/Tracking/LiveTracking.aspx` and `TripTracking.aspx`; `Admin/Routes/StopCoordinates.aspx`; `Passenger/TrackTrip.aspx`; all associated code-behind/designer files; Driver Trip Details tracking partial/designer/control; Passenger Ticket Details link; Admin navigation; Route Details coordinate actions; ServiceFactory; shared CSS; tracking JS; tile configuration; classic project entries; locally bundled Leaflet assets/license.
- Verification/documentation: three Slice 7 C# harnesses, handler config, reusable verification script, standalone browser fixture/loopback host, verification README, root README, database README, and this report.

No existing production files were removed. Migration files, authentication architecture, operational services, existing assignment/schedule/payment rules, and logo assets were preserved.

Final Git status: 23 modified tracked files and 45 new untracked files, all left unstaged. Branch/HEAD are unchanged. Temporary verification browser/loopback-host/IIS Express processes were closed; launch the application normally from Visual Studio for manual acceptance.

## Manual-test follow-up — tracking stylesheet runtime error

The manual `TR-000006` test exposed a runtime `HttpException`: the master page's head contains inline code blocks, so its Controls collection is read-only. `TripTrackingPanel.OnPreRender` had tried to append a Leaflet stylesheet to that collection. Successful compilation/precompilation and the earlier standalone renderer fixture did not exercise this real page/master lifecycle; the earlier tests therefore missed the defect.

The shared control now declares its local stylesheet directly in `TripTrackingPanel.ascx`, with a server-resolved application-relative URL. It no longer modifies `Page.Header.Controls`. Script registration, authentication, polling, calculations, and database behavior are unchanged. The same control supplies Administrator, Driver, and Passenger tracking.

Verified against clean follow-up baseline `Slice-7` commit `43fe196f6a3f6712884fb899b059b7ef9ee6e95d`:

- Debug and Release isolated builds: zero warnings/errors.
- New real-page render regression: the actual `Admin/Tracking/TripTracking.aspx` page rendered successfully while its master head remained read-only; local CSS and both scripts were present.
- `TR-000006` read-only snapshot: exactly five missing-coordinate Stops, complete itinerary, no position coordinates/marker, polling enabled, and server refresh time present.
- No database writes, account changes, migration changes, or replacement of the DLLs used by the paused Visual Studio session.

Stop the paused debugging session, use **Build > Rebuild Solution**, then start the app again and retry **Track Trip** for `TR-000006`. Do not continue the old paused request or run a stale last-successful build. See the verification README for the repeatable runtime render command.
