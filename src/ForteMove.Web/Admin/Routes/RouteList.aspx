<%@ Page Title="Route list" Language="C#" MasterPageFile="~/Admin/Admin.Master" AutoEventWireup="true" CodeBehind="RouteList.aspx.cs" Inherits="ForteMove.Web.Admin.Routes.RouteList" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <header class="page-heading page-heading-with-action">
        <div>
            <p class="section-kicker">Route network</p>
            <h1>Route list</h1>
            <p>Find permanent ForteMove service paths and review each ordered itinerary.</p>
        </div>
        <a class="btn btn-primary" href="<%= ResolveUrl("~/Admin/Routes/CreateRoute.aspx") %>">Create route</a>
    </header>

    <asp:Panel ID="pnlFlash" runat="server" CssClass="alert app-alert" role="status" Visible="false">
        <span class="alert-icon" aria-hidden="true">i</span>
        <asp:Label ID="lblFlash" runat="server" />
    </asp:Panel>

    <section class="app-panel filter-panel" aria-label="Route filters">
        <div class="row g-3 align-items-end">
            <div class="col-lg-7">
                <asp:Label ID="lblSearch" runat="server" AssociatedControlID="txtSearch" CssClass="form-label" Text="Search routes" />
                <asp:TextBox ID="txtSearch" runat="server" CssClass="form-control" MaxLength="100" placeholder="Route code, name, origin or destination" />
            </div>
            <div class="col-sm-7 col-lg-3">
                <asp:Label ID="lblStatus" runat="server" AssociatedControlID="ddlStatus" CssClass="form-label" Text="Route status" />
                <asp:DropDownList ID="ddlStatus" runat="server" CssClass="form-select">
                    <asp:ListItem Text="All statuses" Value="" />
                    <asp:ListItem Text="Active" Value="true" />
                    <asp:ListItem Text="Inactive" Value="false" />
                </asp:DropDownList>
            </div>
            <div class="col-sm-5 col-lg-2 filter-actions">
                <asp:Button ID="btnApplyFilters" runat="server" CssClass="btn btn-primary" Text="Apply" OnClick="btnApplyFilters_Click" />
                <asp:LinkButton ID="btnClearFilters" runat="server" CssClass="btn btn-link" Text="Clear" CausesValidation="false" OnClick="btnClearFilters_Click" />
            </div>
        </div>
    </section>

    <asp:Panel ID="pnlResults" runat="server" CssClass="app-panel route-panel" Visible="false">
        <div class="fleet-panel-heading">
            <div>
                <h2>Service routes</h2>
                <p><asp:Literal ID="litRouteCount" runat="server" /></p>
            </div>
        </div>
        <div class="table-responsive">
            <table class="table route-table align-middle mb-0">
                <thead>
                    <tr>
                        <th scope="col">Route</th>
                        <th scope="col">Journey</th>
                        <th scope="col">Stops</th>
                        <th scope="col">Distance</th>
                        <th scope="col">Duration</th>
                        <th scope="col">Fare</th>
                        <th scope="col">Status</th>
                        <th scope="col"><span class="visually-hidden">Actions</span></th>
                    </tr>
                </thead>
                <tbody>
                    <asp:Repeater ID="rptRoutes" runat="server">
                        <ItemTemplate>
                            <tr>
                                <td>
                                    <strong class="route-code"><%#: Eval("RouteCode") %></strong>
                                    <span class="cell-secondary"><%#: Eval("RouteName") %></span>
                                </td>
                                <td>
                                    <strong><%#: Eval("OriginStopName") %></strong>
                                    <span class="cell-tertiary">to <%#: Eval("DestinationStopName") %></span>
                                </td>
                                <td><%#: Eval("StopCount") %></td>
                                <td><%#: FormatDistance(Eval("EstimatedDistanceKm")) %></td>
                                <td><%#: FormatDuration(Eval("EstimatedDurationMinutes")) %></td>
                                <td><%#: FormatFare(Eval("DefaultFare")) %></td>
                                <td><span class='<%# GetStatusCss(Eval("IsActive")) %>'><%#: FormatStatus(Eval("IsActive")) %></span></td>
                                <td class="table-action"><asp:HyperLink runat="server" CssClass="btn btn-sm btn-outline-secondary" NavigateUrl='<%# GetDetailsUrl(Eval("RouteId")) %>' Text="View details" /></td>
                            </tr>
                        </ItemTemplate>
                    </asp:Repeater>
                </tbody>
            </table>
        </div>
    </asp:Panel>

    <asp:Panel ID="pnlEmpty" runat="server" CssClass="app-panel empty-state" Visible="false">
        <span class="empty-state-icon" aria-hidden="true">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" focusable="false"><circle cx="6" cy="18" r="2.25" /><circle cx="18" cy="6" r="2.25" /><path d="M7.8 16.6c2.7-1.8 1.2-5.6 4.2-7.2 1.1-.6 2.4-.8 4-.9" /></svg>
        </span>
        <h2><asp:Literal ID="litEmptyHeading" runat="server" /></h2>
        <p><asp:Literal ID="litEmptyMessage" runat="server" /></p>
        <asp:HyperLink ID="lnkEmptyAction" runat="server" NavigateUrl="~/Admin/Routes/CreateRoute.aspx" CssClass="btn btn-primary" Text="Create the first route" />
    </asp:Panel>
</asp:Content>
