param(
    [Parameter(Mandatory = $true)]
    [string]$Query
)

Write-Host "Script started"

$clientId = "p8tfbuzs9lcvur6erh6fwevejsmzx9"
$clientSecret = "8hgyaw52nkvhfbm3p9yf1dfx9e4hcf"

if ([string]::IsNullOrWhiteSpace($clientId) -or [string]::IsNullOrWhiteSpace($clientSecret)) {
    $dbPath = "C:\Users\hed_p\AppData\Roaming\Playarr\playarr.db"

    if (-not (Test-Path $dbPath))
    {
        throw "Database not found: $dbPath"
    }

    Write-Host "Database found: $dbPath"

    $clientId = (sqlite3 $dbPath "SELECT IGDB_Client_ID FROM settings LIMIT 1;").Trim()
    $clientSecret = (sqlite3 $dbPath "SELECT IGDB_Secret FROM settings LIMIT 1;").Trim()

    if ([string]::IsNullOrWhiteSpace($clientId))
    {
        throw "IGDB_Client_ID is empty."
    }

    if ([string]::IsNullOrWhiteSpace($clientSecret))
    {
        throw "IGDB_Secret is empty."
    }
}

Write-Host "Client ID found"
Write-Host "Client Secret length: $($clientSecret.Length)"

try
{
    Write-Host "Requesting Twitch OAuth token..."

    $oauth = Invoke-RestMethod `
        -Uri "https://id.twitch.tv/oauth2/token?client_id=$clientId&client_secret=$clientSecret&grant_type=client_credentials" `
        -Method Post

    Write-Host $oauth | ConvertTo-Json -Depth 100

    $accessToken = $oauth.access_token

    if ([string]::IsNullOrWhiteSpace($accessToken))
    {
        throw "Failed to obtain Twitch access token."
    }

    Write-Host "Access token acquired"
    Write-Host "Access token length: $($accessToken.Length)"

    $headers = @{
        "Client-ID"     = $clientId
        "Authorization" = "Bearer $accessToken"
    }

    Write-Host "Calling IGDB..."

    $result = Invoke-RestMethod `
        -Uri "https://api.igdb.com/v4/games" `
        -Method POST `
        -Headers $headers `
        -Body $Query `
        -ContentType "text/plain"

    Write-Host "Request successful"

    $result | ConvertTo-Json -Depth 100
}
catch
{
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red

    if ($_.Exception.Response)
    {
        try
        {
            Write-Host "Status: $([int]$_.Exception.Response.StatusCode)"

            $reader = [System.IO.StreamReader]::new(
                $_.Exception.Response.GetResponseStream()
            )

            $body = $reader.ReadToEnd()

            Write-Host "Response:"
            Write-Output $body
        }
        catch
        {
            Write-Host "Could not read response body."
        }
    }
}
