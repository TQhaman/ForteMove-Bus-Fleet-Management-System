<%@ Page Title="Register bus" Language="C#" MasterPageFile="~/Admin/Admin.Master" AutoEventWireup="true" CodeBehind="RegisterBus.aspx.cs" Inherits="ForteMove.Web.Admin.RegisterBus" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <header class="page-heading page-heading-with-action">
        <div>
            <p class="section-kicker">Fleet register</p>
            <h1>Register a bus</h1>
            <p>Capture the vehicle identity, operating capacity and current compliance dates.</p>
        </div>
        <a class="btn btn-outline-secondary" href="<%= ResolveUrl("~/Admin/FleetList.aspx") %>">View fleet list</a>
    </header>

    <asp:Panel ID="pnlErrors" runat="server" CssClass="alert alert-danger validation-summary" role="alert" Visible="false" tabindex="-1">
        <div class="validation-summary-heading">
            <span class="alert-icon" aria-hidden="true">!</span>
            <strong>Review the highlighted information</strong>
        </div>
        <ul>
            <asp:Repeater ID="rptErrors" runat="server">
                <ItemTemplate><li><%#: Eval("Message") %></li></ItemTemplate>
            </asp:Repeater>
        </ul>
    </asp:Panel>

    <div class="form-layout">
        <section class="app-panel form-panel" aria-labelledby="identity-heading">
            <div class="panel-heading">
                <span class="panel-step">01</span>
                <div>
                    <h2 id="identity-heading">Vehicle identity</h2>
                    <p>Use the identifiers shown on the vehicle and registration records.</p>
                </div>
            </div>

            <div class="row g-4">
                <div class="col-md-4">
                    <asp:Label ID="lblFleetNumber" runat="server" AssociatedControlID="txtFleetNumber" CssClass="form-label required-label" Text="Fleet number" />
                    <asp:TextBox ID="txtFleetNumber" runat="server" CssClass="form-control" MaxLength="30" required="required" autocomplete="off" />
                    <div class="form-text">Suggested automatically; replace it if the vehicle already has an assigned fleet number.</div>
                </div>
                <div class="col-md-4">
                    <asp:Label ID="lblRegistrationNumber" runat="server" AssociatedControlID="txtRegistrationNumber" CssClass="form-label required-label" Text="Registration number" />
                    <asp:TextBox ID="txtRegistrationNumber" runat="server" CssClass="form-control" MaxLength="30" required="required" autocomplete="off" />
                </div>
                <div class="col-md-4">
                    <asp:Label ID="lblVin" runat="server" AssociatedControlID="txtVin" CssClass="form-label required-label" Text="VIN" />
                    <asp:TextBox ID="txtVin" runat="server" CssClass="form-control" MaxLength="50" required="required" autocomplete="off" />
                </div>
                <div class="col-md-4">
                    <asp:Label ID="lblMake" runat="server" AssociatedControlID="txtMake" CssClass="form-label required-label" Text="Make" />
                    <asp:TextBox ID="txtMake" runat="server" CssClass="form-control" MaxLength="100" required="required" autocomplete="off" />
                </div>
                <div class="col-md-4">
                    <asp:Label ID="lblModel" runat="server" AssociatedControlID="txtModel" CssClass="form-label required-label" Text="Model" />
                    <asp:TextBox ID="txtModel" runat="server" CssClass="form-control" MaxLength="100" required="required" autocomplete="off" />
                </div>
                <div class="col-md-4">
                    <asp:Label ID="lblManufactureYear" runat="server" AssociatedControlID="txtManufactureYear" CssClass="form-label required-label" Text="Manufacture year" />
                    <asp:TextBox ID="txtManufactureYear" runat="server" CssClass="form-control" TextMode="Number" min="1950" step="1" required="required" inputmode="numeric" />
                </div>
            </div>
        </section>

        <section class="app-panel form-panel" aria-labelledby="configuration-heading">
            <div class="panel-heading">
                <span class="panel-step">02</span>
                <div>
                    <h2 id="configuration-heading">Operating configuration</h2>
                    <p>Record the capacity, energy system and current odometer reading.</p>
                </div>
            </div>

            <div class="row g-4">
                <div class="col-md-4">
                    <asp:Label ID="lblCategory" runat="server" AssociatedControlID="ddlCategory" CssClass="form-label required-label" Text="Bus category" />
                    <asp:DropDownList ID="ddlCategory" runat="server" CssClass="form-select" required="required" />
                </div>
                <div class="col-md-4">
                    <asp:Label ID="lblPassengerCapacity" runat="server" AssociatedControlID="txtPassengerCapacity" CssClass="form-label required-label" Text="Passenger capacity" />
                    <asp:TextBox ID="txtPassengerCapacity" runat="server" CssClass="form-control" TextMode="Number" min="1" max="200" step="1" required="required" inputmode="numeric" />
                </div>
                <div class="col-md-4">
                    <asp:Label ID="lblGrossVehicleMass" runat="server" AssociatedControlID="txtGrossVehicleMass" CssClass="form-label required-label" Text="Gross vehicle mass (kg)" />
                    <asp:TextBox ID="txtGrossVehicleMass" runat="server" CssClass="form-control" TextMode="Number" min="1" step="1" required="required" inputmode="numeric" />
                    <div class="form-text">Use the approved mass shown on the vehicle documentation.</div>
                </div>
                <div class="col-md-4">
                    <asp:Label ID="lblPropulsion" runat="server" AssociatedControlID="ddlPropulsion" CssClass="form-label required-label" Text="Propulsion type" />
                    <asp:DropDownList ID="ddlPropulsion" runat="server" CssClass="form-select" required="required" />
                </div>
                <div class="col-md-4">
                    <asp:Label ID="lblFuelCapacity" runat="server" AssociatedControlID="txtFuelCapacity" CssClass="form-label" Text="Fuel-tank capacity (L)" />
                    <asp:TextBox ID="txtFuelCapacity" runat="server" CssClass="form-control" TextMode="Number" min="0.01" step="0.01" inputmode="decimal" />
                    <div class="form-text">Required for petrol, diesel and hybrid buses.</div>
                </div>
                <div class="col-md-4">
                    <asp:Label ID="lblBatteryCapacity" runat="server" AssociatedControlID="txtBatteryCapacity" CssClass="form-label" Text="Battery capacity (kWh)" />
                    <asp:TextBox ID="txtBatteryCapacity" runat="server" CssClass="form-control" TextMode="Number" min="0.01" step="0.01" inputmode="decimal" />
                    <div class="form-text">Required for electric and hybrid buses.</div>
                </div>
                <div class="col-md-4">
                    <asp:Label ID="lblOdometer" runat="server" AssociatedControlID="txtOdometer" CssClass="form-label required-label" Text="Odometer (km)" />
                    <asp:TextBox ID="txtOdometer" runat="server" CssClass="form-control" TextMode="Number" min="0" step="0.1" required="required" inputmode="decimal" />
                </div>
            </div>
        </section>

        <section class="app-panel form-panel" aria-labelledby="compliance-heading">
            <div class="panel-heading">
                <span class="panel-step">03</span>
                <div>
                    <h2 id="compliance-heading">Compliance documents</h2>
                    <p>Expired documents can be recorded, but the bus will be placed Out of Service.</p>
                </div>
            </div>

            <div class="row g-4">
                <div class="col-md-4">
                    <asp:Label ID="lblLicenceExpiry" runat="server" AssociatedControlID="txtLicenceExpiry" CssClass="form-label required-label" Text="Licence expiry" />
                    <asp:TextBox ID="txtLicenceExpiry" runat="server" CssClass="form-control" TextMode="Date" required="required" />
                </div>
                <div class="col-md-4">
                    <asp:Label ID="lblRoadworthyExpiry" runat="server" AssociatedControlID="txtRoadworthyExpiry" CssClass="form-label required-label" Text="Roadworthy expiry" />
                    <asp:TextBox ID="txtRoadworthyExpiry" runat="server" CssClass="form-control" TextMode="Date" required="required" />
                </div>
                <div class="col-md-4">
                    <asp:Label ID="lblInsuranceExpiry" runat="server" AssociatedControlID="txtInsuranceExpiry" CssClass="form-label required-label" Text="Insurance expiry" />
                    <asp:TextBox ID="txtInsuranceExpiry" runat="server" CssClass="form-control" TextMode="Date" required="required" />
                </div>
                <div class="col-md-4">
                    <asp:Label ID="lblVehicleStatus" runat="server" AssociatedControlID="ddlVehicleStatus" CssClass="form-label required-label" Text="Vehicle status" />
                    <asp:DropDownList ID="ddlVehicleStatus" runat="server" CssClass="form-select" required="required">
                        <asp:ListItem Text="Operational" Value="Operational" Selected="True" />
                        <asp:ListItem Text="Out of service" Value="OutOfService" />
                        <asp:ListItem Text="Under maintenance" Value="UnderMaintenance" />
                        <asp:ListItem Text="Retired" Value="Retired" />
                    </asp:DropDownList>
                </div>
            </div>
        </section>

        <div class="form-actions">
            <a class="btn btn-outline-secondary" href="<%= ResolveUrl("~/Admin/FleetList.aspx") %>">Cancel</a>
            <asp:Button ID="btnRegisterBus" runat="server" CssClass="btn btn-primary" Text="Register bus" OnClick="btnRegisterBus_Click" />
        </div>
    </div>
</asp:Content>
