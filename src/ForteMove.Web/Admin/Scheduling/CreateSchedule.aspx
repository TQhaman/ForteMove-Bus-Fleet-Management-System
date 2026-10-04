<%@ Page Title="Create schedule" Language="C#" MasterPageFile="~/Admin/Admin.Master" AutoEventWireup="true" CodeBehind="CreateSchedule.aspx.cs" Inherits="ForteMove.Web.Admin.Scheduling.CreateSchedule" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <header class="page-heading page-heading-with-action"><div><p class="section-kicker">Scheduling</p><h1>Create schedule</h1><p>Define a repeating service pattern and review its future Trips before saving.</p></div><a class="btn btn-outline-secondary" href="<%= ResolveUrl("~/Admin/Scheduling/ScheduleList.aspx") %>">View schedules</a></header>
    <asp:Panel ID="pnlErrors" runat="server" CssClass="alert alert-danger validation-summary" role="alert" Visible="false" tabindex="-1"><div class="validation-summary-heading"><span class="alert-icon" aria-hidden="true">!</span><strong>Review the schedule information</strong></div><ul><asp:Repeater ID="rptErrors" runat="server"><ItemTemplate><li><%#: Eval("Message") %></li></ItemTemplate></asp:Repeater></ul></asp:Panel>
    <div class="form-layout schedule-builder-layout">
        <section class="app-panel form-panel" aria-labelledby="schedule-pattern-heading">
            <div class="panel-heading"><span class="panel-step">01</span><div><h2 id="schedule-pattern-heading">Service pattern</h2><p>Choose the Route, operating days and departure times.</p></div></div>
            <div class="row g-4">
                <div class="col-md-3"><span class="form-label d-block">Schedule code</span><strong class="generated-code"><asp:Literal ID="litScheduleCode" runat="server" /></strong><div class="form-text">Assigned automatically when saved.</div></div>
                <div class="col-md-9"><asp:Label runat="server" AssociatedControlID="ddlRoute" CssClass="form-label required-label" Text="Route" /><asp:DropDownList ID="ddlRoute" runat="server" CssClass="form-select" /></div>
                <div class="col-12"><span class="form-label required-label">Operating days</span><asp:CheckBoxList ID="cblOperatingDays" runat="server" CssClass="day-selector" RepeatDirection="Horizontal" RepeatLayout="Flow"><asp:ListItem Text="Monday" Value="1" /><asp:ListItem Text="Tuesday" Value="2" /><asp:ListItem Text="Wednesday" Value="3" /><asp:ListItem Text="Thursday" Value="4" /><asp:ListItem Text="Friday" Value="5" /><asp:ListItem Text="Saturday" Value="6" /><asp:ListItem Text="Sunday" Value="0" /></asp:CheckBoxList></div>
                <div class="col-12"><span class="form-label required-label">Departure times</span>
                    <asp:Panel ID="pnlNoDepartures" runat="server" CssClass="compact-empty">No departure times added yet.</asp:Panel>
                    <asp:Repeater ID="rptDepartures" runat="server" OnItemCommand="rptDepartures_ItemCommand"><HeaderTemplate><div class="departure-chip-list"></HeaderTemplate><ItemTemplate><span class="departure-chip"><strong><%#: Eval("Display") %></strong><asp:LinkButton runat="server" CommandName="Remove" CommandArgument='<%# Eval("Value") %>' Text="Remove" CssClass="btn btn-link btn-sm" CausesValidation="false" /></span></ItemTemplate><FooterTemplate></div></FooterTemplate></asp:Repeater>
                    <div class="inline-input-row"><asp:TextBox ID="txtDepartureTime" runat="server" CssClass="form-control" TextMode="Time" step="60" /><asp:LinkButton ID="btnAddDeparture" runat="server" CssClass="btn btn-outline-primary" Text="Add departure" CausesValidation="false" OnClick="btnAddDeparture_Click" /></div>
                </div>
            </div>
        </section>
        <section class="app-panel form-panel" aria-labelledby="effective-period-heading">
            <div class="panel-heading"><span class="panel-step">02</span><div><h2 id="effective-period-heading">Effective period and capacity</h2><p>Set when the recurring service operates.</p></div></div>
            <div class="row g-4">
                <div class="col-md-6"><asp:Label runat="server" AssociatedControlID="txtStartDate" CssClass="form-label required-label" Text="Effective start date" /><asp:TextBox ID="txtStartDate" runat="server" CssClass="form-control" TextMode="Date" /></div>
                <div class="col-md-6"><asp:Label runat="server" AssociatedControlID="txtEndDate" CssClass="form-label required-label" Text="Effective end date" /><asp:TextBox ID="txtEndDate" runat="server" CssClass="form-control" TextMode="Date" /></div>
                <div class="col-md-6"><asp:Label runat="server" AssociatedControlID="ddlBusCategory" CssClass="form-label" Text="Preferred bus category" /><asp:DropDownList ID="ddlBusCategory" runat="server" CssClass="form-select" /></div>
                <div class="col-md-6"><asp:Label runat="server" AssociatedControlID="txtExpectedCapacity" CssClass="form-label" Text="Expected capacity" /><asp:TextBox ID="txtExpectedCapacity" runat="server" CssClass="form-control" TextMode="Number" min="1" step="1" inputmode="numeric" /></div>
            </div>
        </section>
        <asp:Panel ID="pnlPreview" runat="server" CssClass="app-panel preview-panel" Visible="false" role="status">
            <p class="section-kicker">Schedule preview</p><h2>This Schedule will generate <asp:Literal ID="litPreviewCount" runat="server" /> Trips</h2><p><asp:Literal ID="litPreviewRange" runat="server" /></p><p class="form-text">Only future departures are included when the schedule starts today.</p>
        </asp:Panel>
        <div class="form-actions"><a class="btn btn-outline-secondary" href="<%= ResolveUrl("~/Admin/Scheduling/ScheduleList.aspx") %>">Cancel</a><asp:Button ID="btnPreview" runat="server" CssClass="btn btn-outline-primary" Text="Review schedule" OnClick="btnPreview_Click" /><asp:Button ID="btnCreate" runat="server" CssClass="btn btn-primary" Text="Create schedule" Enabled="false" OnClick="btnCreate_Click" /></div>
    </div>
</asp:Content>
