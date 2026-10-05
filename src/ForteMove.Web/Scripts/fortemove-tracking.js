(function () {
    "use strict";
    function start() {
        document.querySelectorAll("[data-tracking-url]").forEach(function (panel) {
            var get = function (name) { return panel.querySelector('[data-tracking="' + name + '"]'); };
            var map = null, path = null, bus = null, stopMarkers = [], timer = null, running = false, stopped = false, failures = 0, geometry = "";
            function text(name, value) { get(name).textContent = value || "-"; }
            function message(name, value) { text(name, value); get(name).hidden = !value; }
            function clearBus() { if (bus && map) { map.removeLayer(bus); bus = null; } }
            function render(s) {
                text("status", s.SafeStatus); text("fleet", s.FleetNumber || "Assignment pending");
                text("progress", s.ProgressPercent === null ? "Unavailable" : s.ProgressPercent.toFixed(1) + "%");
                text("current", s.CurrentStop); text("next", s.NextStop);
                text("refreshed", s.LastCalculatedLocalTime);
                text("context", s.RouteCode + " - " + s.RouteName + ". Departure: " + s.ScheduledDepartureLocal + ". Expected finish: " + s.ExpectedFinishLocal + ".");
                message("availability", s.AvailabilityMessage); message("attention", s.AttentionMessage === s.AvailabilityMessage ? null : s.AttentionMessage); message("stale", "");
                var list = get("stops"); list.replaceChildren();
                (s.Stops || []).forEach(function (stop) { var item = document.createElement("li"); item.textContent = stop.Name; if (stop.Name === s.NextStop) item.className = "tracking-next"; list.appendChild(item); });
                var valid = (s.Stops || []).length >= 2 && s.Stops.every(function (stop) { return typeof stop.Latitude === "number" && typeof stop.Longitude === "number" && stop.Latitude >= -90 && stop.Latitude <= 90 && stop.Longitude >= -180 && stop.Longitude <= 180; });
                if (typeof L === "undefined") { message("tiles", "The map could not load. The itinerary and service status remain available."); }
                else if (valid) {
                    if (!map) {
                        map = L.map(get("map"), { scrollWheelZoom: false });
                        if (panel.dataset.tileUrl) {
                            var layer = L.tileLayer(panel.dataset.tileUrl, { maxZoom: 19, attribution: '&copy; <a href="https://www.openstreetmap.org/copyright" rel="noreferrer">OpenStreetMap contributors</a>' }).addTo(map);
                            layer.on("tileerror", function () { message("tiles", "Map background unavailable. The simulated route and progress remain available."); });
                        } else message("tiles", "Map background disabled. The simulated route and progress remain available.");
                    }
                    var signature = JSON.stringify(s.Stops);
                    if (signature !== geometry) {
                        geometry = signature; if (path) map.removeLayer(path); stopMarkers.forEach(function (marker) { map.removeLayer(marker); }); stopMarkers = [];
                        var points = s.Stops.map(function (stop) { return [stop.Latitude, stop.Longitude]; });
                        path = L.polyline(points, { color: "#1684df", weight: 4 }).addTo(map);
                        s.Stops.forEach(function (stop, i) {
                            var label = document.createElement("span"); label.textContent = stop.Order + ". " + stop.Name;
                            stopMarkers.push(L.circleMarker([stop.Latitude, stop.Longitude], { radius: i === 0 || i === s.Stops.length - 1 ? 8 : 5, color: "#10243b", fillColor: i === 0 ? "#2c9471" : i === s.Stops.length - 1 ? "#10243b" : "#fff", fillOpacity: 1 }).bindTooltip(label).addTo(map));
                        });
                        map.fitBounds(path.getBounds(), { padding: [25, 25], maxZoom: 15 });
                    }
                    if (typeof s.CurrentLatitude === "number" && typeof s.CurrentLongitude === "number") {
                        var point = [s.CurrentLatitude, s.CurrentLongitude];
                        if (bus) bus.setLatLng(point); else bus = L.marker(point, { icon: L.divIcon({ className: "tracking-bus", html: '<span aria-label="Simulated Bus">BUS</span>', iconSize: [42, 28], iconAnchor: [21, 14] }) }).addTo(map);
                    } else clearBus();
                } else { clearBus(); if (map) { map.remove(); map = null; path = null; stopMarkers = []; geometry = ""; } }
                stopped = !s.CanPoll;
            }
            function schedule() { clearTimeout(timer); if (!stopped && !document.hidden) timer = setTimeout(refresh, Math.min(30000, 10000 * (failures + 1))); }
            function refresh() {
                if (running || document.hidden) return; running = true;
                var abort = new AbortController(), timeout = setTimeout(function () { abort.abort(); }, 8000);
                fetch(panel.dataset.trackingUrl, { credentials: "same-origin", cache: "no-store", signal: abort.signal }).then(function (response) {
                    if ([401, 403, 404].indexOf(response.status) >= 0) { stopped = true; clearBus(); throw new Error("Tracking access ended. Return to your service page or sign in again."); }
                    if (!response.ok) throw new Error("Tracking could not refresh. The last displayed position is stale.");
                    return response.json();
                }).then(function (snapshot) { failures = 0; render(snapshot); }).catch(function (error) { failures++; message("stale", error.name === "AbortError" ? "Tracking refresh timed out. The displayed position is stale." : error.message); })
                    .finally(function () { clearTimeout(timeout); running = false; schedule(); });
            }
            get("refresh").addEventListener("click", refresh);
            document.addEventListener("visibilitychange", function () { if (document.hidden) clearTimeout(timer); else if (!stopped) refresh(); });
            refresh();
        });
    }
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", start); else start();
}());
