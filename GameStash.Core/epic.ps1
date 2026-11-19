param(
    [Parameter(Mandatory = $true)]
    [string]$Endpoint,

    [Parameter(Mandatory = $false)]
    [string]$Query = "",

    [Parameter(Mandatory = $false)]
    [ValidateSet("GET", "POST", "PUT", "DELETE")]
    [string]$Method = "GET",

    [string]$PgHost = "localhost",
    [int]$PgPort = 5432,
    [string]$PgUser = "postgres",
    [string]$PgPassword = "postgres",
    [string]$PgDatabase = "gamestash"
)

Write-Host "Epic Games API script started."

# Official Epic Games Launcher client credentials (from EpicClient.cs)
$clientId = "34a02cf8f4414e29b15921876da36f9a"
$clientSecret = "daafbccc737745039dffe53d94fc76cf"

# 1. Locate the psql executable
$psqlCmd = Get-Command psql -ErrorAction SilentlyContinue
if ($psqlCmd) {
    $psqlExe = "psql"
} else {
    $found = Get-ChildItem "C:\Program Files\PostgreSQL" -Filter psql.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $found) {
        throw "Command 'psql' was not found in PATH or 'C:\Program Files\PostgreSQL'."
    }
    $psqlExe = $found.FullName
}

# 2. Retrieve tokens from PostgreSQL
Write-Host "Fetching Epic tokens from database '$PgDatabase'..."
if (-not [string]::IsNullOrWhiteSpace($PgPassword)) {
    $env:PGPASSWORD = $PgPassword
}

$sql = "SELECT COALESCE(epic_access_token, '') || '|' || COALESCE(epic_refresh_token, '') FROM settings LIMIT 1;"

try {
    $output = (& $psqlExe -h $PgHost -p $PgPort -U $PgUser -d $PgDatabase -t -A -c $sql).Trim()
}
catch {
    throw "Error executing query on PostgreSQL: $($_.Exception.Message)"
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

if ([string]::IsNullOrWhiteSpace($output) -or -not ($output -match "\|")) {
    throw "Table 'settings' is empty or missing 'epic_access_token' / 'epic_refresh_token' columns."
}

$parts = $output -split '\|'
$accessToken = $parts[0].Trim()
$refreshToken = $parts[1].Trim()

Write-Host "Access Token length: $($accessToken.Length)"
Write-Host "Refresh Token length: $($refreshToken.Length)"

# 3. Verify session token
# 3. Verify session token
$sessionValid = $false

if (-not [string]::IsNullOrWhiteSpace($accessToken)) {
    Write-Host "Verifying Epic session token..."
    try {
        $verifyRes = Invoke-RestMethod `
            -Uri "https://account-public-service-prod.ol.epicgames.com/account/api/oauth/verify" `
            -Method GET `
            -Headers @{ "Authorization" = "Bearer $accessToken" } `
            -ErrorAction Stop

        $accountId = $verifyRes.account_id

        # Try display_name (snake_case) from verify first
        $displayName = $verifyRes.display_name
        if ([string]::IsNullOrWhiteSpace($displayName)) {
            $displayName = $verifyRes.displayName
        }

        # If verify didn't include it, fetch it directly from the public account endpoint
        if ([string]::IsNullOrWhiteSpace($displayName) -and -not [string]::IsNullOrWhiteSpace($accountId)) {
            try {
                $accountProfile = Invoke-RestMethod `
                    -Uri "https://account-public-service-prod.ol.epicgames.com/account/api/public/account/$accountId" `
                    -Method GET `
                    -Headers @{ "Authorization" = "Bearer $accessToken" }
                $displayName = $accountProfile.displayName
            }
            catch { }
        }

        Write-Host "Token is valid! Connected as: $displayName (Account: $accountId)" -ForegroundColor Green
        $sessionValid = $true
    }
    catch {
        Write-Host "Token verification failed (expired or invalid). Refresh required." -ForegroundColor Yellow
    }
}

# 4. Refresh token if verification failed or token was empty
if (-not $sessionValid) {
    if ([string]::IsNullOrWhiteSpace($refreshToken)) {
        throw "Cannot refresh session: 'epic_refresh_token' is empty in the database."
    }

    Write-Host "Refreshing Epic access token..."

    $basicAuthBytes = [System.Text.Encoding]::UTF8.GetBytes("$clientId`:$clientSecret")
    $basicAuthHeader = "Basic " + [Convert]::ToBase64String($basicAuthBytes)

    $refreshBody = @{
        grant_type    = "refresh_token"
        refresh_token = $refreshToken
    }

    try {
        $tokenRes = Invoke-RestMethod `
            -Uri "https://account-public-service-prod.ol.epicgames.com/account/api/oauth/token" `
            -Method POST `
            -Headers @{ "Authorization" = $basicAuthHeader } `
            -ContentType "application/x-www-form-urlencoded" `
            -Body $refreshBody `
            -ErrorAction Stop

        $accessToken = $tokenRes.access_token
        if (-not [string]::IsNullOrWhiteSpace($tokenRes.refresh_token)) {
            $refreshToken = $tokenRes.refresh_token
        }

        Write-Host "Token refreshed successfully!" -ForegroundColor Green

        # Persist updated tokens back into PostgreSQL
        Write-Host "Updating rotated tokens in PostgreSQL..."
        $updateSql = "UPDATE settings SET epic_access_token = '$accessToken', epic_refresh_token = '$refreshToken';"

        if (-not [string]::IsNullOrWhiteSpace($PgPassword)) { $env:PGPASSWORD = $PgPassword }
        & $psqlExe -h $PgHost -p $PgPort -U $PgUser -d $PgDatabase -c $updateSql | Out-Null
        Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
        Write-Host "PostgreSQL settings updated."
    }
    catch {
        throw "Critical failure refreshing Epic Games token: $($_.Exception.Message)"
    }
}

# 5. Resolve endpoint target URL
# Handles absolute URLs or relative microservice paths
$targetUrl = $Endpoint
if ($targetUrl -notmatch '^https?://') {
    if ($targetUrl.StartsWith('/library')) {
        $targetUrl = "https://library-service.live.use1a.on.epicgames.com$targetUrl"
    } elseif ($targetUrl.StartsWith('/catalog')) {
        $targetUrl = "https://catalog-public-service-prod06.ol.epicgames.com$targetUrl"
    } elseif ($targetUrl.StartsWith('/account')) {
        $targetUrl = "https://account-public-service-prod.ol.epicgames.com$targetUrl"
    } else {
        $targetUrl = "https://library-service.live.use1a.on.epicgames.com/" + $targetUrl.TrimStart('/')
    }
}

# Append $Query to URL if using GET
if ($Method -eq "GET" -and -not [string]::IsNullOrWhiteSpace($Query)) {
    $sep = if ($targetUrl.Contains("?")) { "&" } else { "?" }
    $targetUrl = "$targetUrl$sep$($Query.TrimStart('?', '&'))"
}

# 6. Execute final API request
try {
    $headers = @{
        "Authorization" = "Bearer $accessToken"
    }

    Write-Host "Calling Epic Games API ($Method $targetUrl)..."

    $requestParams = @{
        Uri     = $targetUrl
        Method  = $Method
        Headers = $headers
    }

    if ($Method -ne "GET" -and -not [string]::IsNullOrWhiteSpace($Query)) {
        $requestParams["Body"] = $Query
        $requestParams["ContentType"] = "application/json"
    }

    $result = Invoke-RestMethod @requestParams
    Write-Host "Request successful!" -ForegroundColor Green

    $result | ConvertTo-Json -Depth 100
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red

    if ($_.Exception.Response) {
        try {
            Write-Host "Status: $([int]$_.Exception.Response.StatusCode)"
            $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
            Write-Host "Response Body:"
            Write-Output $reader.ReadToEnd()
        }
        catch {
            Write-Host "Could not read response body."
        }
    }
}