<%@ Page Title="Account" Language="C#" MasterPageFile="~/Passenger/Passenger.Master" AutoEventWireup="true" CodeBehind="Account.aspx.cs" Inherits="ForteMove.Web.Passenger.Account" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
<header class="passenger-heading"><p class="section-kicker">Passenger profile</p><h1>Account</h1><p>Your ForteMove sign-in and contact details.</p></header>
<section class="app-panel"><div class="route-summary-grid"><div><span>Name</span><strong><asp:Literal ID="litName" runat="server" /></strong></div><div><span>Email address</span><strong><asp:Literal ID="litEmail" runat="server" /></strong></div><div><span>Phone number</span><strong><asp:Literal ID="litPhone" runat="server" /></strong></div><div><span>Account access</span><strong><asp:Literal ID="litStatus" runat="server" /></strong></div></div><p class="text-secondary mt-3 mb-0">Profile editing is not included in this release.</p></section>
</asp:Content>
