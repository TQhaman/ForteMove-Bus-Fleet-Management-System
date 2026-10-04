<%@ Page Title="Schedule list" Language="C#" MasterPageFile="~/Admin/Admin.Master" AutoEventWireup="true" CodeBehind="ScheduleList.aspx.cs" Inherits="ForteMove.Web.Admin.Scheduling.ScheduleList" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <header class="page-heading page-heading-with-action">
        <div><p class="section-kicker">Scheduling</p><h1>Schedule list</h1><p>Review recurring service patterns and their generated Trips.</p></div>
        <a class="btn btn-primary" href="<%= ResolveUrl("~/Admin/Scheduling/CreateSchedule.aspx") %>">Create schedule</a>
    </header>
    <asp:Panel ID="pnlFlash" runat="server" CssClass="alert app-alert" role="status" Visible="false"><span class="alert-icon" aria-hidden="true">i</span><asp:Label ID="lblFlash" runat="server" /></asp:Panel>
    <section class="app-panel filter-panel" aria-label="Schedule filters">
        <div class="row g-3 align-items-end">
            <div class="col-lg-7"><asp:Label runat="server" AssociatedControlID="txtSearch" CssClass="form-label" Text="Search schedules" /><asp:TextBox ID="txtSearch" runat="server" CssClass="form-control" MaxLength="100" placeholder="Schedule code, Route code or Route name" /></div>
            <div class="col-sm-7 col-lg-3"><asp:Label runat="server" AssociatedControlID="ddlStatus" CssClass="form-label" Text="Schedule status" /><asp:DropDownList ID="ddlStatus" runat="server" CssClass="form-select"><asp:ListItem Text="All statuses" Value="" /><asp:ListItem Text="Active" Value="Active" /><asp:ListItem Text="Upcoming" Value="Upcoming" /><asp:ListItem Text="Ended" Value="Ended" /><asp:ListItem Text="Inactive" Value="Inactive" /></asp:DropDownList></div>
            <div class="col-sm-5 col-lg-2 filter-actions"><asp:Button ID="btnApplyFilters" runat="server" CssClass="btn btn-primary" Text="Apply" OnClick="btnApplyFilters_Click" /><asp:LinkButton ID="btnClearFilters" runat="server" CssClass="btn btn-link" Text="Clear" CausesValidation="false" OnClick="btnClearFilters_Click" /></div>
        </div>
    </section>
    <asp:Panel ID="pnlResults" runat="server" CssClass="app-panel route-panel" Visible="false">
        <div class="fleet-panel-heading"><div><h2>Recurring schedules</h2><p><asp:Literal ID="litCount" runat="server" /></p></div></div>
        <div class="table-responsive"><table class="table route-table align-middle mb-0"><thead><tr><th>Schedule</th><th>Route</th><th>Operating days</th><th>Departures</th><th>Effective period</th><th>Trips</th><th>Status</th><th><span class="visually-hidden">Actions</span></th></tr></thead><tbody>
            <asp:Repeater ID="rptSchedules" runat="server"><ItemTemplate><tr>
                <td><strong class="route-code"><%#: Eval("ScheduleCode") %></strong></td>
                <td><strong><%#: Eval("RouteName") %></strong><span class="cell-secondary"><%#: Eval("RouteCode") %></span></td>
                <td><%#: Eval("OperatingDaysDisplay") %></td><td><%#: Eval("DepartureTimesDisplay") %></td>
                <td><%#: FormatPeriod(Eval("EffectiveStartDate"), Eval("EffectiveEndDate")) %></td><td><%#: Eval("GeneratedTripCount") %></td>
                <td><span class='<%# GetStatusCss(Eval("Status")) %>'><%#: FormatStatus(Eval("Status")) %></span></td>
                <td class="table-action"><asp:HyperLink runat="server" CssClass="btn btn-sm btn-outline-secondary" NavigateUrl='<%# GetDetailsUrl(Eval("RouteScheduleId")) %>' Text="View details" /></td>
            </tr></ItemTemplate></asp:Repeater>
        </tbody></table></div>
    </asp:Panel>
    <asp:Panel ID="pnlEmpty" runat="server" CssClass="app-panel empty-state" Visible="false"><h2><asp:Literal ID="litEmptyHeading" runat="server" /></h2><p><asp:Literal ID="litEmptyMessage" runat="server" /></p><asp:HyperLink ID="lnkEmptyAction" runat="server" NavigateUrl="~/Admin/Scheduling/CreateSchedule.aspx" CssClass="btn btn-primary" Text="Create the first schedule" /></asp:Panel>
</asp:Content>
