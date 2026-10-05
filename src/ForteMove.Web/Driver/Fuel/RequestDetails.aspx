<%@ Page Title="Fuel Request" Language="C#" MasterPageFile="~/Driver/Driver.Master" AutoEventWireup="true" CodeBehind="RequestDetails.aspx.cs" Inherits="ForteMove.Web.Driver.Fuel.RequestDetailsPage"  %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
<header class="driver-heading"><div><p class="section-kicker">Fuel</p><h1>Fuel Request</h1></div></header>
<nav class="fuel-subnavigation" aria-label="Fuel navigation"><a  href="<%= ResolveUrl("~/Driver/Fuel/Requests.aspx") %>">Requests</a><a  href="<%= ResolveUrl("~/Driver/Fuel/Vouchers.aspx") %>">Active Vouchers</a><a  href="<%= ResolveUrl("~/Driver/Fuel/History.aspx") %>">History</a></nav>
<asp:Panel ID="pnlMessage" runat="server" CssClass="alert alert-danger" Visible="false" role="alert"><asp:Literal ID="litMessage" runat="server"/></asp:Panel>
<section class="app-panel form-panel"><asp:Literal ID="litDetails" runat="server"/><asp:HyperLink ID="lnkVoucher" runat="server" CssClass="btn btn-primary" Text="View Voucher" Visible="false"/></section>
</asp:Content>
