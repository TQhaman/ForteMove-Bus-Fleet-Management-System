<%@ Page Title="Route details" Language="C#" MasterPageFile="~/Admin/Admin.Master" AutoEventWireup="true" CodeBehind="RouteDetails.aspx.cs" Inherits="ForteMove.Web.Admin.Routes.RouteDetailsPage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <asp:Panel ID="pnlDetails" runat="server" Visible="false">
        <header class="page-heading page-heading-with-action">
            <div>
                <p class="section-kicker">Route network</p>
                <h1><asp:Literal ID="litRouteCode" runat="server" /> - <asp:Literal ID="litRouteName" runat="server" /></h1>
                <p>Review the service path and its ordered stop itinerary.</p>
            </div>
            <a class="btn btn-outline-secondary" href="<%= ResolveUrl("~/Admin/Routes/RouteList.aspx") %>">Back to route list</a>
        </header>

        <section class="app-panel route-summary" aria-labelledby="route-summary-title">
            <div class="route-summary-heading">
                <div>
                    <p class="section-kicker">Journey</p>
                    <h2 id="route-summary-title">
                        <asp:Literal ID="litOrigin" runat="server" />
                        <svg class="journey-arrow" viewBox="0 0 28 16" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><path d="M2 8h23M19 2l6 6-6 6" /></svg>
                        <asp:Literal ID="litDestination" runat="server" />
                    </h2>
                </div>
                <asp:Label ID="lblRouteStatus" runat="server" />
            </div>
            <dl class="route-summary-grid">
                <div><dt>Distance</dt><dd><asp:Literal ID="litDistance" runat="server" /></dd></div>
                <div><dt>Estimated duration</dt><dd><asp:Literal ID="litDuration" runat="server" /></dd></div>
                <div><dt>Default fare</dt><dd><asp:Literal ID="litFare" runat="server" /></dd></div>
                <div><dt>Stops</dt><dd><asp:Literal ID="litStopCount" runat="server" /></dd></div>
            </dl>
        </section>

        <section class="app-panel itinerary-panel mt-4" aria-labelledby="itinerary-title">
            <div class="panel-heading itinerary-heading">
                <div>
                    <h2 id="itinerary-title">Stop itinerary</h2>
                    <p>Stops are shown in the order travelled from origin to destination.</p>
                </div>
            </div>
            <ol class="route-itinerary">
                <asp:Repeater ID="rptStops" runat="server">
                    <ItemTemplate>
                        <li class="route-itinerary-item">
                            <span class="itinerary-order" aria-hidden="true"><%#: Eval("StopOrder") %></span>
                            <span class="itinerary-copy">
                                <strong><%#: Eval("StopName") %></strong>
                                <span class="route-stop-meta"><span><%#: Eval("StopCode") %></span><span><%#: Eval("Area") %></span></span>
                            </span>
                            <asp:PlaceHolder runat="server" Visible='<%# Eval("EstimatedMinutesFromOrigin") != null %>'>
                                <span class="itinerary-time"><%#: FormatMinutesFromOrigin(Eval("EstimatedMinutesFromOrigin")) %></span>
                            </asp:PlaceHolder>
                            <span class="route-stop-meta"><%#: (bool)Eval("HasCoordinates") ? "Coordinates available" : "Coordinates missing" %>
                                <a href='<%# ResolveUrl("~/Admin/Routes/StopCoordinates.aspx?id=" + Eval("StopId")) %>'>Update coordinates</a>
                            </span>
                        </li>
                    </ItemTemplate>
                </asp:Repeater>
            </ol>
        </section>
    </asp:Panel>

    <asp:Panel ID="pnlNotFound" runat="server" CssClass="app-panel empty-state" Visible="false">
        <span class="empty-state-icon" aria-hidden="true">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" focusable="false"><circle cx="6" cy="18" r="2.25" /><circle cx="18" cy="6" r="2.25" /><path d="M7.8 16.6c2.7-1.8 1.2-5.6 4.2-7.2 1.1-.6 2.4-.8 4-.9" /></svg>
        </span>
        <h1>Route not found</h1>
        <p>The requested route is unavailable or the link is no longer valid.</p>
        <a class="btn btn-primary" href="<%= ResolveUrl("~/Admin/Routes/RouteList.aspx") %>">Return to route list</a>
    </asp:Panel>
</asp:Content>
