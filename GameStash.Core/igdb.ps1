param(
    [Parameter(Mandatory = $true)]
    [string]$Endpoint,

    [Parameter(Mandatory = $true)]
    [string]$Query,

    [string]$PgHost = "localhost",
    [int]$PgPort = 5432,
    [string]$PgUser = "postgres",
    [string]$PgPassword = "postgres",
    [string]$PgDatabase = "gamestash"
)

Write-Host "Script started"

$clientId = $null
$clientSecret = $null

if ([string]::IsNullOrWhiteSpace($clientId) -or [string]::IsNullOrWhiteSpace($clientSecret)) {
    Write-Host "Fetching credentials from PostgreSQL database '$PgDatabase'..."

    # 1. Localizar o executável psql
    $psqlCmd = Get-Command psql -ErrorAction SilentlyContinue
    if ($psqlCmd) {
        $psqlExe = "psql"
    } else {
        $found = Get-ChildItem "C:\Program Files\PostgreSQL" -Filter psql.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $found) {
            throw "O comando 'psql' não foi encontrado no PATH nem em 'C:\Program Files\PostgreSQL'. Instala o client ou adiciona o caminho ao PATH."
        }
        $psqlExe = $found.FullName
    }

    # 2. Configurar a password no ambiente para execução não-interativa
    if (-not [string]::IsNullOrWhiteSpace($PgPassword)) {
        $env:PGPASSWORD = $PgPassword
    }

    # 3. Executar query sacando igdb_client_id e igdb_secret delimitados por '|'
    $sql = "SELECT igdb_client_id || '|' || igdb_secret FROM settings LIMIT 1;"

    try {
        $output = (& $psqlExe -h $PgHost -p $PgPort -U $PgUser -d $PgDatabase -t -A -c $sql).Trim()
    }
    catch {
        throw "Erro ao executar query no PostgreSQL: $($_.Exception.Message)"
    }
    finally {
        # Limpar variável de ambiente por segurança
        Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    }

    if ([string]::IsNullOrWhiteSpace($output) -or -not ($output -match "\|")) {
        throw "A tabela 'settings' está vazia ou as colunas não contêm valores válidos."
    }

    $parts = $output -split '\|'
    $clientId = $parts[0].Trim()
    $clientSecret = $parts[1].Trim()

    if ([string]::IsNullOrWhiteSpace($clientId)) {
        throw "igdb_client_id está vazio na base de dados."
    }

    if ([string]::IsNullOrWhiteSpace($clientSecret)) {
        throw "igdb_secret está vazio na base de dados."
    }

    Write-Host "Credentials successfully retrieved from PostgreSQL."
}

Write-Host "Client ID found: $clientId"
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
		-Uri ("https://api.igdb.com/v4/" + $Endpoint) `
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
