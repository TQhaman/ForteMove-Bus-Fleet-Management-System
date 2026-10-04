<%@ Page Title="Fleet list" Language="C#" MasterPageFile="~/Admin/Admin.Master" AutoEventWireup="true" CodeBehind="FleetList.aspx.cs" Inherits="ForteMove.Web.Admin.FleetList" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <header class="page-heading page-heading-with-action">
        <div>
            <p class="section-kicker">Fleet register</p>
            <h1>Fleet list</h1>
            <p>Find registered buses and review their compliance and vehicle status.</p>
        </div>
        <a class="btn btn-primary" href="<%= ResolveUrl("~/Admin/RegisterBus.aspx") %>">Register bus</a>
    </header>

    <asp:Panel ID="pnlFlash" runat="server" CssClass="alert app-alert" role="status" Visible="false">
        <span class="alert-icon" aria-hidden="true"><asp:Literal ID="litFlashIcon" runat="server" /></span>
        <asp:Label ID="lblFlash" runat="server" />
    </asp:Panel>

    <section class="app-panel filter-panel" aria-label="Fleet filters">
        <div class="row g-3 align-items-end">
            <div class="col-lg-7">
                <asp:Label ID="lblSearch" runat="server" AssociatedControlID="txtSearch" CssClass="form-label" Text="Search fleet" />
                <asp:TextBox ID="txtSearch" runat="server" CssClass="form-control" MaxLength="100" placeholder="Fleet number, registration, VIN, make or model" />
            </div>
            <div class="col-sm-7 col-lg-3">
                <asp:Label ID="lblState" runat="server" AssociatedControlID="ddlState" CssClass="form-label" Text="Vehicle status" />
                <asp:DropDownList ID="ddlState" runat="server" CssClass="form-select">
                    <asp:ListItem Text="All statuses" Value="" />
                    <asp:ListItem Text="Operational" Value="Operational" />
                    <asp:ListItem Text="Out of service" Value="OutOfService" />
                    <asp:ListItem Text="Under maintenance" Value="UnderMaintenance" />
                    <asp:ListItem Text="Retired" Value="Retired" />
                </asp:DropDownList>
            </div>
            <div class="col-sm-5 col-lg-2 filter-actions">
                <asp:Button ID="btnApplyFilters" runat="server" CssClass="btn btn-primary" Text="Apply" OnClick="btnApplyFilters_Click" />
                <asp:LinkButton ID="btnClearFilters" runat="server" CssClass="btn btn-link" Text="Clear" CausesValidation="false" OnClick="btnClearFilters_Click" />
            </div>
        </div>
    </section>

    <asp:Panel ID="pnlResults" runat="server" CssClass="app-panel fleet-panel" Visible="false">
        <div class="fleet-panel-heading">
            <div>
                <h2>Registered fleet</h2>
                <p><asp:Literal ID="litFleetCount" runat="server" /></p>
            </div>
        </div>

        <div class="table-responsive">
            <table class="table fleet-table align-middle mb-0">
                <thead>
                    <tr>
                        <th scope="col">Bus</th>
                        <th scope="col">Vehicle</th>
                        <th scope="col">Configuration</th>
                        <th scope="col">Odometer</th>
                        <th scope="col">Compliance</th>
                        <th scope="col">Status</th>
                    </tr>
                </thead>
                <tbody>
                    <asp:Repeater ID="rptFleet" runat="server">
                        <ItemTemplate>
                            <tr>
                                <td>
                                    <strong class="fleet-number"><%#: Eval("FleetNumber") %></strong>
                                    <span class="cell-secondary"><%#: Eval("RegistrationNumber") %></span>
                                    <span class="cell-tertiary">VIN <%#: Eval("Vin") %></span>
                                </td>
                                <td>
                                    <strong><%#: Eval("Make") %> <%#: Eval("Model") %></strong>
                                    <span class="cell-secondary"><%#: Eval("ManufactureYear") %></span>
                                </td>
                                <td>
                                    <span><%#: Eval("CategoryName") %></span>
                                    <span class="cell-secondary"><%#: Eval("PassengerCapacity") %> passengers</span>
                                    <span class="cell-tertiary"><%#: Eval("PropulsionName") %></span>
                                    <span class="cell-tertiary"><%#: FormatGvm(Container.DataItem) %></span>
                                </td>
                                <td><%#: FormatOdometer(Eval("OdometerKilometres")) %></td>
                                <td>
                                    <span class='<%# GetComplianceCss(Eval("ComplianceStatus")) %>'><%#: FormatCompliance(Eval("ComplianceStatus")) %></span>
                                    <span class="cell-tertiary compliance-dates"><%#: FormatComplianceDates(Container.DataItem) %></span>
                                </td>
                                <td>
                                    <span class='<%# GetStateCss(Eval("BaseOperationalState")) %>'><%#: FormatState(Eval("BaseOperationalState")) %></span>
                                    <span class="cell-tertiary"><a href='<%# ResolveUrl("~/Admin/FleetDetails.aspx?id=" + Eval("BusId")) %>'>View details</a></span>
                                </td>
                            </tr>
                        </ItemTemplate>
                    </asp:Repeater>
                </tbody>
            </table>
        </div>
    </asp:Panel>

    <asp:Panel ID="pnlEmpty" runat="server" CssClass="app-panel empty-state" Visible="false">
        <span class="empty-state-icon" aria-hidden="true">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" focusable="false"><rect x="3" y="5" width="13" height="12" rx="2" /><path d="M3 11h13M6 17v2M13 17v2M19 7h2M19 11h2M19 15h2" /></svg>
        </span>
        <h2><asp:Literal ID="litEmptyHeading" runat="server" /></h2>
        <p><asp:Literal ID="litEmptyMessage" runat="server" /></p>
        <asp:HyperLink ID="lnkEmptyAction" runat="server" NavigateUrl="~/Admin/RegisterBus.aspx" CssClass="btn btn-primary" Text="Register the first bus" />
    </asp:Panel>
</asp:Content>
