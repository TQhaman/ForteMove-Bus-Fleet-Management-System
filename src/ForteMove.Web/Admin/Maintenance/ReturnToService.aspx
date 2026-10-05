<%@ Page Title="Return to service" Language="C#" MasterPageFile="~/Admin/Admin.Master" AutoEventWireup="true" CodeBehind="ReturnToService.aspx.cs" Inherits="ForteMove.Web.Admin.Maintenance.ReturnToServicePage" %>
<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
<header class="page-heading"><div><p class="eyebrow">Maintenance</p><h1>Return to service</h1></div><a class="btn btn-outline-primary" href="Overview.aspx">Maintenance overview</a></header>
<asp:Panel ID="pnlMessage" runat="server" Visible="false" CssClass="alert alert-danger" role="alert"><asp:Literal ID="litMessage" runat="server" /></asp:Panel>
<section class="surface-card"><asp:Literal ID="litReview" runat="server" /><p>This decision does not clear affected Trip reviews. Resolve those through Assignments after restoring the bus.</p><div class="form-group"><asp:Label runat="server" AssociatedControlID="txtNote" Text="Return-to-service decision note" /><asp:TextBox ID="txtNote" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="4" MaxLength="1000" /></div><asp:Button ID="btnReturn" runat="server" Text="Confirm return to service" CssClass="btn btn-primary" OnClick="Return_Click" /><asp:HyperLink ID="lnkHistory" runat="server" Text="Service history" CssClass="btn btn-outline-primary" /></section>
</asp:Content>

