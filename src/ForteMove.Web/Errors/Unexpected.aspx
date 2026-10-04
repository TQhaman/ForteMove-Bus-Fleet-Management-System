<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Unexpected.aspx.cs" Inherits="ForteMove.Web.Errors.Unexpected" %>

<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Something went wrong | ForteMove</title>
    <link rel="icon" type="image/png" href="<%= ResolveUrl("~/Content/Brand/fortemove-mark.png") %>" />
    <link href="<%= ResolveUrl("~/Content/vendor/bootstrap.min.css") %>" rel="stylesheet" />
    <link href="<%= ResolveUrl("~/Content/fortemove.css") %>" rel="stylesheet" />
</head>
<body class="status-page">
    <main class="status-card">
        <img src="<%= ResolveUrl("~/Content/Brand/fortemove-mark.png") %>" alt="" class="status-mark" />
        <p class="section-kicker">Temporary interruption</p>
        <h1>We could not complete that request.</h1>
        <p>Return to ForteMove and review the latest information before trying again.</p>
        <a class="btn btn-primary" href="<%= ResolveUrl("~/Default.aspx") %>">Return to ForteMove</a>
    </main>
</body>
</html>
