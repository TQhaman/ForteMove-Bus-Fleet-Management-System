<%@ Page Title="Schedule details" Language="C#" MasterPageFile="~/Admin/Admin.Master" AutoEventWireup="true" CodeBehind="ScheduleDetails.aspx.cs" Inherits="ForteMove.Web.Admin.Scheduling.ScheduleDetailsPage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <asp:Panel ID="pnlNotFound" runat="server" CssClass="app-panel empty-state" Visible="false"><h1>Schedule not found</h1><p>The requested schedule is unavailable.</p><a class="btn btn-primary" href="<%= ResolveUrl("~/Admin/Scheduling/ScheduleList.aspx") %>">Return to schedules</a></asp:Panel>
    <asp:Panel ID="pnlDetails" runat="server" Visible="false">
        <header class="page-heading page-heading-with-action"><div><p class="section-kicker">Scheduling</p><h1><asp:Literal ID="litScheduleCode" runat="server" /></h1><p><asp:Literal ID="litRoute" runat="server" /></p></div><div class="heading-actions"><asp:HyperLink ID="lnkChange" runat="server" CssClass="btn btn-primary" Text="Change schedule" /><a class="btn btn-outline-secondary" href="<%= ResolveUrl("~/Admin/Scheduling/ScheduleList.aspx") %>">Back to list</a></div></header>
        <asp:Panel ID="pnlFlash" runat="server" CssClass="alert app-alert" role="status" Visible="false"><span class="alert-icon" aria-hidden="true">i</span><asp:Label ID="lblFlash" runat="server" /></asp:Panel>
        <div class="details-grid">
            <section class="app-panel details-panel"><div class="panel-heading"><div><p class="section-kicker">Current service pattern</p><h2>Schedule overview</h2></div><asp:Label ID="lblStatus" runat="server" /></div>
                <dl class="detail-list"><div><dt>Journey</dt><dd><asp:Literal ID="litJourney" runat="server" /></dd></div><div><dt>Operating days</dt><dd><asp:Literal ID="litDays" runat="server" /></dd></div><div><dt>Departure times</dt><dd><asp:Literal ID="litTimes" runat="server" /></dd></div><div><dt>Effective period</dt><dd><asp:Literal ID="litPeriod" runat="server" /></dd></div><div><dt>Preferred bus category</dt><dd><asp:Literal ID="litCategory" runat="server" /></dd></div><div><dt>Expected capacity</dt><dd><asp:Literal ID="litCapacity" runat="server" /></dd></div><div><dt>Generated Trips</dt><dd><asp:Literal ID="litTripCount" runat="server" /></dd></div></dl>
            </section>
            <section class="app-panel details-panel"><div class="panel-heading"><div><p class="section-kicker">Previous patterns</p><h2>Change history</h2></div></div>
                <asp:Panel ID="pnlNoHistory" runat="server" CssClass="compact-empty">No schedule changes have been applied.</asp:Panel>
                <asp:Repeater ID="rptHistory" runat="server"><HeaderTemplate><div class="history-list"></HeaderTemplate><ItemTemplate><article class="history-item"><strong><%#: FormatHistoryPeriod(Eval("EffectiveStartDate"), Eval("SupersededFromDate"), Eval("EffectiveEndDate")) %></strong><span><%#: Eval("OperatingDaysDisplay") %></span><span><%#: Eval("DepartureTimesDisplay") %></span></article></ItemTemplate><FooterTemplate></div></FooterTemplate></asp:Repeater>
            </section>
        </div>
    </asp:Panel>
</asp:Content>
