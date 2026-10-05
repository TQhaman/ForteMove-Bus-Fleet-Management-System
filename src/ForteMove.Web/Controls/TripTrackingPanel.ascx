<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="TripTrackingPanel.ascx.cs" Inherits="ForteMove.Web.Controls.TripTrackingPanel" %>
<section id="trackingPanel" runat="server" class="app-panel tracking-panel" aria-label="Simulated tracking">
    <div class="panel-heading"><div><p class="section-kicker">Simulated tracking</p><h2>Service progress</h2><p>Position is estimated between route stops using the recorded Trip timeline.</p></div></div>
    <p data-tracking="status" role="status">Loading service information...</p>
    <p data-tracking="availability" class="alert alert-warning" hidden></p>
    <p data-tracking="attention" class="alert alert-warning" hidden></p>
    <p data-tracking="stale" class="alert alert-warning" hidden></p>
    <p data-tracking="tiles" class="alert alert-info" hidden>Map background unavailable. The simulated route and progress remain available.</p>
    <div data-tracking="map" class="tracking-map" aria-label="Route map with simulated position"></div>
    <dl class="tracking-summary"><div><dt>Bus</dt><dd data-tracking="fleet">Pending</dd></div><div><dt>Simulated progress</dt><dd data-tracking="progress">Unavailable</dd></div><div><dt>Current segment from</dt><dd data-tracking="current">-</dd></div><div><dt>Next stop</dt><dd data-tracking="next">-</dd></div></dl>
    <p data-tracking="context"></p><ol data-tracking="stops" class="tracking-itinerary"></ol>
    <p class="tracking-disclaimer">Simulated tracking uses the Route's currently stored Stop coordinates. Corrections also change historical and Completed Trip maps. This display is not historical GPS evidence. The line connects Stops and does not represent road routing.</p>
    <p class="cell-secondary">Last refreshed (South Africa): <span data-tracking="refreshed">Waiting for the server</span></p>
    <button type="button" data-tracking="refresh" class="btn btn-outline-primary">Refresh tracking</button>
</section>
