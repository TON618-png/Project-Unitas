$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$project = Join-Path $PSScriptRoot 'Unitas.Webshop/Unitas.Webshop.csproj'
$gmailScope = 'https://www.googleapis.com/auth/gmail.send'

function ConvertTo-Base64Url([byte[]] $Bytes) {
    return [Convert]::ToBase64String($Bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function New-RandomToken {
    $bytes = [byte[]]::new(32)
    $random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $random.GetBytes($bytes) }
    finally { $random.Dispose() }
    return ConvertTo-Base64Url $bytes
}

$enteredPath = Read-Host 'Pad naar het gedownloade OAuth-clientbestand (JSON)'
if ([string]::IsNullOrWhiteSpace($enteredPath)) {
    throw 'Geef het pad naar het OAuth-clientbestand op.'
}
$configPath = $enteredPath.Trim().Trim('"')
$client = (Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json).installed
if ([string]::IsNullOrWhiteSpace($client.client_id) -or [string]::IsNullOrWhiteSpace($client.client_secret)) {
    throw 'Download in Google Cloud het JSON-bestand van een OAuth-client van het type Desktop-app.'
}

$state = New-RandomToken
$verifier = New-RandomToken
$sha256 = [System.Security.Cryptography.SHA256]::Create()
try { $challenge = ConvertTo-Base64Url ($sha256.ComputeHash([Text.Encoding]::ASCII.GetBytes($verifier))) }
finally { $sha256.Dispose() }

$portFinder = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$portFinder.Start()
try { $port = $portFinder.LocalEndpoint.Port }
finally { $portFinder.Stop() }
$callback = "http://127.0.0.1:$port/"
$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add($callback)
try {
    $listener.Start()
    $parameters = @{
        client_id = $client.client_id
        redirect_uri = $callback
        response_type = 'code'
        scope = "openid email $gmailScope"
        access_type = 'offline'
        prompt = 'consent select_account'
        state = $state
        code_challenge = $challenge
        code_challenge_method = 'S256'
    }
    $query = ($parameters.GetEnumerator() | ForEach-Object {
        "$($_.Key)=$([Uri]::EscapeDataString($_.Value))"
    }) -join '&'
    Start-Process "https://accounts.google.com/o/oauth2/v2/auth?$query"
    Write-Host 'Rond in de browser de toestemming voor de Gmail-afzender af.'

    $deadline = [DateTimeOffset]::UtcNow.AddMinutes(5)
    do {
        $remaining = $deadline - [DateTimeOffset]::UtcNow
        $next = $listener.GetContextAsync()
        if ($remaining -le [TimeSpan]::Zero -or !$next.Wait($remaining)) {
            throw 'Geen terugkoppeling van Google ontvangen. Start de configuratie opnieuw.'
        }

        $context = $next.GetAwaiter().GetResult()
        $request = $context.Request
        $validCallback = $request.HttpMethod -eq 'GET' -and $request.Url.AbsolutePath -eq '/' -and
            [string]::Equals($state, $request.QueryString['state'], [StringComparison]::Ordinal)
        $code = $request.QueryString['code']
        $oauthError = $request.QueryString['error']
        $response = $context.Response
        $response.StatusCode = if ($validCallback -and $code -and !$oauthError) { 200 } else { 400 }
        $response.ContentType = 'text/plain; charset=utf-8'
        $response.Headers['Cache-Control'] = 'no-store'
        $message = if ($response.StatusCode -eq 200) {
            'Terugkoppeling ontvangen. Ga verder in PowerShell.'
        } else {
            'De toestemming kon niet worden verwerkt. Ga terug naar PowerShell.'
        }
        try {
            $bytes = [Text.Encoding]::UTF8.GetBytes($message)
            $response.ContentLength64 = $bytes.Length
            $response.OutputStream.Write($bytes, 0, $bytes.Length)
        }
        finally { $response.Close() }
    } until ($validCallback)
}
finally { $listener.Close() }

if (!$code -or $oauthError) {
    throw 'Google heeft geen toestemming gegeven om via Gmail te versturen.'
}

try {
    $tokens = Invoke-RestMethod -Method Post -Uri 'https://oauth2.googleapis.com/token' -MaximumRedirection 0 -TimeoutSec 20 -Body @{
        client_id = $client.client_id
        client_secret = $client.client_secret
        code = $code
        code_verifier = $verifier
        redirect_uri = $callback
        grant_type = 'authorization_code'
    }
    if (!$tokens.refresh_token -or !$tokens.access_token -or $gmailScope -notin ($tokens.scope -split ' ')) {
        throw 'Onvoldoende toestemming voor Gmail.'
    }
    $account = Invoke-RestMethod -Uri 'https://openidconnect.googleapis.com/v1/userinfo' -MaximumRedirection 0 -TimeoutSec 20 -Headers @{
        Authorization = "Bearer $($tokens.access_token)"
    }
}
catch {
    throw 'De Google-koppeling kon niet worden voltooid. Controleer de OAuth-client en sta het verzenden via Gmail toe.'
}

if (!$account.email -or $account.email_verified -ne $true) {
    throw 'Google heeft geen geverifieerd afzenderadres teruggegeven.'
}

@{
    'Gmail:ClientId' = $client.client_id
    'Gmail:ClientSecret' = $client.client_secret
    'Gmail:RefreshToken' = $tokens.refresh_token
    'Gmail:From' = $account.email
} | ConvertTo-Json -Compress | dotnet user-secrets set --project $project

if ($LASTEXITCODE -ne 0) {
    throw 'De Gmail-instellingen konden niet worden opgeslagen.'
}

Write-Host "Gmail is ingesteld met $($account.email) als afzender. Herstart de webshop."
