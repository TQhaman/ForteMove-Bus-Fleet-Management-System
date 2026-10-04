<%@ Page Title="Create route" Language="C#" MasterPageFile="~/Admin/Admin.Master" AutoEventWireup="true" CodeBehind="CreateRoute.aspx.cs" Inherits="ForteMove.Web.Admin.Routes.CreateRoute" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <header class="page-heading page-heading-with-action">
        <div>
            <p class="section-kicker">Route network</p>
            <h1>Create route</h1>
            <p>Define the service path, then arrange its stops in the order travelled.</p>
        </div>
        <a class="btn btn-outline-secondary" href="<%= ResolveUrl("~/Admin/Routes/RouteList.aspx") %>">View route list</a>
    </header>

    <asp:Panel ID="pnlErrors" runat="server" CssClass="alert alert-danger validation-summary" role="alert" Visible="false" tabindex="-1">
        <div class="validation-summary-heading"><span class="alert-icon" aria-hidden="true">!</span><strong>Review the highlighted information</strong></div>
        <ul><asp:Repeater ID="rptErrors" runat="server"><ItemTemplate><li><%#: Eval("Message") %></li></ItemTemplate></asp:Repeater></ul>
    </asp:Panel>

    <div class="form-layout route-builder-layout">
        <section class="app-panel form-panel" aria-labelledby="route-information-heading">
            <div class="panel-heading">
                <span class="panel-step">01</span>
                <div><h2 id="route-information-heading">Route information</h2><p>Record the permanent service path details.</p></div>
            </div>
            <div class="row g-4">
                <div class="col-md-3">
                    <span class="form-label d-block">Route code</span>
                    <strong class="generated-code"><asp:Literal ID="litRouteCode" runat="server" /></strong>
                    <div class="form-text">Assigned automatically when the route is saved.</div>
                </div>
                <div class="col-md-9">
                    <asp:Label ID="lblRouteName" runat="server" AssociatedControlID="txtRouteName" CssClass="form-label required-label" Text="Route name" />
                    <asp:TextBox ID="txtRouteName" runat="server" CssClass="form-control" MaxLength="150" autocomplete="off" />
                </div>
                <div class="col-md-4">
                    <asp:Label ID="lblDistance" runat="server" AssociatedControlID="txtDistance" CssClass="form-label required-label" Text="Estimated distance" />
                    <div class="input-group"><asp:TextBox ID="txtDistance" runat="server" CssClass="form-control" TextMode="Number" min="0.01" step="0.01" inputmode="decimal" /><span class="input-group-text">km</span></div>
                </div>
                <div class="col-md-4">
                    <asp:Label ID="lblDuration" runat="server" AssociatedControlID="txtDuration" CssClass="form-label required-label" Text="Estimated duration" />
                    <div class="input-group"><asp:TextBox ID="txtDuration" runat="server" CssClass="form-control" TextMode="Number" min="1" step="1" inputmode="numeric" /><span class="input-group-text">minutes</span></div>
                </div>
                <div class="col-md-4">
                    <asp:Label ID="lblFare" runat="server" AssociatedControlID="txtFare" CssClass="form-label required-label" Text="Default fare" />
                    <div class="input-group"><span class="input-group-text">R</span><asp:TextBox ID="txtFare" runat="server" CssClass="form-control" TextMode="Number" min="0" step="0.01" inputmode="decimal" /></div>
                </div>
            </div>
        </section>

        <section id="route-stops-panel" class="app-panel form-panel" aria-labelledby="route-stops-heading">
            <div class="panel-heading">
                <span class="panel-step">02</span>
                <div><h2 id="route-stops-heading">Route stops</h2><p>Add at least two stops and arrange them from origin to destination.</p></div>
            </div>

            <asp:Panel ID="pnlStopDraftErrors" runat="server" CssClass="alert alert-danger validation-summary" role="alert" Visible="false" tabindex="-1">
                <div class="validation-summary-heading"><span class="alert-icon" aria-hidden="true">!</span><strong>Review the new stop</strong></div>
                <ul><asp:Repeater ID="rptStopDraftErrors" runat="server"><ItemTemplate><li><%#: Eval("Message") %></li></ItemTemplate></asp:Repeater></ul>
            </asp:Panel>

            <asp:Panel ID="pnlNoStops" runat="server" CssClass="route-stops-empty">
                <strong>No stops added yet</strong>
                <span>Select an existing stop or create a new one below.</span>
            </asp:Panel>

            <asp:Repeater ID="rptRouteStops" runat="server" OnItemCommand="rptRouteStops_ItemCommand">
                <HeaderTemplate><ol class="route-stop-list"></HeaderTemplate>
                <ItemTemplate>
                    <li class="route-stop-row">
                        <span class="stop-order" aria-label='Stop <%#: Eval("StopOrder") %>'><%#: Eval("StopOrder") %></span>
                        <span class="route-stop-copy">
                            <strong><%#: Eval("StopName") %></strong>
                            <span class="route-stop-meta"><span><%#: Eval("StopCode") %></span><span><%#: Eval("Area") %></span></span>
                            <asp:PlaceHolder runat="server" Visible='<%# Convert.ToBoolean(Eval("IsNew")) %>'><span class="draft-chip">New stop</span></asp:PlaceHolder>
                        </span>
                        <span class="route-stop-actions">
                            <asp:LinkButton runat="server" CssClass="btn btn-sm btn-outline-secondary" CommandName="MoveUp" CommandArgument='<%# Eval("DraftKey") %>' Text="Move up" CausesValidation="false" Enabled='<%# Convert.ToBoolean(Eval("CanMoveUp")) %>' />
                            <asp:LinkButton runat="server" CssClass="btn btn-sm btn-outline-secondary" CommandName="MoveDown" CommandArgument='<%# Eval("DraftKey") %>' Text="Move down" CausesValidation="false" Enabled='<%# Convert.ToBoolean(Eval("CanMoveDown")) %>' />
                            <asp:LinkButton runat="server" CssClass="btn btn-sm btn-link text-danger" CommandName="Remove" CommandArgument='<%# Eval("DraftKey") %>' Text="Remove" CausesValidation="false" />
                        </span>
                    </li>
                </ItemTemplate>
                <FooterTemplate></ol></FooterTemplate>
            </asp:Repeater>

            <div class="stop-picker">
                <div class="stop-picker-existing">
                    <asp:Label ID="lblExistingStop" runat="server" AssociatedControlID="ddlExistingStop" CssClass="form-label" Text="Add an existing stop" />
                    <div class="stop-picker-controls">
                        <asp:DropDownList ID="ddlExistingStop" runat="server" CssClass="form-select" />
                        <asp:LinkButton ID="btnAddExistingStop" runat="server" CssClass="btn btn-outline-secondary" Text="Add stop" CausesValidation="false" OnClick="btnAddExistingStop_Click" />
                    </div>
                </div>
                <span class="stop-picker-divider">or</span>
                <asp:LinkButton ID="btnShowNewStop" runat="server" CssClass="btn btn-outline-secondary" Text="Create new stop" CausesValidation="false" OnClick="btnShowNewStop_Click" />
            </div>

            <asp:Panel ID="pnlNewStop" runat="server" CssClass="inline-stop-panel" Visible="false">
                <div class="inline-stop-heading">
                    <div><p class="section-kicker">New network stop</p><h3>Create and add stop</h3></div>
                    <span class="generated-code"><asp:Literal ID="litStopCode" runat="server" /></span>
                </div>
                <p class="inline-stop-guidance">The stop remains part of this route draft until the route is saved.</p>
                <div class="row g-3">
                    <div class="col-md-6">
                        <asp:Label ID="lblStopName" runat="server" AssociatedControlID="txtStopName" CssClass="form-label required-label" Text="Stop name" />
                        <asp:TextBox ID="txtStopName" runat="server" CssClass="form-control" MaxLength="150" autocomplete="off" />
                    </div>
                    <div class="col-md-6">
                        <asp:Label ID="lblArea" runat="server" AssociatedControlID="txtArea" CssClass="form-label required-label" Text="Area" />
                        <asp:TextBox ID="txtArea" runat="server" CssClass="form-control" MaxLength="150" autocomplete="off" />
                    </div>
                    <div class="col-md-6">
                        <asp:Label ID="lblLatitude" runat="server" AssociatedControlID="txtLatitude" CssClass="form-label" Text="Latitude (optional)" />
                        <asp:TextBox ID="txtLatitude" runat="server" CssClass="form-control" TextMode="Number" min="-90" max="90" step="0.000001" inputmode="decimal" />
                    </div>
                    <div class="col-md-6">
                        <asp:Label ID="lblLongitude" runat="server" AssociatedControlID="txtLongitude" CssClass="form-label" Text="Longitude (optional)" />
                        <asp:TextBox ID="txtLongitude" runat="server" CssClass="form-control" TextMode="Number" min="-180" max="180" step="0.000001" inputmode="decimal" />
                    </div>
                </div>
                <div class="inline-stop-actions">
                    <asp:LinkButton ID="btnCancelNewStop" runat="server" CssClass="btn btn-link" Text="Cancel" CausesValidation="false" OnClick="btnCancelNewStop_Click" />
                    <asp:LinkButton ID="btnAddNewStop" runat="server" CssClass="btn btn-primary" Text="Add stop to route" CausesValidation="false" OnClick="btnAddNewStop_Click" />
                </div>
            </asp:Panel>
        </section>

        <div class="form-actions">
            <a class="btn btn-outline-secondary" href="<%= ResolveUrl("~/Admin/Routes/RouteList.aspx") %>">Cancel</a>
            <asp:Button ID="btnSaveRoute" runat="server" CssClass="btn btn-primary" Text="Save route" OnClick="btnSaveRoute_Click" />
        </div>
    </div>
</asp:Content>
