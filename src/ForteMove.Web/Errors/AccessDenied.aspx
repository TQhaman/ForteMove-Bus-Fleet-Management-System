<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="AccessDenied.aspx.cs" Inherits="ForteMove.Web.Errors.AccessDenied" %>

<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Access denied | ForteMove</title>
    <link rel="icon" type="image/png" href="<%= ResolveUrl("~/Content/Brand/fortemove-mark.png") %>" />
    <link href="<%= ResolveUrl("~/Content/vendor/bootstrap.min.css") %>" rel="stylesheet" />
    <link href="<%= ResolveUrl("~/Content/fortemove.css") %>" rel="stylesheet" />
</head>
<body class="status-page">
    <main class="status-card">
        <img src="<%= ResolveUrl("~/Content/Brand/fortemove-mark.png") %>" alt="" class="status-mark" />
        <p class="section-kicker">Access restricted</p>
        <h1>You do not have access to this area.</h1>
        <p>Your ForteMove account is signed in, but this page is reserved for a different role.</p>
        <a class="btn btn-primary" href="<%= ResolveUrl("~/Default.aspx") %>">Return to ForteMove</a>
    </main>
</body>
</html>
