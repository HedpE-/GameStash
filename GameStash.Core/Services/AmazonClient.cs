using GameStash.Core.Models.Amazon;
using Serilog;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GameStash.Core.Services
{
    public class AmazonClient
    {
        // Official Amazon Games Launcher client identifier used by open-source launchers (Nile/Heroic)
        public const string ClientId = "amzn1.application-oa2-client.2f823397848e48739c255e0d4a4b38c6";
        private const string RedirectUri = "https://www.amazon.com/ap/maplanding";
        private const string OpenIdBaseUrl = "http://specs.openid.net/auth/2.0";
        private const string AmazonOpenIdOAuth2Ns = "http://www.amazon.com/ap/ext/oauth/2";
        private const string AmazonSignInUrl = "https://www.amazon.com/ap/signin";

        private const string AuthHost = "api.amazon.com";
        private const string GamingHost = "gaming.amazon.com";

        private readonly HttpClient _httpClient;
        private string? _accessToken;
        private string? _refreshToken;
        private string? _deviceSerial;
        private readonly Func<string, string, Task>? _onTokensRefreshed;

        public AmazonClient(
            string? refreshToken = null,
            string? accessToken = null,
            string? deviceSerial = null,
            Func<string, string, Task>? onTokensRefreshed = null)
        {
            _refreshToken = refreshToken;
            _accessToken = accessToken;
            // Falls back to the serial used during registration if not explicitly passed
            _deviceSerial = deviceSerial ?? "7721A9347744493A8F561F03E3CE2E48";
            _onTokensRefreshed = onTokensRefreshed;
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Generates the Amazon OAuth login URL along with the PKCE code_verifier and deviceSerial
        /// needed to complete token exchange.
        /// </summary>
        public static (string LoginUrl, string CodeVerifier, string DeviceSerial) GenerateLoginUrl()
        {
            var randomBytes = RandomNumberGenerator.GetBytes(32);
            string codeVerifier = Convert.ToBase64String(randomBytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            var challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
            string codeChallenge = Convert.ToBase64String(challengeBytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            string deviceSerial = Guid.NewGuid().ToString("N").ToUpperInvariant();
            string clientIdHex = Convert.ToHexString(Encoding.UTF8.GetBytes($"{deviceSerial}#A2UMVHOX7UP4V7")).ToLowerInvariant();

            var queryParams = new Dictionary<string, string>
            {
                ["openid.ns"] = OpenIdBaseUrl,
                ["openid.claimed_id"] = $"{OpenIdBaseUrl}/identifier_select",
                ["openid.identity"] = $"{OpenIdBaseUrl}/identifier_select",
                ["openid.mode"] = "checkid_setup",
                ["openid.oa2.scope"] = "device_auth_access",
                ["openid.ns.oa2"] = AmazonOpenIdOAuth2Ns,
                ["openid.oa2.response_type"] = "code",
                ["openid.oa2.code_challenge_method"] = "S256",
                ["openid.oa2.code_challenge"] = codeChallenge,
                ["openid.oa2.client_id"] = $"device:{clientIdHex}",
                ["language"] = "en_US",
                ["marketPlaceId"] = "ATVPDKIKX0DER",
                ["openid.return_to"] = RedirectUri,
                ["openid.pape.max_auth_age"] = "0",
                ["openid.assoc_handle"] = "amzn_sonic_games_launcher",
                ["pageId"] = "amzn_sonic_games_launcher"
            };

            var queryString = string.Join("&", queryParams.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
            return ($"{AmazonSignInUrl}?{queryString}", codeVerifier, deviceSerial);
        }

        /// <summary>
        /// Exchanges the authorization code (or full redirect URL) and PKCE code_verifier for Amazon tokens.
        /// </summary>
        public async Task<bool> ExchangeCodeForTokenAsync(string codeOrUrl, string codeVerifier, string deviceSerial)
        {
            if (string.IsNullOrWhiteSpace(codeOrUrl) || string.IsNullOrWhiteSpace(codeVerifier) || string.IsNullOrWhiteSpace(deviceSerial))
                return false;

            string code = codeOrUrl.Trim();
            if (code.Contains("openid.oa2.authorization_code="))
            {
                var match = Regex.Match(code, @"openid\.oa2\.authorization_code=([^&]+)");
                if (match.Success)
                    code = Uri.UnescapeDataString(match.Groups[1].Value);
            }

            try
            {
                string clientIdHex = Convert.ToHexString(Encoding.UTF8.GetBytes($"{deviceSerial}#A2UMVHOX7UP4V7")).ToLowerInvariant();

                var payload = new
                {
                    auth_data = new
                    {
                        client_id = clientIdHex,
                        authorization_code = code,
                        code_verifier = codeVerifier,
                        code_algorithm = "SHA-256",
                        client_domain = "DeviceLegacy",
                        use_global_authentication = false
                    },
                    registration_data = new
                    {
                        domain = "Device",
                        app_name = "AGSLauncher for Windows",
                        app_version = "1.0.0",
                        device_model = "Windows",
                        device_name = (string?)null,
                        device_type = "A2UMVHOX7UP4V7",
                        device_serial = deviceSerial,
                        os_version = "10.0.19044.0"
                    },
                    requested_extensions = new[] { "customer_info", "device_info" },
                    requested_token_type = new[] { "bearer", "mac_dms" },
                    user_context_map = new { }
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, $"https://{AuthHost}/auth/register");
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:128.0) Gecko/20100101 Firefox/128.0");
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Log.ForContext<AmazonClient>().Error("Amazon /auth/register failed ({StatusCode}): {Body}", response.StatusCode, json);
                    return false;
                }

                using var doc = JsonDocument.Parse(json);

                var bearer = doc.RootElement
                    .GetProperty("response")
                    .GetProperty("success")
                    .GetProperty("tokens")
                    .GetProperty("bearer");

                _accessToken = bearer.GetProperty("access_token").GetString();
                _refreshToken = bearer.GetProperty("refresh_token").GetString();

                if (_onTokensRefreshed != null &&
                    !string.IsNullOrWhiteSpace(_refreshToken) &&
                    !string.IsNullOrWhiteSpace(_accessToken))
                {
                    await _onTokensRefreshed(_refreshToken, _accessToken);
                }

                return !string.IsNullOrWhiteSpace(_accessToken);
            }
            catch (Exception ex)
            {
                Log.ForContext<AmazonClient>().Error(ex, "Failed to register device and exchange authorization code for Amazon tokens.");
                return false;
            }
        }

        /// <summary>
        /// Refreshes an expired access token using the stored refresh token.
        /// </summary>
        public async Task<bool> RefreshAccessTokenAsync()
        {
            if (string.IsNullOrWhiteSpace(_refreshToken))
                return false;

            try
            {
                var payload = new
                {
                    app_name = "AGSLauncher for Windows",
                    app_version = "1.0.0",
                    source_token_type = "refresh_token",
                    source_token = _refreshToken,
                    requested_token_type = "access_token"
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, $"https://{AuthHost}/auth/token");
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<AmazonTokenResponse>(json);

                if (tokenData != null && !string.IsNullOrWhiteSpace(tokenData.AccessToken))
                {
                    _accessToken = tokenData.AccessToken;
                    if (!string.IsNullOrWhiteSpace(tokenData.RefreshToken))
                        _refreshToken = tokenData.RefreshToken;

                    if (_onTokensRefreshed != null && !string.IsNullOrWhiteSpace(_refreshToken))
                        await _onTokensRefreshed(_refreshToken, _accessToken);

                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.ForContext<AmazonClient>().Error(ex, "Failed to refresh Amazon Games access token.");
            }

            return false;
        }

        /// <summary>
        /// Validates the current session and retrieves the user's Amazon profile.
        /// </summary>
        public async Task<AmazonUserProfile?> GetUserProfileAsync()
        {
            if (string.IsNullOrWhiteSpace(_accessToken))
            {
                if (!await RefreshAccessTokenAsync())
                    return null;
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{AuthHost}/user/profile");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var response = await _httpClient.SendAsync(request);
                if ((response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.BadRequest) && await RefreshAccessTokenAsync())
                {
                    using var retry = new HttpRequestMessage(HttpMethod.Get, $"https://{AuthHost}/user/profile");
                    retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                    response = await _httpClient.SendAsync(retry);
                }

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<AmazonUserProfile>(json);
            }
            catch (Exception ex)
            {
                Log.ForContext<AmazonClient>().Error(ex, "Failed to verify Amazon Games user profile.");
                return null;
            }
        }

        /// <summary>
        /// Fetches all owned games (entitlements) from the Amazon Games Distribution API with pagination.
        /// </summary>
        public async Task<List<AmazonEntitlement>> FetchOwnedGamesAsync()
        {
            var allEntitlements = new List<AmazonEntitlement>();

            if (string.IsNullOrWhiteSpace(_accessToken))
            {
                if (!await RefreshAccessTokenAsync())
                    return allEntitlements;
            }

            try
            {
                string? nextToken = null;
                var endpointUrl = $"https://{GamingHost}/api/distribution/entitlements";

                // Compute hardwareHash = SHA256(deviceSerial).ToUpper()
                var serial = _deviceSerial ?? "7721A9347744493A8F561F03E3CE2E48";
                var hardwareHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serial))).ToUpperInvariant();

                do
                {
                    var requestBody = new Dictionary<string, object?>
                    {
                        ["Operation"] = "GetEntitlements",
                        ["clientId"] = "Sonic",
                        ["syncPoint"] = null,
                        ["nextToken"] = nextToken,
                        ["maxResults"] = 50,
                        ["productIdFilter"] = null,
                        ["keyId"] = "d5dc8b8b-86c8-4fc4-ae93-18c0def5314d",
                        ["hardwareHash"] = hardwareHash
                    };

                    var jsonPayload = JsonSerializer.Serialize(requestBody);

                    HttpRequestMessage BuildRequest()
                    {
                        var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
                        request.Headers.TryAddWithoutValidation("User-Agent", "com.amazon.agslauncher.win/3.0.9202.1");
                        request.Headers.Add("x-amzn-token", _accessToken);
                        request.Headers.Add("X-Amz-Target", "com.amazon.animusdistributionservice.entitlement.AnimusEntitlementsService.GetEntitlements");
                        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                        // Content-Encoding MUST be added to Content.Headers in .NET, not request.Headers
                        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                        content.Headers.TryAddWithoutValidation("Content-Encoding", "amz-1.0");
                        request.Content = content;

                        return request;
                    }

                    using var request = BuildRequest();
                    var response = await _httpClient.SendAsync(request);
                    var json = await response.Content.ReadAsStringAsync();

                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && await RefreshAccessTokenAsync())
                    {
                        using var retryRequest = BuildRequest();
                        response = await _httpClient.SendAsync(retryRequest);
                    }

                    response.EnsureSuccessStatusCode();

                    if (json.Contains("com.amazon.coral.service#"))
                    {
                        Log.ForContext<AmazonClient>().Error("Amazon EntitlementsService Coral error: {Response}", json);
                        break;
                    }

                    var page = JsonSerializer.Deserialize<AmazonEntitlementsResponse>(json);

                    if (page?.Entitlements != null && page.Entitlements.Any())
                    {
                        allEntitlements.AddRange(page.Entitlements.Where(e => e.Product != null && !string.IsNullOrWhiteSpace(e.Product.Title)));
                        nextToken = page.NextToken;
                    }
                    else
                    {
                        break;
                    }
                } while (!string.IsNullOrEmpty(nextToken));
            }
            catch (Exception ex)
            {
                Log.ForContext<AmazonClient>().Error(ex, "Failed to fetch owned entitlements from Amazon Games.");
            }

            return allEntitlements;
        }

        /// <summary>
        /// Fetches full storefront metadata (descriptions, developer, publisher, genres,
        /// release date, cover, and screenshots) on demand for a single Amazon product ID.
        /// </summary>
        public async Task<AmazonStoreAppDetails?> FetchStoreAppDetailsAsync(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId))
                return null;

            if (string.IsNullOrWhiteSpace(_accessToken) && !await RefreshAccessTokenAsync())
                return null;

            try
            {
                var endpointUrl = $"https://{GamingHost}/api/distribution/entitlements";
                var serial = _deviceSerial ?? "7721A9347744493A8F561F03E3CE2E48";
                var hardwareHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serial))).ToUpperInvariant();

                var requestBody = new Dictionary<string, object?>
                {
                    ["Operation"] = "GetEntitlements",
                    ["clientId"] = "Sonic",
                    ["syncPoint"] = null,
                    ["nextToken"] = null,
                    ["maxResults"] = 10,
                    ["productIdFilter"] = new[] { productId },
                    ["keyId"] = "d5dc8b8b-86c8-4fc4-ae93-18c0def5314d",
                    ["hardwareHash"] = hardwareHash
                };

                var jsonPayload = JsonSerializer.Serialize(requestBody);

                HttpRequestMessage BuildRequest()
                {
                    var req = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
                    req.Headers.TryAddWithoutValidation("User-Agent", "com.amazon.agslauncher.win/3.0.9202.1");
                    req.Headers.Add("x-amzn-token", _accessToken);
                    req.Headers.Add("X-Amz-Target", "com.amazon.animusdistributionservice.entitlement.AnimusEntitlementsService.GetEntitlements");
                    req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    content.Headers.TryAddWithoutValidation("Content-Encoding", "amz-1.0");
                    req.Content = content;

                    return req;
                }

                using var request = BuildRequest();
                var response = await _httpClient.SendAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && await RefreshAccessTokenAsync())
                {
                    using var retry = BuildRequest();
                    response = await _httpClient.SendAsync(retry);
                }

                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();
                var page = JsonSerializer.Deserialize<AmazonEntitlementsResponse>(json);
                var product = page?.Entitlements?
                    .FirstOrDefault(e => string.Equals(e.Product?.Id, productId, StringComparison.OrdinalIgnoreCase))?
                    .Product;

                if (product == null)
                    return null;

                var detail = product.ProductDetail;
                var inner = detail?.Details;

                var details = new AmazonStoreAppDetails
                {
                    ProductId = productId,
                    Title = product.Title ?? string.Empty,
                    ShortDescription = inner?.Description ?? string.Empty,
                    DetailedDescription = inner?.Description ?? string.Empty,
                    CoverUrl = detail?.IconUrl ?? inner?.BackgroundUrl1 ?? inner?.BackgroundUrl2 ?? string.Empty
                };

                if (!string.IsNullOrWhiteSpace(inner?.Developer))
                    details.Developers.Add(inner.Developer);

                if (!string.IsNullOrWhiteSpace(inner?.Publisher))
                    details.Publishers.Add(inner.Publisher);

                if (inner?.Genres != null && inner.Genres.Count > 0)
                    details.Genres.AddRange(inner.Genres);

                if (!string.IsNullOrWhiteSpace(inner?.BackgroundUrl1))
                    details.Screenshots.Add(inner.BackgroundUrl1);

                if (!string.IsNullOrWhiteSpace(inner?.BackgroundUrl2))
                    details.Screenshots.Add(inner.BackgroundUrl2);

                if (!string.IsNullOrWhiteSpace(inner?.BackgroundUrl1) && !details.Screenshots.Contains(inner.BackgroundUrl1))
                    details.Screenshots.Add(inner.BackgroundUrl1);

                if (!string.IsNullOrWhiteSpace(inner?.BackgroundUrl2) && !details.Screenshots.Contains(inner.BackgroundUrl2))
                    details.Screenshots.Add(inner.BackgroundUrl2);

                if (!string.IsNullOrWhiteSpace(inner?.ReleaseDate) &&
                    DateTimeOffset.TryParse(inner.ReleaseDate, out var parsedDate))
                {
                    details.ReleaseDateText = parsedDate.ToString("MMM dd, yyyy");
                }

                details.Platforms.Add("PC (Microsoft Windows)");
                return details;
            }
            catch (Exception ex)
            {
                Log.ForContext<AmazonClient>().Warning(ex, "Failed to fetch Amazon store details for ProductId {ProductId}.", productId);
                return null;
            }
        }


        /// <summary>
        /// Attempts to fetch achievements for an Amazon Luna / Gaming product ID.
        /// </summary>
        public async Task<AmazonGameAchievementsResult?> FetchGameAchievementsAsync(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId) || string.IsNullOrWhiteSpace(_accessToken))
                return null;

            try
            {
                var endpointUrl = $"https://{GamingHost}/graphql";
                var payload = new
                {
                    operationName = "GetGameAchievements",
                    variables = new { productId },
                    query = """
                        query GetGameAchievements($productId: String!) {
                            gameAchievements(productId: $productId) {
                                totalCount
                                unlockedCount
                                items {
                                    id
                                    title
                                    description
                                    unlocked
                                    unlockedAt
                                    rarityPercentage
                                    iconUrl
                                    lockedIconUrl
                                    hidden
                                }
                            }
                        }
                        """
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && await RefreshAccessTokenAsync())
                {
                    using var retry = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
                    retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                    retry.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    response = await _httpClient.SendAsync(retry);
                }

                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();
                var parsed = JsonSerializer.Deserialize<AmazonGraphQlAchievementsResponse>(json);
                var data = parsed?.Data?.GameAchievements;

                if (data == null || data.Items.Count == 0)
                    return null;

                var achievements = data.Items
                    .Select(a =>
                    {
                        bool maskSecret = a.Hidden && !a.Unlocked;
                        return new AmazonPlayerAchievement
                        {
                            Id = a.Id,
                            Name = a.Title,
                            Description = a.Description,
                            Unlocked = a.Unlocked,
                            UnlockDate = a.UnlockedAt,
                            RarityPercent = a.RarityPercentage,
                            UnlockedIconUrl = a.IconUrl,
                            LockedIconUrl = a.LockedIconUrl,
                            IsHidden = a.Hidden
                        };
                    })
                    .OrderByDescending(a => a.Unlocked)
                    .ThenByDescending(a => a.UnlockDate)
                    .ThenBy(a => a.RarityPercent)
                    .ToList();

                return new AmazonGameAchievementsResult
                {
                    ProductId = productId,
                    TotalAchievements = data.TotalCount > 0 ? data.TotalCount : achievements.Count,
                    UnlockedAchievements = achievements.Count(a => a.Unlocked),
                    Achievements = achievements
                };
            }
            catch (Exception ex)
            {
                Log.ForContext<AmazonClient>().Debug(ex, "No achievements available for Amazon product {ProductId}.", productId);
                return null;
            }
        }

        /// <summary>
        /// Reads the local Amazon Games Launcher manifest files to detect installed game product IDs.
        /// </summary>
        public HashSet<string> GetLocallyInstalledGameIds()
        {
            var installedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!OperatingSystem.IsWindows())
                return installedIds;

            try
            {
                string amazonDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Amazon Games", "Data", "Games"
                );

                if (!Directory.Exists(amazonDataDir))
                    return installedIds;

                string installInfoJson = Path.Combine(amazonDataDir, "GameInstallInfo.json");
                if (File.Exists(installInfoJson))
                {
                    var json = File.ReadAllText(installInfoJson);
                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var element in doc.RootElement.EnumerateArray())
                        {
                            if (element.TryGetProperty("Installed", out var instProp) &&
                                instProp.ValueKind == JsonValueKind.True &&
                                element.TryGetProperty("Id", out var idProp))
                            {
                                var id = idProp.GetString();
                                if (!string.IsNullOrWhiteSpace(id))
                                    installedIds.Add(id);
                            }
                        }
                    }
                }

                string defaultGamesDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Amazon Games", "Library"
                );

                if (Directory.Exists(defaultGamesDir))
                {
                    foreach (var fuelFile in Directory.GetFiles(defaultGamesDir, "fuel.json", SearchOption.AllDirectories))
                    {
                        try
                        {
                            var fuelJson = File.ReadAllText(fuelFile);
                            using var fuelDoc = JsonDocument.Parse(fuelJson);
                            if (fuelDoc.RootElement.TryGetProperty("Id", out var idProp))
                            {
                                var id = idProp.GetString();
                                if (!string.IsNullOrWhiteSpace(id))
                                    installedIds.Add(id);
                            }
                        }
                        catch
                        {
                            // Ignore malformed fuel.json files
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ForContext<AmazonClient>().Warning(ex, "Error scanning local Amazon Games installations.");
            }

            return installedIds;
        }
    }
}