<%@ Page Title="Dashboard" Language="C#" MasterPageFile="~/Admin/Admin.Master" AutoEventWireup="true" CodeBehind="Dashboard.aspx.cs" Inherits="ForteMove.Web.Admin.Dashboard" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <header class="page-heading"><div><p class="section-kicker">Operations overview</p><h1>Good to see you, <asp:Literal ID="litFirstName" runat="server" />.</h1><p>Monitor the current fleet and scheduled network, then move directly to operational work.</p></div></header>
    <div class="dashboard-overview-grid">
        <section class="app-panel fleet-status-panel" aria-labelledby="fleet-status-title">
            <div class="panel-heading"><div><p class="section-kicker">Fleet status</p><h2 id="fleet-status-title">Total fleet: <asp:Literal ID="litTotalFleet" runat="server" /></h2></div></div>
            <div class="fleet-status-bars">
                <div class="fleet-status-row"><div class="fleet-status-label"><span>Operational</span><strong><asp:Literal ID="litOperationalFleet" runat="server" /></strong></div><div class="status-track"><div ID="barOperational" runat="server" class="status-fill status-fill-success"></div></div></div>
                <div class="fleet-status-row"><div class="fleet-status-label"><span>Out of service</span><strong><asp:Literal ID="litOutOfServiceFleet" runat="server" /></strong></div><div class="status-track"><div ID="barOutOfService" runat="server" class="status-fill status-fill-danger"></div></div></div>
                <div class="fleet-status-row"><div class="fleet-status-label"><span>Under maintenance</span><strong><asp:Literal ID="litUnderMaintenanceFleet" runat="server" /></strong></div><div class="status-track"><div ID="barUnderMaintenance" runat="server" class="status-fill status-fill-warning"></div></div></div>
                <asp:Panel ID="pnlRetired" runat="server" CssClass="fleet-status-row" Visible="false"><div class="fleet-status-label"><span>Retired</span><strong><asp:Literal ID="litRetiredFleet" runat="server" /></strong></div><div class="status-track"><div ID="barRetired" runat="server" class="status-fill status-fill-neutral"></div></div></asp:Panel>
            </div>
        </section>
        <section class="app-panel operations-metrics-panel" aria-labelledby="network-status-title"><div class="panel-heading"><div><p class="section-kicker">Network and service</p><h2 id="network-status-title">Current operations</h2></div></div><div class="compact-metric-grid">
            <article><span>Active Routes</span><strong><asp:Literal ID="litActiveRoutes" runat="server" /></strong></article>
            <article><span>Active Schedules</span><strong><asp:Literal ID="litActiveSchedules" runat="server" /></strong></article>
            <article><span>Today's Trips</span><strong><asp:Literal ID="litTodaysTrips" runat="server" /></strong></article>
            <article><span>Unassigned Trips</span><strong><asp:Literal ID="litUnassignedTrips" runat="server" /></strong></article>
            <article><span>Available Drivers</span><strong><asp:Literal ID="litAvailableDrivers" runat="server" /></strong></article>
            <article><span>Upcoming Scheduled Trips</span><strong><asp:Literal ID="litScheduledTrips" runat="server" /></strong></article>
        </div></section>
    </div>
    <section class="app-panel mt-4" aria-labelledby="live-operations-title">
        <div class="panel-heading"><div><p class="section-kicker">Live operations</p><h2 id="live-operations-title">Trip attention</h2></div><a class="btn btn-outline-primary btn-sm" href="<%= ResolveUrl("~/Admin/Operations/Exceptions.aspx") %>">View exceptions</a></div>
        <div class="compact-metric-grid">
            <article><span>Ready</span><strong><asp:Literal ID="litReadyTrips" runat="server" /></strong></article>
            <article><span>In progress</span><strong><asp:Literal ID="litInProgressTrips" runat="server" /></strong></article>
            <article><span>Delayed</span><strong><asp:Literal ID="litDelayedTrips" runat="server" /></strong></article>
            <article><span>Cannot proceed</span><strong><asp:Literal ID="litOpenCannotProceed" runat="server" /></strong></article>
            <article><span>Critical defects</span><strong><asp:Literal ID="litCriticalDefects" runat="server" /></strong></article>
        </div>
    </section>
    <section class="app-panel mt-4"><div class="panel-heading"><h2>Maintenance attention</h2><a class="btn btn-outline-primary btn-sm" href="Maintenance/Overview.aspx">View maintenance</a></div><div class="compact-metric-grid"><article><span>Buses with overdue service</span><strong><asp:Literal ID="litOverdueMaintenance" runat="server" /></strong></article><article><span>Open work orders</span><strong><asp:Literal ID="litOpenMaintenance" runat="server" /></strong></article></div></section>
<section aria-labelledby="quick-actions-title" class="mt-4"><div class="section-heading-row"><div><p class="section-kicker">Quick actions</p><h2 id="quick-actions-title">Manage operations</h2></div></div><div class="action-grid action-grid-three">
        <a class="action-card" href="<%= ResolveUrl("~/Admin/RegisterBus.aspx") %>"><span class="action-card-copy"><strong>Register a bus</strong><span>Add a vehicle to the fleet register.</span></span><span class="action-arrow" aria-hidden="true">&gt;</span></a>
        <a class="action-card" href="<%= ResolveUrl("~/Admin/Routes/CreateRoute.aspx") %>"><span class="action-card-copy"><strong>Create a Route</strong><span>Build an ordered service path.</span></span><span class="action-arrow" aria-hidden="true">&gt;</span></a>
        <a class="action-card" href="<%= ResolveUrl("~/Admin/Scheduling/CreateSchedule.aspx") %>"><span class="action-card-copy"><strong>Create a Schedule</strong><span>Generate recurring future Trips.</span></span><span class="action-arrow" aria-hidden="true">&gt;</span></a>
        <a class="action-card" href="<%= ResolveUrl("~/Admin/FleetList.aspx") %>"><span class="action-card-copy"><strong>Fleet list</strong><span>Review vehicles and compliance.</span></span><span class="action-arrow" aria-hidden="true">&gt;</span></a>
        <a class="action-card" href="<%= ResolveUrl("~/Admin/Scheduling/ScheduleList.aspx") %>"><span class="action-card-copy"><strong>Schedule list</strong><span>Review recurring service patterns.</span></span><span class="action-arrow" aria-hidden="true">&gt;</span></a>
        <a class="action-card" href="<%= ResolveUrl("~/Admin/Scheduling/TripList.aspx") %>"><span class="action-card-copy"><strong>Trips</strong><span>Review generated dated services.</span></span><span class="action-arrow" aria-hidden="true">&gt;</span></a>
        <a class="action-card" href="<%= ResolveUrl("~/Admin/Drivers/DriverList.aspx") %>"><span class="action-card-copy"><strong>Drivers</strong><span>Review availability and credentials.</span></span><span class="action-arrow" aria-hidden="true">&gt;</span></a>
        <a class="action-card" href="<%= ResolveUrl("~/Admin/Assignments/AssignmentQueue.aspx") %>"><span class="action-card-copy"><strong>Assignment queue</strong><span>Review Driver and bus recommendations.</span></span><span class="action-arrow" aria-hidden="true">&gt;</span></a>
        <a class="action-card" href="<%= ResolveUrl("~/Admin/Operations/Defects.aspx") %>"><span class="action-card-copy"><strong>Defect reports</strong><span>Review vehicle safety reports.</span></span><span class="action-arrow" aria-hidden="true">&gt;</span></a>
    </div></section>
</asp:Content>
