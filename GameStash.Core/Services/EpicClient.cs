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
    public class EpicClient
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

        private const string StoreGqlHost = "launcher.store.epicgames.com";
        private const string _graphqlUrl = $"https://{StoreGqlHost}/graphql";

        private readonly HttpClient _httpClient;
        private string? _accessToken;
        private string? _refreshToken;
        private string? _accountId;
        private readonly Func<string, string, Task>? _onTokensRefreshed;

        public EpicClient(
            string? refreshToken = null,
            string? accessToken = null,
            Func<string, string, Task>? onTokensRefreshed = null)
        {
            _refreshToken = refreshToken;
            _accessToken = accessToken;
            _onTokensRefreshed = onTokensRefreshed;
            _httpClient = new();
        }

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

                if (tokenData != null && !string.IsNullOrWhiteSpace(tokenData.AccessToken))
                {
                    _accessToken = tokenData.AccessToken;
                    if (!string.IsNullOrWhiteSpace(tokenData.RefreshToken))
                        _refreshToken = tokenData.RefreshToken;
                    if (!string.IsNullOrWhiteSpace(tokenData.AccountId))
                        _accountId = tokenData.AccountId;

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

                if (tokenData != null && !string.IsNullOrWhiteSpace(tokenData.AccessToken))
                {
                    _accessToken = tokenData.AccessToken;
                    if (!string.IsNullOrWhiteSpace(tokenData.RefreshToken))
                        _refreshToken = tokenData.RefreshToken;
                    if (!string.IsNullOrWhiteSpace(tokenData.AccountId))
                        _accountId = tokenData.AccountId;

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

            using var semaphore = new SemaphoreSlim(10); // Limit to 10 simultaneous HTTP requests to avoid HTTP 429 responses.

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

        /// <summary>
        /// Fetches full storefront metadata (descriptions, developers, publishers, platforms,
        /// release date, cover, and screenshots) on demand from the Epic Games Catalog API.
        /// </summary>
        public async Task<EpicStoreAppDetails?> FetchStoreAppDetailsAsync(string namespaceId, string catalogItemId)
        {
            if (string.IsNullOrWhiteSpace(namespaceId) || string.IsNullOrWhiteSpace(catalogItemId))
                return null;

            if (string.IsNullOrWhiteSpace(_accessToken) && !await RefreshAccessTokenAsync())
                return null;

            try
            {
                var url = $"{_catalogUrl}{Uri.EscapeDataString(namespaceId)}/bulk/items?id={Uri.EscapeDataString(catalogItemId)}&includeDLCDetails=false&includeMainGameDetails=false&country=US&locale=en";

                HttpRequestMessage BuildRequest()
                {
                    var req = new HttpRequestMessage(HttpMethod.Get, url);
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                    req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
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
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty(catalogItemId, out var item) ||
                    item.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                var details = new EpicStoreAppDetails
                {
                    Namespace = namespaceId,
                    CatalogItemId = catalogItemId,
                    Title = item.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? string.Empty : string.Empty,
                    ShortDescription = item.TryGetProperty("description", out var descProp) ? descProp.GetString() ?? string.Empty : string.Empty,
                    DetailedDescription = item.TryGetProperty("longDescription", out var longDescProp) ? longDescProp.GetString() ?? string.Empty : string.Empty
                };

                // Extract developer and publisher (top-level or customAttributes)
                if (item.TryGetProperty("developer", out var devProp) && !string.IsNullOrWhiteSpace(devProp.GetString()))
                {
                    details.Developers.Add(devProp.GetString()!);
                }

                if (item.TryGetProperty("customAttributes", out var attrsNode) && attrsNode.ValueKind == JsonValueKind.Object)
                {
                    if (details.Developers.Count == 0 &&
                        attrsNode.TryGetProperty("developerName", out var devAttr) &&
                        devAttr.TryGetProperty("value", out var devVal) &&
                        !string.IsNullOrWhiteSpace(devVal.GetString()))
                    {
                        details.Developers.Add(devVal.GetString()!);
                    }

                    if (attrsNode.TryGetProperty("publisherName", out var pubAttr) &&
                        pubAttr.TryGetProperty("value", out var pubVal) &&
                        !string.IsNullOrWhiteSpace(pubVal.GetString()))
                    {
                        details.Publishers.Add(pubVal.GetString()!);
                    }
                }

                // Extract release date and supported platforms
                if (item.TryGetProperty("releaseInfo", out var releaseArr) && releaseArr.ValueKind == JsonValueKind.Array)
                {
                    var firstRelease = releaseArr.EnumerateArray().FirstOrDefault();
                    if (firstRelease.ValueKind == JsonValueKind.Object)
                    {
                        if (firstRelease.TryGetProperty("dateAdded", out var dateProp) &&
                            DateTimeOffset.TryParse(dateProp.GetString(), out var parsedDate))
                        {
                            details.ReleaseDateText = parsedDate.ToString("MMM dd, yyyy");
                        }

                        if (firstRelease.TryGetProperty("platform", out var platsArr) && platsArr.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var p in platsArr.EnumerateArray())
                            {
                                var plat = p.GetString();
                                if (string.Equals(plat, "Windows", StringComparison.OrdinalIgnoreCase))
                                    details.Platforms.Add("PC (Microsoft Windows)");
                                else if (string.Equals(plat, "Mac", StringComparison.OrdinalIgnoreCase))
                                    details.Platforms.Add("Mac");
                            }
                        }
                    }
                }

                // Extract vertical cover and wide screenshots from keyImages
                if (item.TryGetProperty("keyImages", out var imagesArr) && imagesArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var img in imagesArr.EnumerateArray())
                    {
                        var type = img.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;
                        var imgUrl = img.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
                        if (string.IsNullOrWhiteSpace(imgUrl)) continue;

                        if (string.IsNullOrWhiteSpace(details.CoverUrl) &&
                            (type == "DieselGameBoxTall" || type == "OfferImageTall" || type == "Thumbnail"))
                        {
                            details.CoverUrl = imgUrl;
                        }
                        else if (type == "DieselGameBox" || type == "OfferImageWide" || type == "Screenshot")
                        {
                            details.Screenshots.Add(imgUrl);
                        }
                    }
                }

                return details;
            }
            catch (Exception ex)
            {
                Log.ForContext<EpicClient>().Warning(ex, "Failed to fetch Epic catalog details for {CatalogItemId}.", catalogItemId);
                return null;
            }
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
        /// Executes a GraphQL request against Epic's official launcher GraphQL endpoint.
        /// </summary>
        private async Task<JsonDocument?> PostGraphQLAsync(object payload)
        {
            var jsonPayload = JsonSerializer.Serialize(payload);

            HttpRequestMessage BuildRequest()
            {
                var req = new HttpRequestMessage(HttpMethod.Post, _graphqlUrl);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                req.Headers.TryAddWithoutValidation("User-Agent", "EpicGamesLauncher/14.0.8-22004686+++Portal+Release-Live");
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                req.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                return req;
            }

            using var request = BuildRequest();
            var response = await _httpClient.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && await RefreshAccessTokenAsync())
            {
                using var retry = BuildRequest();
                response = await _httpClient.SendAsync(retry);
            }

            var resJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Log.ForContext<EpicClient>().Warning(
                    "Epic GraphQL returned {StatusCode} for query: {Body}",
                    response.StatusCode, resJson);
                return null;
            }

            return JsonDocument.Parse(resJson);
        }

        /// <summary>
        /// Fetches player achievements, XP, rarity, and icons for a game sandbox (Namespace)
        /// via Epic's Store GraphQL API.
        /// </summary>
        public async Task<EpicGameAchievementsResult?> FetchGameAchievementsAsync(string sandboxId)
        {
            if (string.IsNullOrWhiteSpace(sandboxId))
                return null;

            if (!await EnsureAccountIdAsync())
                return null;

            // 1. Query Schema Definitions with explicit operationName
            var schemaPayload = new
            {
                operationName = "Achievement",
                query = """
            query Achievement($sandboxId: String!, $locale: String!) {
              Achievement {
                productAchievementsRecordBySandbox(sandboxId: $sandboxId, locale: $locale) {
                  productId
                  sandboxId
                  totalAchievements
                  totalProductXP
                  achievements {
                    achievement {
                      sandboxId
                      deploymentId
                      name
                      hidden
                      isBase
                      achievementSetId
                      unlockedDisplayName
                      lockedDisplayName
                      unlockedDescription
                      lockedDescription
                      unlockedIconLink
                      lockedIconLink
                      XP
                      rarity {
                        percent
                      }
                    }
                  }
                }
              }
            }
            """,
                variables = new { sandboxId, locale = "en-US" }
            };

            // 2. Query Player Unlock Progress with explicit operationName
            var userProgressPayload = new
            {
                operationName = "PlayerAchievement",
                query = """
            query PlayerAchievement($epicAccountId: String!, $sandboxId: String!, $locale: String!) {
              PlayerAchievement {
                playerAchievementGameRecordsBySandbox(
                  epicAccountId: $epicAccountId
                  sandboxId: $sandboxId
                ) {
                  records {
                    totalXP
                    totalUnlocked
                    playerAchievements {
                      playerAchievement {
                        sandboxId
                        epicAccountId
                        unlocked
                        progress
                        XP
                        unlockDate
                        achievementName
                        isBase
                        achievementSetId
                      }
                    }
                  }
                }
              }
            }
            """,
                variables = new { epicAccountId = _accountId, sandboxId, locale = "en-US" }
            };

            // Execute queries concurrently
            var schemaTask = PostGraphQLAsync(schemaPayload);
            var userTask = PostGraphQLAsync(userProgressPayload);
            await Task.WhenAll(schemaTask, userTask);

            using var schemaDoc = await schemaTask;
            using var userDoc = await userTask;

            // If schema query failed (or game doesn't support achievements), return null gracefully
            if (schemaDoc == null)
                return null;

            if (!schemaDoc.RootElement.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("Achievement", out var achRoot) ||
                !achRoot.TryGetProperty("productAchievementsRecordBySandbox", out var prodRecord) ||
                prodRecord.ValueKind != JsonValueKind.Object ||
                !prodRecord.TryGetProperty("achievements", out var achArray) ||
                achArray.ValueKind != JsonValueKind.Array ||
                achArray.GetArrayLength() == 0)
            {
                return null;
            }

            // Build lookup of player unlock records
            var userProgressMap = new Dictionary<string, (bool Unlocked, double Progress, DateTimeOffset? UnlockDate)>(StringComparer.OrdinalIgnoreCase);

            if (userDoc != null &&
                userDoc.RootElement.TryGetProperty("data", out var uData) &&
                uData.TryGetProperty("PlayerAchievement", out var pa) &&
                pa.TryGetProperty("playerAchievementGameRecordsBySandbox", out var paRec) &&
                paRec.TryGetProperty("records", out var records) &&
                records.ValueKind == JsonValueKind.Array)
            {
                foreach (var rec in records.EnumerateArray())
                {
                    if (rec.TryGetProperty("playerAchievements", out var paList) && paList.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var paItem in paList.EnumerateArray())
                        {
                            if (paItem.TryGetProperty("playerAchievement", out var pObj))
                            {
                                string achName = pObj.TryGetProperty("achievementName", out var nProp) ? nProp.GetString() ?? "" : "";
                                if (string.IsNullOrWhiteSpace(achName)) continue;

                                bool unlocked = pObj.TryGetProperty("unlocked", out var uProp) && uProp.ValueKind == JsonValueKind.True;
                                double progress = pObj.TryGetProperty("progress", out var prProp) && prProp.ValueKind == JsonValueKind.Number ? prProp.GetDouble() : (unlocked ? 100.0 : 0.0);
                                DateTimeOffset? unlockDate = null;
                                if (pObj.TryGetProperty("unlockDate", out var dProp) &&
                                    dProp.ValueKind == JsonValueKind.String &&
                                    DateTimeOffset.TryParse(dProp.GetString(), out var parsedDate))
                                {
                                    unlockDate = parsedDate;
                                }

                                userProgressMap[achName] = (unlocked, progress, unlockDate);
                            }
                        }
                    }
                }
            }

            // Merge definitions with player progress
            int totalProductXp = 0;
            var achievements = new List<EpicPlayerAchievement>();

            foreach (var item in achArray.EnumerateArray())
            {
                if (!item.TryGetProperty("achievement", out var achObj))
                    continue;

                string id = achObj.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(id)) continue;

                bool hidden = achObj.TryGetProperty("hidden", out var hProp) && hProp.ValueKind == JsonValueKind.True;
                int xp = achObj.TryGetProperty("XP", out var xpProp) && xpProp.ValueKind == JsonValueKind.Number ? xpProp.GetInt32() : 0;

                double rarityPercent = 0;
                if (achObj.TryGetProperty("rarity", out var rarObj) &&
                    rarObj.ValueKind == JsonValueKind.Object &&
                    rarObj.TryGetProperty("percent", out var pctProp) &&
                    pctProp.ValueKind == JsonValueKind.Number)
                {
                    rarityPercent = pctProp.GetDouble();
                }

                string uDisplayName = achObj.TryGetProperty("unlockedDisplayName", out var udn) ? udn.GetString() ?? "" : "";
                string lDisplayName = achObj.TryGetProperty("lockedDisplayName", out var ldn) ? ldn.GetString() ?? "" : "";
                string uDesc = achObj.TryGetProperty("unlockedDescription", out var udd) ? udd.GetString() ?? "" : "";
                string lDesc = achObj.TryGetProperty("lockedDescription", out var ldd) ? ldd.GetString() ?? "" : "";
                string? uIcon = achObj.TryGetProperty("unlockedIconLink", out var uil) ? uil.GetString() : null;
                string? lIcon = achObj.TryGetProperty("lockedIconLink", out var lil) ? lil.GetString() : null;

                bool unlocked = false;
                double progress = 0.0;
                DateTimeOffset? unlockDate = null;

                if (userProgressMap.TryGetValue(id, out var userProg))
                {
                    unlocked = userProg.Unlocked;
                    progress = userProg.Progress;
                    unlockDate = userProg.UnlockDate;
                }

                string displayName = unlocked && !string.IsNullOrWhiteSpace(uDisplayName)
                    ? uDisplayName
                    : (!string.IsNullOrWhiteSpace(lDisplayName) ? lDisplayName : (!string.IsNullOrWhiteSpace(uDisplayName) ? uDisplayName : id));

                string description = unlocked && !string.IsNullOrWhiteSpace(uDesc)
                    ? uDesc
                    : (!string.IsNullOrWhiteSpace(lDesc) ? lDesc : (!string.IsNullOrWhiteSpace(uDesc) ? uDesc : string.Empty));

                totalProductXp += xp;

                achievements.Add(new EpicPlayerAchievement
                {
                    Id = id,
                    Name = displayName,
                    Description = description,
                    Unlocked = unlocked,
                    UnlockDate = unlockDate,
                    Progress = progress,
                    Xp = xp,
                    RarityPercent = rarityPercent,
                    UnlockedIconUrl = uIcon,
                    LockedIconUrl = lIcon ?? uIcon,
                    IsHidden = hidden
                });
            }

            var ordered = achievements
                .OrderByDescending(a => a.Unlocked)
                .ThenByDescending(a => a.UnlockDate)
                .ThenByDescending(a => a.Progress)
                .ThenByDescending(a => a.Xp)
                .ToList();

            return new EpicGameAchievementsResult
            {
                SandboxId = sandboxId,
                TotalAchievements = ordered.Count,
                UnlockedAchievements = ordered.Count(a => a.Unlocked),
                TotalXp = totalProductXp,
                EarnedXp = ordered.Where(a => a.Unlocked).Sum(a => a.Xp),
                Achievements = ordered
            };
        }
    }
}