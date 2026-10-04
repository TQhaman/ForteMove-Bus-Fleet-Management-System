<%@ Page Title="Dashboard" Language="C#" MasterPageFile="~/Admin/Admin.Master" AutoEventWireup="true" CodeBehind="Dashboard.aspx.cs" Inherits="ForteMove.Web.Admin.Dashboard" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <header class="page-heading">
        <div>
            <p class="section-kicker">Operations overview</p>
            <h1>Good to see you, <asp:Literal ID="litFirstName" runat="server" />.</h1>
            <p>Monitor the current fleet and route network, then move directly to the work that needs your attention.</p>
        </div>
    </header>

    <section aria-labelledby="operations-summary-title">
        <div class="section-heading-row">
            <div>
                <p class="section-kicker">Current position</p>
                <h2 id="operations-summary-title">Operations summary</h2>
            </div>
        </div>
        <div class="metric-grid">
            <article class="metric-card app-panel">
                <span class="metric-label">Total fleet</span>
                <strong class="metric-value"><asp:Literal ID="litTotalFleet" runat="server" /></strong>
            </article>
            <article class="metric-card app-panel metric-card-success">
                <span class="metric-label">Operational</span>
                <strong class="metric-value"><asp:Literal ID="litOperationalFleet" runat="server" /></strong>
            </article>
            <article class="metric-card app-panel metric-card-danger">
                <span class="metric-label">Out of service</span>
                <strong class="metric-value"><asp:Literal ID="litOutOfServiceFleet" runat="server" /></strong>
            </article>
            <article class="metric-card app-panel metric-card-warning">
                <span class="metric-label">Under maintenance</span>
                <strong class="metric-value"><asp:Literal ID="litUnderMaintenanceFleet" runat="server" /></strong>
            </article>
            <article class="metric-card app-panel metric-card-route">
                <span class="metric-label">Active routes</span>
                <strong class="metric-value"><asp:Literal ID="litActiveRoutes" runat="server" /></strong>
            </article>
        </div>
    </section>

    <section aria-labelledby="quick-actions-title" class="mt-4">
        <div class="section-heading-row">
            <div>
                <p class="section-kicker">Quick actions</p>
                <h2 id="quick-actions-title">Manage operations</h2>
            </div>
        </div>
        <div class="action-grid action-grid-four">
            <a class="action-card" href="<%= ResolveUrl("~/Admin/RegisterBus.aspx") %>">
                <span class="action-card-icon" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" focusable="false"><path d="M4 16V8.5A2.5 2.5 0 0 1 6.5 6h7A2.5 2.5 0 0 1 16 8.5V16" /><path d="M4 12h12M6.5 16v2M13.5 16v2M4 16h12" /><path d="M19 5v6M16 8h6" /></svg></span>
                <span class="action-card-copy"><strong>Register a bus</strong><span>Add a vehicle and capture its operational details.</span></span>
                <svg class="action-arrow" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><path d="m9 18 6-6-6-6" /></svg>
            </a>
            <a class="action-card" href="<%= ResolveUrl("~/Admin/FleetList.aspx") %>">
                <span class="action-card-icon" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" focusable="false"><rect x="3" y="5" width="13" height="12" rx="2" /><path d="M3 11h13M6 17v2M13 17v2M19 7h2M19 11h2M19 15h2" /></svg></span>
                <span class="action-card-copy"><strong>Open fleet list</strong><span>Review registered buses, compliance and vehicle status.</span></span>
                <svg class="action-arrow" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><path d="m9 18 6-6-6-6" /></svg>
            </a>
            <a class="action-card" href="<%= ResolveUrl("~/Admin/Routes/CreateRoute.aspx") %>">
                <span class="action-card-icon" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" focusable="false"><circle cx="5.5" cy="18" r="2" /><circle cx="15" cy="7" r="2" /><path d="M7 16.6c2.2-1.8 1-4.7 3.4-6.1.8-.5 1.7-.7 2.7-.9M19 14v6M16 17h6" /></svg></span>
                <span class="action-card-copy"><strong>Create a route</strong><span>Build a permanent service path from an ordered stop list.</span></span>
                <svg class="action-arrow" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><path d="m9 18 6-6-6-6" /></svg>
            </a>
            <a class="action-card" href="<%= ResolveUrl("~/Admin/Routes/RouteList.aspx") %>">
                <span class="action-card-icon" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" focusable="false"><circle cx="6" cy="18" r="2.25" /><circle cx="18" cy="6" r="2.25" /><path d="M7.8 16.6c2.7-1.8 1.2-5.6 4.2-7.2 1.1-.6 2.4-.8 4-.9" /></svg></span>
                <span class="action-card-copy"><strong>Open route list</strong><span>Find active routes and review their full itineraries.</span></span>
                <svg class="action-arrow" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><path d="m9 18 6-6-6-6" /></svg>
            </a>
        </div>
    </section>
</asp:Content>
