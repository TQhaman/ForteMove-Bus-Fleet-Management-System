<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="RegisterPassenger.aspx.cs" Inherits="ForteMove.Web.Account.RegisterPassenger" %>
<!DOCTYPE html>
<html lang="en">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Create passenger account | ForteMove</title>
    <link rel="icon" type="image/png" href="<%= ResolveUrl("~/Content/Brand/fortemove-mark.png") %>" />
    <link runat="server" href="~/Content/vendor/bootstrap.min.css" rel="stylesheet" />
    <link runat="server" href="~/Content/fortemove.css" rel="stylesheet" />
</head>
<body class="auth-page">
<main class="auth-layout">
    <section class="auth-brand-panel" aria-label="ForteMove"><div class="auth-brand-content"><img class="auth-wordmark" src="<%= ResolveUrl("~/Content/Brand/fortemove-wordmark.png") %>" alt="ForteMove" /><p class="auth-eyebrow">Passenger portal</p><h1>Your next journey starts here.</h1><p>Create one secure account for tickets and your ForteMove wallet.</p></div></section>
    <section class="auth-form-panel"><form id="form1" runat="server" class="auth-card passenger-registration-card">
        <div class="auth-mobile-brand"><img src="<%= ResolveUrl("~/Content/Brand/fortemove-mark.png") %>" alt="" /><span>ForteMove</span></div>
        <p class="section-kicker">Passenger registration</p><h2>Create your account</h2><p class="text-secondary mb-4">All fields marked required must be completed.</p>
        <asp:Panel ID="pnlErrors" runat="server" CssClass="alert alert-danger validation-summary" Visible="false" role="alert"><strong>Check your details</strong><ul><asp:Repeater ID="rptErrors" runat="server"><ItemTemplate><li><%#: Eval("Message") %></li></ItemTemplate></asp:Repeater></ul></asp:Panel>
        <div class="row g-3">
            <div class="col-sm-6"><asp:Label runat="server" AssociatedControlID="txtFirstName" CssClass="form-label required-label" Text="First name" /><asp:TextBox ID="txtFirstName" runat="server" CssClass="form-control" MaxLength="100" autocomplete="given-name" /></div>
            <div class="col-sm-6"><asp:Label runat="server" AssociatedControlID="txtLastName" CssClass="form-label required-label" Text="Last name" /><asp:TextBox ID="txtLastName" runat="server" CssClass="form-control" MaxLength="100" autocomplete="family-name" /></div>
            <div class="col-12"><asp:Label runat="server" AssociatedControlID="txtEmail" CssClass="form-label required-label" Text="Email address" /><asp:TextBox ID="txtEmail" runat="server" CssClass="form-control" TextMode="Email" MaxLength="254" autocomplete="email" /></div>
            <div class="col-12"><asp:Label runat="server" AssociatedControlID="txtPhone" CssClass="form-label" Text="Phone number (optional)" /><asp:TextBox ID="txtPhone" runat="server" CssClass="form-control" MaxLength="30" autocomplete="tel" /></div>
            <div class="col-12"><asp:Label runat="server" AssociatedControlID="txtPassword" CssClass="form-label required-label" Text="Password" /><asp:TextBox ID="txtPassword" runat="server" CssClass="form-control" TextMode="Password" MaxLength="128" autocomplete="new-password" /><div class="form-text">Use a passphrase of 15 to 128 characters. Spaces and Unicode are allowed.</div></div>
            <div class="col-12"><asp:Label runat="server" AssociatedControlID="txtConfirmPassword" CssClass="form-label required-label" Text="Confirm password" /><asp:TextBox ID="txtConfirmPassword" runat="server" CssClass="form-control" TextMode="Password" MaxLength="128" autocomplete="new-password" /></div>
        </div>
        <asp:Button ID="btnRegister" runat="server" CssClass="btn btn-primary btn-lg w-100 mt-4" Text="Create passenger account" OnClick="btnRegister_Click" />
        <p class="auth-support-note">Already registered? <a href="<%= ResolveUrl("~/Account/Login.aspx") %>">Sign in</a></p>
    </form></section>
</main>
</body></html>
