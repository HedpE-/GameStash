using GameStash.Core.Models.Gog;
using Microsoft.Win32;
using Serilog;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GameStash.Core.Services
{
    public class GogClient
    {
        // Public official GOG Galaxy client credentials used across open-source launchers
        private const string ClientId = "46899977096215655";
        private const string ClientSecret = "9d85c43b1482497dbbce61f6e4aa173a433796eeae2ca8c5f6129f2dc4de46d9";
        private const string RedirectUri = "https://embed.gog.com/on_login_success?origin=client";
        private const string AuthHost = "auth.gog.com";
        private const string ApiHost = "api.gog.com";
        private const string GameplayHost = "gameplay.gog.com";

        private readonly HttpClient _httpClient;
        private string? _accessToken;
        private string? _refreshToken;
        private string? _userId;
        private string? _username;

        private readonly Func<string, string, Task>? _onTokensRefreshed;

        public GogClient(
            string? refreshToken = null,
            string? accessToken = null,
            Func<string, string, Task>? onTokensRefreshed = null)
        {
            _refreshToken = refreshToken;
            _accessToken = accessToken;
            _onTokensRefreshed = onTokensRefreshed;
            _httpClient = new();
        }

        /// <summary>
        /// Exchanges authorization code (or redirect URL) for access and refresh tokens.
        /// </summary>
        public async Task<GogTokenResponse?> ExchangeCodeForTokenAsync(string codeOrUrl)
        {
            if (string.IsNullOrWhiteSpace(codeOrUrl))
                return null;

            // Extract code if user pasted the full redirect URL
            string code = codeOrUrl.Trim();
            if (code.Contains("code="))
            {
                var match = Regex.Match(code, @"code=([^&]+)");
                if (match.Success)
                    code = match.Groups[1].Value;
            }

            try
            {
                var url = $"https://{AuthHost}/token?client_id={ClientId}&client_secret={ClientSecret}&grant_type=authorization_code&code={code}&redirect_uri={Uri.EscapeDataString(RedirectUri)}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<GogTokenResponse>(json);

                if (tokenData != null && !string.IsNullOrWhiteSpace(tokenData.AccessToken))
                {
                    _accessToken = tokenData.AccessToken;
                    if (!string.IsNullOrWhiteSpace(tokenData.RefreshToken))
                        _refreshToken = tokenData.RefreshToken;
                    if (!string.IsNullOrWhiteSpace(tokenData.UserId))
                        _userId = tokenData.UserId;

                    if (_onTokensRefreshed != null &&
                        !string.IsNullOrWhiteSpace(_refreshToken) &&
                        !string.IsNullOrWhiteSpace(_accessToken))
                    {
                        await _onTokensRefreshed(_refreshToken, _accessToken);
                    }
                }

                return tokenData;
            }
            catch (Exception ex)
            {
                Log.ForContext<GogClient>().Error(ex, "Failed to exchange authorization code for GOG tokens.");
                return null;
            }
        }

        /// <summary>
        /// Refreshes expired access token using the stored refresh token.
        /// </summary>
        public async Task<bool> RefreshAccessTokenAsync()
        {
            if (string.IsNullOrWhiteSpace(_refreshToken))
                return false;

            try
            {
                var url = $"https://{AuthHost}/token?client_id={ClientId}&client_secret={ClientSecret}&grant_type=refresh_token&refresh_token={_refreshToken}";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    return false;

                var json = await response.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<GogTokenResponse>(json);

                if (tokenData != null && !string.IsNullOrWhiteSpace(tokenData.AccessToken))
                {
                    _accessToken = tokenData.AccessToken;
                    if (!string.IsNullOrWhiteSpace(tokenData.RefreshToken))
                        _refreshToken = tokenData.RefreshToken;
                    if (!string.IsNullOrWhiteSpace(tokenData.UserId))
                        _userId = tokenData.UserId;

                    if (_onTokensRefreshed != null &&
                        !string.IsNullOrWhiteSpace(_refreshToken) &&
                        !string.IsNullOrWhiteSpace(_accessToken))
                    {
                        await _onTokensRefreshed(_refreshToken, _accessToken);
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.ForContext<GogClient>().Error(ex, "Failed to refresh GOG access token.");
            }

            return false;
        }

        // Helper to ensure _userId is available before calling user-scoped endpoints:
        private async Task<bool> EnsureUserIdAsync()
        {
            if (!string.IsNullOrWhiteSpace(_userId) && !string.IsNullOrWhiteSpace(_accessToken))
                return true;

            var userData = await GetUserDataAsync();
            if (userData != null && !string.IsNullOrWhiteSpace(userData.UserId))
            {
                _userId = userData.UserId;
                _username = userData.Username;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Validates current session by fetching user profile data.
        /// </summary>
        public async Task<GogUserData?> GetUserDataAsync()
        {
            if (string.IsNullOrWhiteSpace(_accessToken))
            {
                var refreshed = await RefreshAccessTokenAsync();
                if (!refreshed) return null;
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://embed.gog.com/userData.json");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var response = await _httpClient.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    if (await RefreshAccessTokenAsync())
                    {
                        using var retryRequest = new HttpRequestMessage(HttpMethod.Get, "https://embed.gog.com/userData.json");
                        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                        response = await _httpClient.SendAsync(retryRequest);
                    }
                }

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<GogUserData>(json);
            }
            catch (Exception ex)
            {
                Log.ForContext<GogClient>().Error(ex, "Failed to retrieve GOG user profile.");
                return null;
            }
        }

        /// <summary>
        /// Fetches all owned games from GOG account with pagination.
        /// </summary>
        public async Task<List<GogProduct>> FetchOwnedGamesAsync()
        {
            var allProducts = new List<GogProduct>();
            int page = 1;
            int totalPages = 1;

            if (string.IsNullOrWhiteSpace(_accessToken))
            {
                var refreshed = await RefreshAccessTokenAsync();
                if (!refreshed) return allProducts;
            }

            try
            {
                do
                {
                    var url = $"https://embed.gog.com/account/getFilteredProducts?mediaType=1&page={page}";
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                    var response = await _httpClient.SendAsync(request);
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        if (await RefreshAccessTokenAsync())
                        {
                            using var retry = new HttpRequestMessage(HttpMethod.Get, url);
                            retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                            response = await _httpClient.SendAsync(retry);
                        }
                    }

                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<GogProductsPage>(json);

                    if (data?.Products != null)
                    {
                        allProducts.AddRange(data.Products);
                        totalPages = data.TotalPages;
                    }
                    else
                    {
                        break;
                    }

                    page++;
                } while (page <= totalPages);
            }
            catch (Exception ex)
            {
                Log.ForContext<GogClient>().Error(ex, "Failed to fetch owned GOG products.");
            }

            return allProducts;
        }

        /// <summary>
        /// Fetches full storefront metadata (descriptions, developers, publishers, platforms,
        /// release date, cover, and screenshots) on demand from the public GOG Products API.
        /// </summary>
        public async Task<GogStoreAppDetails?> FetchStoreAppDetailsAsync(long gogGameId)
        {
            if (gogGameId <= 0)
                return null;

            try
            {
                var url = $"https://{ApiHost}/products/{gogGameId}?expand=description,screenshots";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var details = new GogStoreAppDetails
                {
                    GameId = gogGameId,
                    Title = root.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? string.Empty : string.Empty
                };

                if (root.TryGetProperty("description", out var descNode) && descNode.ValueKind == JsonValueKind.Object)
                {
                    if (descNode.TryGetProperty("lead", out var leadProp))
                        details.ShortDescription = StripHtml(leadProp.GetString());

                    if (descNode.TryGetProperty("full", out var fullProp))
                        details.DetailedDescription = StripHtml(fullProp.GetString());
                }

                if (root.TryGetProperty("release_date", out var relProp) &&
                    relProp.ValueKind == JsonValueKind.String &&
                    DateTimeOffset.TryParse(relProp.GetString(), out var parsedDate))
                {
                    details.ReleaseDateText = parsedDate.ToString("MMM dd, yyyy");
                }

                if (root.TryGetProperty("developers", out var devsArr) && devsArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var d in devsArr.EnumerateArray())
                    {
                        var val = d.GetString();
                        if (!string.IsNullOrWhiteSpace(val)) details.Developers.Add(val);
                    }
                }

                if (root.TryGetProperty("publishers", out var pubsArr) && pubsArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in pubsArr.EnumerateArray())
                    {
                        var val = p.GetString();
                        if (!string.IsNullOrWhiteSpace(val)) details.Publishers.Add(val);
                    }
                }

                if (root.TryGetProperty("genres", out var genresArr) && genresArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var g in genresArr.EnumerateArray())
                    {
                        var val = g.GetString();
                        if (!string.IsNullOrWhiteSpace(val)) details.Genres.Add(val);
                    }
                }

                if (root.TryGetProperty("content_system_compatibility", out var osNode) && osNode.ValueKind == JsonValueKind.Object)
                {
                    if (osNode.TryGetProperty("windows", out var w) && w.ValueKind == JsonValueKind.True)
                        details.Platforms.Add("PC (Microsoft Windows)");
                    if (osNode.TryGetProperty("osx", out var m) && m.ValueKind == JsonValueKind.True)
                        details.Platforms.Add("Mac");
                    if (osNode.TryGetProperty("linux", out var l) && l.ValueKind == JsonValueKind.True)
                        details.Platforms.Add("Linux");
                }

                if (root.TryGetProperty("images", out var imgNode) && imgNode.ValueKind == JsonValueKind.Object)
                {
                    if (imgNode.TryGetProperty("logo2x", out var logoProp))
                    {
                        var raw = logoProp.GetString();
                        if (!string.IsNullOrWhiteSpace(raw))
                            details.CoverUrl = raw.StartsWith("//") ? $"https:{raw}" : raw;
                    }
                }

                if (root.TryGetProperty("screenshots", out var shotsArr) && shotsArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var shot in shotsArr.EnumerateArray())
                    {
                        if (shot.TryGetProperty("formatted_images", out var fmtArr) && fmtArr.ValueKind == JsonValueKind.Array)
                        {
                            var fullShot = fmtArr.EnumerateArray()
                                .FirstOrDefault(f => f.TryGetProperty("formatter_name", out var fn) && fn.GetString() == "ggvgm_2x");

                            var target = fullShot.ValueKind == JsonValueKind.Object
                                ? fullShot
                                : fmtArr.EnumerateArray().FirstOrDefault();

                            if (target.ValueKind == JsonValueKind.Object &&
                                target.TryGetProperty("image_url", out var urlProp))
                            {
                                var urlStr = urlProp.GetString();
                                if (!string.IsNullOrWhiteSpace(urlStr))
                                    details.Screenshots.Add(urlStr);
                            }
                        }
                    }
                }

                return details;
            }
            catch (Exception ex)
            {
                Log.ForContext<GogClient>().Warning(ex, "Failed to fetch GOG store details for ID {GogGameId}.", gogGameId);
                return null;
            }
        }

        private static string StripHtml(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var decoded = System.Net.WebUtility.HtmlDecode(input);
            var clean = Regex.Replace(decoded, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
            return Regex.Replace(clean, "<[^>]+>", string.Empty).Trim();
        }


        /// <summary>
        /// Scans local Windows registry for installed GOG games.
        /// </summary>
        public HashSet<long> GetLocallyInstalledGameIds()
        {
            var installedIds = new HashSet<long>();

            if (!OperatingSystem.IsWindows())
                return installedIds;

            try
            {
                // GOG registers installations under HKLM\SOFTWARE\WOW6432Node\GOG.com\Games
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games")
                                ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\GOG.com\Games");

                if (key != null)
                {
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        var gameIdVal = subKey?.GetValue("gameID")?.ToString() ?? subKeyName;
                        if (long.TryParse(gameIdVal, out var gogId))
                        {
                            installedIds.Add(gogId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ForContext<GogClient>().Warning(ex, "Error scanning local GOG installations in Windows Registry.");
            }

            return installedIds;
        }

        /// <summary>
        /// Fetches accumulated playtime in minutes for all games played via GOG Galaxy.
        /// </summary>
        public async Task<Dictionary<long, int>> FetchPlaytimeAsync()
        {
            var result = new Dictionary<long, int>();

            if (!await EnsureUserIdAsync() || string.IsNullOrWhiteSpace(_username))
                return result;

            try
            {
                int page = 1;
                int totalPages = 1;

                do
                {
                    var url = $"https://www.gog.com/u/{Uri.EscapeDataString(_username)}/games/stats?page={page}";
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var response = await _httpClient.SendAsync(request);
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && await RefreshAccessTokenAsync())
                    {
                        using var retry = new HttpRequestMessage(HttpMethod.Get, url);
                        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                        retry.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        response = await _httpClient.SendAsync(retry);
                    }

                    if (!response.IsSuccessStatusCode)
                        break;

                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("pages", out var pagesProp) && pagesProp.ValueKind == JsonValueKind.Number)
                        totalPages = pagesProp.GetInt32();

                    if (doc.RootElement.TryGetProperty("_embedded", out var embedded) &&
                        embedded.TryGetProperty("items", out var items) &&
                        items.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in items.EnumerateArray())
                        {
                            long gameId = 0;
                            if (item.TryGetProperty("game", out var gameNode) &&
                                gameNode.TryGetProperty("id", out var idProp))
                            {
                                long.TryParse(idProp.ToString(), out gameId);
                            }

                            int minutes = 0;
                            if (item.TryGetProperty("stats", out var statsNode) && statsNode.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var userStat in statsNode.EnumerateObject())
                                {
                                    if (userStat.Value.TryGetProperty("playtime", out var ptProp) && ptProp.ValueKind == JsonValueKind.Number)
                                    {
                                        minutes = ptProp.GetInt32();
                                        break;
                                    }
                                }
                            }

                            if (gameId > 0 && minutes > 0)
                                result[gameId] = minutes;
                        }
                    }
                    else
                    {
                        break;
                    }

                    page++;
                } while (page <= totalPages);
            }
            catch (Exception ex)
            {
                Log.ForContext<GogClient>().Warning(ex, "Failed to retrieve GOG playtime from web stats endpoint.");
            }

            return result;
        }

        /// <summary>
        /// Fetches player achievements, unlock dates, rarity, and icons for a specific GOG product ID.
        /// </summary>
        public async Task<GogGameAchievementsResult?> FetchGameAchievementsAsync(long gogGameId)
        {
            if (gogGameId <= 0 || !await EnsureUserIdAsync())
                return null;

            try
            {
                var url = $"https://{GameplayHost}/clients/{gogGameId}/users/{_userId}/achievements";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                request.Headers.TryAddWithoutValidation("Accept-Language", "en-US");

                var response = await _httpClient.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && await RefreshAccessTokenAsync())
                {
                    using var retry = new HttpRequestMessage(HttpMethod.Get, url);
                    retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                    retry.Headers.TryAddWithoutValidation("Accept-Language", "en-US");
                    response = await _httpClient.SendAsync(retry);
                }

                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();
                var parsed = JsonSerializer.Deserialize<GogAchievementsResponse>(json);

                if (parsed == null || parsed.Items.Count == 0)
                    return null;

                var achievements = parsed.Items
                    .Select(item =>
                    {
                        bool isUnlocked = item.DateUnlocked.HasValue;
                        bool isSecret = !item.Visible && !isUnlocked;

                        return new GogPlayerAchievement
                        {
                            Id = item.AchievementId,
                            Key = item.AchievementKey,
                            Name = item.Name,
                            Description = item.Description,
                            Unlocked = isUnlocked,
                            UnlockDate = item.DateUnlocked,
                            RarityPercent = item.Rarity,
                            UnlockedIconUrl = item.ImageUrlUnlocked,
                            LockedIconUrl = item.ImageUrlLocked,
                            IsHidden = !item.Visible
                        };
                    })
                    .OrderByDescending(a => a.Unlocked)
                    .ThenByDescending(a => a.UnlockDate)
                    .ThenBy(a => a.RarityPercent)
                    .ToList();

                return new GogGameAchievementsResult
                {
                    GameId = gogGameId,
                    TotalAchievements = parsed.TotalCount > 0 ? parsed.TotalCount : achievements.Count,
                    UnlockedAchievements = achievements.Count(a => a.Unlocked),
                    Achievements = achievements
                };
            }
            catch (Exception ex)
            {
                Log.ForContext<GogClient>().Warning(ex, "Failed to retrieve GOG achievements for game ID {GameId}.", gogGameId);
                return null;
            }
        }

    }
}