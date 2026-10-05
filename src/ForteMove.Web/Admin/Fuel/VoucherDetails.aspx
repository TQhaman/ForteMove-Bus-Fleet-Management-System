<%@ Page Title="Fuel Voucher" Language="C#" MasterPageFile="~/Admin/Admin.Master" AutoEventWireup="true" CodeBehind="VoucherDetails.aspx.cs" Inherits="ForteMove.Web.Admin.Fuel.VoucherDetailsPage"  %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
<header class="page-heading"><div><p class="section-kicker">Fuel</p><h1>Fuel Voucher</h1></div></header>
<nav class="fuel-subnavigation" aria-label="Fuel navigation"><a  href="<%= ResolveUrl("~/Admin/Fuel/Requests.aspx") %>">Requests</a><a  href="<%= ResolveUrl("~/Admin/Fuel/Vouchers.aspx") %>">Vouchers</a><a  href="<%= ResolveUrl("~/Admin/Fuel/Transactions.aspx") %>">Transactions</a><a  href="<%= ResolveUrl("~/Admin/Fuel/Stations.aspx") %>">Stations</a><a  href="<%= ResolveUrl("~/Admin/Fuel/PrototypeTerminal.aspx") %>">Prototype Terminal</a></nav>
<asp:Panel ID="pnlMessage" runat="server" CssClass="alert alert-danger" Visible="false" role="alert"><asp:Literal ID="litMessage" runat="server"/></asp:Panel>
<section class="app-panel form-panel"><asp:Literal ID="litDetails" runat="server"/><asp:Panel ID="pnlWarnings" runat="server" CssClass="alert alert-warning" Visible="false"><asp:Literal ID="litWarnings" runat="server"/></asp:Panel></section>
</asp:Content>
