# Serves only the synthetic renderer fixture on loopback. Never serves repository or database files.
$ErrorActionPreference = 'Stop'
$slice7Listener = New-Object System.Net.HttpListener
$slice7Listener.Prefixes.Add('http://localhost:55359/')
$slice7Listener.Start()
try {
    while ($slice7Listener.IsListening) {
        $slice7Context = $slice7Listener.GetContext()
        if ($slice7Context.Request.Url.AbsolutePath -ne '/') {
            $slice7Context.Response.StatusCode = 404
        } else {
            $slice7Bytes = [IO.File]::ReadAllBytes((Join-Path $PSScriptRoot 'Slice7BrowserPreview.html'))
            $slice7Context.Response.ContentType = 'text/html; charset=utf-8'
            $slice7Context.Response.OutputStream.Write($slice7Bytes, 0, $slice7Bytes.Length)
        }
        $slice7Context.Response.Close()
    }
} finally { $slice7Listener.Close() }
