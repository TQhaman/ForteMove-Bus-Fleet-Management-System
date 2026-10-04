<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="ForteMove.Web.Account.Login" %>

<!DOCTYPE html>
<html lang="en">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <meta name="description" content="Secure sign in to the ForteMove transit operations platform." />
    <title>Sign in | ForteMove</title>
    <link rel="icon" type="image/png" href="<%= ResolveUrl("~/Content/Brand/fortemove-mark.png") %>" />
    <link runat="server" href="~/Content/vendor/bootstrap.min.css" rel="stylesheet" />
    <link runat="server" href="~/Content/fortemove.css" rel="stylesheet" />
</head>
<body class="auth-page">
    <main class="auth-layout">
        <section class="auth-brand-panel" aria-label="ForteMove">
            <div class="auth-brand-content">
                <img class="auth-wordmark" src="<%= ResolveUrl("~/Content/Brand/fortemove-wordmark.png") %>" alt="ForteMove" />
                <p class="auth-eyebrow">Depot &amp; transit management</p>
                <h1>Move the operation forward with clarity.</h1>
                <p>Secure access for the people responsible for every journey.</p>
            </div>
        </section>

        <section class="auth-form-panel">
            <form id="form1" runat="server" class="auth-card">
                <div class="auth-mobile-brand">
                    <img src="<%= ResolveUrl("~/Content/Brand/fortemove-mark.png") %>" alt="" />
                    <span>ForteMove</span>
                </div>
                <p class="section-kicker">Secure portal</p>
                <h2>Welcome back</h2>
                <p class="text-secondary mb-4">Sign in with your ForteMove account.</p>

                <asp:Panel ID="pnlError" runat="server" CssClass="alert alert-danger app-alert" role="alert" Visible="false">
                    <span class="alert-icon" aria-hidden="true">!</span>
                    <asp:Label ID="lblError" runat="server" />
                </asp:Panel>

                <div class="mb-3">
                    <asp:Label ID="lblEmail" runat="server" AssociatedControlID="txtEmail" CssClass="form-label" Text="Email address" />
                    <asp:TextBox ID="txtEmail" runat="server" CssClass="form-control form-control-lg" TextMode="Email" MaxLength="254" autocomplete="username" required="required" aria-describedby="emailHelp" />
                    <div id="emailHelp" class="form-text">Use the email linked to your staff or passenger account.</div>
                </div>

                <div class="mb-4">
                    <asp:Label ID="lblPassword" runat="server" AssociatedControlID="txtPassword" CssClass="form-label" Text="Password" />
                    <asp:TextBox ID="txtPassword" runat="server" CssClass="form-control form-control-lg" TextMode="Password" MaxLength="128" autocomplete="current-password" required="required" />
                </div>

                <asp:Button ID="btnSignIn" runat="server" CssClass="btn btn-primary btn-lg w-100" Text="Sign in" OnClick="btnSignIn_Click" />
                <p class="auth-support-note">Access is limited to authorised ForteMove users.</p>
            </form>
        </section>
    </main>
</body>
</html>
