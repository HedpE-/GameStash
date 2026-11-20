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
        private const string GameplayHost = "gameplay.gog.com";

        private readonly HttpClient _httpClient;
        private string? _accessToken;
        private string? _refreshToken;
        private string? _userId;

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
                var url = $"https://auth.gog.com/token?client_id={ClientId}&client_secret={ClientSecret}&grant_type=authorization_code&code={code}&redirect_uri={Uri.EscapeDataString(RedirectUri)}";
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
                var url = $"https://gog.com{ClientId}&client_secret={ClientSecret}&grant_type=refresh_token&refresh_token={_refreshToken}";
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
            if (!await EnsureUserIdAsync())
                return new Dictionary<long, int>();

            try
            {
                var url = $"https://{GameplayHost}/games/users/{_userId}/sessions";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var response = await _httpClient.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && await RefreshAccessTokenAsync())
                {
                    using var retry = new HttpRequestMessage(HttpMethod.Get, url);
                    retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                    response = await _httpClient.SendAsync(retry);
                }

                if (!response.IsSuccessStatusCode)
                    return new Dictionary<long, int>();

                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<GogPlaytimeResponse>(json);

                var result = new Dictionary<long, int>();
                if (data?.Items != null)
                {
                    foreach (var item in data.Items)
                    {
                        if (long.TryParse(item.GameId, out long gameId) && item.MinutesSum > 0)
                        {
                            result[gameId] = item.MinutesSum;
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.ForContext<GogClient>().Warning(ex, "Failed to retrieve GOG playtime sessions.");
                return new Dictionary<long, int>();
            }
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
                            Name = isSecret ? "Hidden Achievement" : item.Name,
                            Description = isSecret ? "Details for this achievement will be revealed once unlocked." : item.Description,
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