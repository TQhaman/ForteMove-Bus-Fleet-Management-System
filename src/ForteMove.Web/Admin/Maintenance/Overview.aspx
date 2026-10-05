<%@ Page Title="Maintenance" Language="C#" MasterPageFile="~/Admin/Admin.Master" AutoEventWireup="true" CodeBehind="Overview.aspx.cs" Inherits="ForteMove.Web.Admin.Maintenance.OverviewPage" %>
<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
<header class="page-heading"><div><p class="eyebrow">Maintenance</p><h1>Maintenance</h1></div><a class="btn btn-outline-primary" href="Overview.aspx">Maintenance overview</a></header>
<asp:Panel ID="pnlMessage" runat="server" Visible="false" CssClass="alert alert-danger" role="alert"><asp:Literal ID="litMessage" runat="server" /></asp:Panel>
<section class="maintenance-metrics"><asp:Literal ID="litMetrics" runat="server" /></section><div class="maintenance-actions"><a class="btn btn-primary" href="CreateWorkOrder.aspx">Create work order</a><a class="btn btn-outline-primary" href="Due.aspx">Due and overdue</a><a class="btn btn-outline-primary" href="Plans.aspx">Preventive plans</a><a class="btn btn-outline-primary" href="RepairProviders.aspx">Repair providers</a></div><section class="surface-card"><h2>Attention required</h2><asp:Literal ID="litAttention" runat="server" /></section><section class="surface-card"><h2>Current work</h2><asp:Literal ID="litOrders" runat="server" /></section>
</asp:Content>

