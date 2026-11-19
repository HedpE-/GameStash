using GameStash.Core.Models.Epic;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GameStash.Core.Services
{
    public class EpicClient(string? refreshToken = null, string? accessToken = null)
    {
        // Official Epic Games Launcher client credentials used by open-source launchers (Legendary/Heroic)
        public const string ClientId = "34a02cf8f4414e29b15921876da36f9a";
        private const string ClientSecret = "daafbccc737745039dffe53d94fc76cf";

        private const string AccountHost = "account-public-service-prod.ol.epicgames.com";
        private const string _tokenUrl = $"https://{AccountHost}/account/api/oauth/token";
        private const string _verifyUrl = $"https://{AccountHost}/account/api/oauth/verify";

        private const string LibraryHost = "library-service.live.use1a.on.epicgames.com";
        private const string _libraryUrl = $"https://{LibraryHost}/library/api/public/items?includeMetadata=true";
        private const string _playtimeUrlTemplate = $"https://{LibraryHost}/library/api/public/playtime/account/{{0}}/all";

        private const string CatalogHost = "catalog-public-service-prod06.ol.epicgames.com";
        private const string _catalogUrl = $"https://{CatalogHost}/catalog/api/shared/namespace/";

        private const string _graphQlEndpoint = "https://store.epicgames.com/graphql";

        private readonly HttpClient _httpClient = new();
        private string? _accessToken = accessToken;
        private string? _refreshToken = refreshToken;
        private string? _accountId;

        public string? AccessToken => _accessToken;
        public string? RefreshToken => _refreshToken;

        private AuthenticationHeaderValue GetBasicAuthHeader()
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"));
            return new AuthenticationHeaderValue("Basic", credentials);
        }

        /// <summary>
        /// Exchanges the authorization code (or pasted JSON/URL) for access and refresh tokens.
        /// </summary>
        public async Task<EpicAuthTokenResponse?> ExchangeCodeForTokenAsync(string codeOrJson)
        {
            if (string.IsNullOrWhiteSpace(codeOrJson))
                return null;

            string code = codeOrJson.Trim();

            // Extracts the code if the user pasted the entire JSON response from the Epic login page
            if (code.Contains("authorizationCode"))
            {
                var match = Regex.Match(code, @"""authorizationCode""\s*:\s*""([^""]+)""");
                if (match.Success)
                    code = match.Groups[1].Value;
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _tokenUrl);
                request.Headers.Authorization = GetBasicAuthHeader();
                request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "grant_type", "authorization_code" },
                    { "code", code }
                });
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<EpicAuthTokenResponse>(json);

                if (tokenData != null)
                {
                    _accessToken = tokenData.AccessToken;
                    _refreshToken = tokenData.RefreshToken;
                    _accountId = tokenData.AccountId;
                }

                return tokenData;
            }
            catch (Exception ex)
            {
                Log.ForContext<EpicClient>().Error(ex, "Failed to exchange authorization code for Epic Games tokens.");
                return null;
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
                using var request = new HttpRequestMessage(HttpMethod.Post, _tokenUrl);
                request.Headers.Authorization = GetBasicAuthHeader();
                request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "grant_type", "refresh_token" },
                    { "refresh_token", _refreshToken }
                });
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<EpicAuthTokenResponse>(json);

                if (tokenData != null)
                {
                    _accessToken = tokenData.AccessToken;
                    _refreshToken = tokenData.RefreshToken;
                    _accountId = tokenData.AccountId;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.ForContext<EpicClient>().Error(ex, "Failed to refresh Epic Games access token.");
            }

            return false;
        }

        /// <summary>
        /// Validates the current session and returns user profile info.
        /// </summary>
        public async Task<EpicVerifyTokenResponse?> VerifySessionAsync()
        {
            if (string.IsNullOrWhiteSpace(_accessToken))
            {
                if (!await RefreshAccessTokenAsync())
                    return null;
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, _verifyUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var response = await _httpClient.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && await RefreshAccessTokenAsync())
                {
                    using var retry = new HttpRequestMessage(HttpMethod.Get, _verifyUrl);
                    retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                    response = await _httpClient.SendAsync(retry);
                }

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var userData = JsonSerializer.Deserialize<EpicVerifyTokenResponse>(json);

                if (userData != null)
                    _accountId = userData.AccountId;

                return userData;
            }
            catch (Exception ex)
            {
                Log.ForContext<EpicClient>().Error(ex, "Failed to verify Epic Games user session.");
                return null;
            }
        }

        private async Task<bool> EnsureAccountIdAsync()
        {
            if (!string.IsNullOrWhiteSpace(_accountId) && !string.IsNullOrWhiteSpace(_accessToken))
                return true;

            var session = await VerifySessionAsync();
            return session != null && !string.IsNullOrWhiteSpace(_accountId);
        }

        /// <summary>
        /// Fetches all owned games from the Epic library with cursor pagination and resolves catalog titles concurrently.
        /// </summary>
        public async Task<List<EpicCatalogItem>> FetchOwnedGamesAsync()
        {
            var ownedRecords = new List<EpicLibraryRecord>();
            var resolvedGames = new ConcurrentBag<EpicCatalogItem>();

            if (string.IsNullOrWhiteSpace(_accessToken))
            {
                if (!await RefreshAccessTokenAsync())
                    return new List<EpicCatalogItem>();
            }

            try
            {
                string? nextCursor = null;

                // 1. Fetch raw library item identifiers with cursor pagination (Sequential due to cursor dependency)
                do
                {
                    var url = $"https://{LibraryHost}/library/api/public/items?includeMetadata=true";
                    if (!string.IsNullOrEmpty(nextCursor))
                        url += $"&cursor={Uri.EscapeDataString(nextCursor)}";

                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                    var response = await _httpClient.SendAsync(request);
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && await RefreshAccessTokenAsync())
                    {
                        using var retry = new HttpRequestMessage(HttpMethod.Get, url);
                        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                        response = await _httpClient.SendAsync(retry);
                    }

                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    var pageData = JsonSerializer.Deserialize<EpicLibraryResponse>(json);

                    if (pageData?.Records != null)
                        ownedRecords.AddRange(pageData.Records);

                    nextCursor = pageData?.ResponseMetadata?.NextCursor;

                } while (!string.IsNullOrEmpty(nextCursor));

                // 2. Group by Namespace and resolve titles concurrently (Max 10 parallel requests)
                var namespaceGroups = ownedRecords
                    .Where(r => !string.IsNullOrWhiteSpace(r.Namespace) && r.Namespace != "ue")
                    .GroupBy(r => r.Namespace)
                    .ToList();

                using var semaphore = new SemaphoreSlim(10); // Limita a 10 pedidos HTTP simultâneos para evitar HTTP 429

                var tasks = namespaceGroups.Select(async group =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var itemIds = group.Select(g => g.CatalogItemId).Distinct().ToList();
                        var queryParams = string.Join("&", itemIds.Select(id => $"id={Uri.EscapeDataString(id)}"));
                        var catalogUrl = $"https://{CatalogHost}/catalog/api/shared/namespace/{group.Key}/bulk/items?{queryParams}&includeDLCDetails=false&includeMainGameDetails=false&country=US&locale=en";

                        using var catRequest = new HttpRequestMessage(HttpMethod.Get, catalogUrl);
                        catRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                        var catResponse = await _httpClient.SendAsync(catRequest);
                        if (!catResponse.IsSuccessStatusCode) return;

                        var catJson = await catResponse.Content.ReadAsStringAsync();
                        var catalogDict = JsonSerializer.Deserialize<Dictionary<string, EpicCatalogItem>>(catJson);

                        if (catalogDict == null) return;

                        foreach (var item in catalogDict.Values)
                        {
                            if (!string.IsNullOrWhiteSpace(item.Title))
                            {
                                resolvedGames.Add(item);
                            }
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                Log.ForContext<EpicClient>().Error(ex, "Failed to fetch owned games from Epic Games Store.");
            }

            return resolvedGames.ToList();
        }

        /// 
        /// Scans local Epic Games Launcher .item manifest files to detect installed CatalogItemIds.
        /// 
        public HashSet<string> GetLocallyInstalledCatalogIds()
        {
            var installedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!OperatingSystem.IsWindows())
                return installedIds;
            try
            {
                string manifestsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests"
                );
                if (!Directory.Exists(manifestsDir))
                    return installedIds;
                foreach (var file in Directory.GetFiles(manifestsDir, "*.item"))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("CatalogItemId", out var idProp))
                        {
                            var catId = idProp.GetString();
                            if (!string.IsNullOrWhiteSpace(catId))
                                installedIds.Add(catId);
                        }
                    }
                    catch
                    {
                        // Ignore malformed or locked manifest files
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ForContext<EpicClient>().Warning(ex, "Error scanning local Epic Games manifests.");
            }
            return installedIds;
        }

        public async Task<Dictionary<string, int>> FetchPlaytimeAsync()
        {
            if (!await EnsureAccountIdAsync())
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var url = string.Format(_playtimeUrlTemplate, _accountId);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var json = await response.Content.ReadAsStringAsync();
            var items = JsonSerializer.Deserialize<List<EpicPlaytimeItem>>(json) ?? [];

            return items
                .Where(i => !string.IsNullOrWhiteSpace(i.ArtifactId))
                .GroupBy(i => i.ArtifactId!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => (int)(g.Max(x => x.TotalTimeSeconds) / 60), // Convert seconds to minutes
                    StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Fetches player achievements, XP, rarity, and icons for a specific game sandbox (Namespace).
        /// </summary>
        public async Task<EpicGameAchievementsResult?> FetchGameAchievementsAsync(string sandboxId)
        {
            if (string.IsNullOrWhiteSpace(sandboxId) ||
                string.IsNullOrWhiteSpace(_accessToken) ||
                string.IsNullOrWhiteSpace(_accountId))
            {
                return null;
            }

            const string persistedQueryHash = "7489b966f9380d376d2697b518de455765f23c20f2ba13d8d43f1643eecb24f4";

            var variablesJson = JsonSerializer.Serialize(new
            {
                epicAccountId = _accountId,
                sandboxId = sandboxId
            });

            var extensionsJson = JsonSerializer.Serialize(new
            {
                persistedQuery = new
                {
                    version = 1,
                    sha256Hash = persistedQueryHash
                }
            });

            var requestUrl = $"{_graphQlEndpoint}?operationName=PlayerAchievement" +
                 $"&variables={Uri.EscapeDataString(variablesJson)}" +
                 $"&extensions={Uri.EscapeDataString(extensionsJson)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            var parsed = JsonSerializer.Deserialize<EpicGraphQlAchievementResponse>(json);

            var record = parsed?.Data?.PlayerAchievement?.Records?.FirstOrDefault();
            if (record == null || record.TotalAchievements == 0)
                return null;

            var achievements = (record.PlayerAchievements ?? [])
                .Select(node => node.Detail)
                .Where(detail => detail != null)
                .Select(detail =>
                {
                    var meta = detail!.Metadata;
                    bool isUnlocked = detail.Unlocked;

                    string displayName = isUnlocked
                        ? (meta?.UnlockedDisplayName ?? detail.AchievementName ?? "Unknown Achievement")
                        : (meta?.LockedDisplayName ?? meta?.UnlockedDisplayName ?? detail.AchievementName ?? "Hidden Achievement");

                    string description = isUnlocked
                        ? (meta?.UnlockedDescription ?? string.Empty)
                        : (meta?.LockedDescription ?? meta?.UnlockedDescription ?? string.Empty);

                    return new EpicPlayerAchievement
                    {
                        Id = detail.AchievementName ?? string.Empty,
                        Name = displayName,
                        Description = description,
                        Unlocked = isUnlocked,
                        UnlockDate = detail.UnlockDate,
                        Progress = detail.Progress,
                        Xp = detail.Xp,
                        RarityPercent = detail.Rarity?.Percent ?? 0,
                        UnlockedIconUrl = meta?.UnlockedIconLink,
                        LockedIconUrl = meta?.LockedIconLink,
                        IsHidden = meta?.Hidden ?? false
                    };
                })
                .OrderByDescending(a => a.Unlocked)
                .ThenByDescending(a => a.UnlockDate)
                .ThenByDescending(a => a.Progress)
                .ThenByDescending(a => a.Xp)
                .ToList();

            return new EpicGameAchievementsResult
            {
                SandboxId = sandboxId,
                TotalAchievements = record.TotalAchievements,
                UnlockedAchievements = achievements.Count(a => a.Unlocked),
                TotalXp = record.TotalXp,
                EarnedXp = achievements.Where(a => a.Unlocked).Sum(a => a.Xp),
                Achievements = achievements
            };
        }

    }
}