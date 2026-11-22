using GameStash.Core.Models.Steam;
using Microsoft.Win32;
using Serilog;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GameStash.Core.Services
{
    public class SteamClient
    {
        private readonly string _steamId64;
        private readonly string _steamApiKey;

        private const string SteamApiHost = "api.steampowered.com";
        private const string SteamStoreHost = "store.steampowered.com";

        private readonly HttpClient _httpClient;

        public SteamClient(string steamId64, string steamApiKey)
        {
            _steamId64 = steamId64;
            _steamApiKey = steamApiKey;
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Validates Steam credentials against the official Valve Web API.
        /// </summary>
        public async Task<bool> GetAuthenticationStatusAsync()
        {
            if (string.IsNullOrWhiteSpace(_steamApiKey) || string.IsNullOrWhiteSpace(_steamId64))
                throw new InvalidOperationException("Steam credentials are not configured.");

            var url = $"https://{SteamApiHost}/ISteamUser/GetPlayerSummaries/v0002/?key={_steamApiKey}&steamids={_steamId64}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new InvalidOperationException("Invalid Steam Web API Key.");

                throw new InvalidOperationException($"Steam API returned HTTP {(int)response.StatusCode}.");
            }

            var json = await response.Content.ReadAsStringAsync();
            if (!json.Contains("\"steamid\"") || json.Contains("\"players\":[]"))
                throw new InvalidOperationException("SteamID64 is invalid or profile could not be found.");

            return true;
        }

        /// <summary>
        /// Queries the official Steam Web API to retrieve the user's owned games list.
        /// </summary>
        public async Task<List<SteamRawGame>> FetchOwnedGamesAsync()
        {
            if (string.IsNullOrWhiteSpace(_steamId64) || string.IsNullOrWhiteSpace(_steamApiKey))
            {
                Log.ForContext<SteamClient>().Warning("Steam credentials are missing or empty.");
                return new List<SteamRawGame>();
            }

            try
            {
                var url = $"https://{SteamApiHost}/IPlayerService/GetOwnedGames/v0001/?key={_steamApiKey}&steamid={_steamId64}&" +
                    $"include_appinfo=true&include_played_free_games=true&include_free_sub=true&skip_unvetted_apps=true&" +
                    $"include_all_free_games=true&format=json";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var steamData = JsonSerializer.Deserialize<SteamOwnedGamesResponse>(json);

                return steamData?.Response?.Games?.Where(g => !string.IsNullOrWhiteSpace(g.Name)).ToList()
                       ?? new List<SteamRawGame>();
            }
            catch (Exception ex)
            {
                Log.ForContext<SteamClient>().Error(ex, "Failed to fetch owned games from Steam Web API.");
                return new List<SteamRawGame>();
            }
        }

        /// <summary>
        /// Scans local drive manifests and library folders to detect installed Steam App IDs.
        /// </summary>
        public HashSet<long> GetLocallyInstalledAppIds()
        {
            var installedAppIds = new HashSet<long>();

            try
            {
                string? steamPath = null;

                if (OperatingSystem.IsWindows())
                {
                    steamPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
                }

                if (string.IsNullOrWhiteSpace(steamPath) || !Directory.Exists(steamPath))
                {
                    string defaultPath = @"C:\Program Files (x86)\Steam";
                    if (Directory.Exists(defaultPath)) steamPath = defaultPath;
                }

                if (string.IsNullOrWhiteSpace(steamPath) || !Directory.Exists(steamPath))
                    return installedAppIds;

                var libraryFolders = new List<string> { steamPath };

                // Parse secondary library folders via libraryfolders.vdf
                string vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdfPath))
                {
                    var lines = File.ReadAllLines(vdfPath);
                    foreach (var line in lines)
                    {
                        var match = Regex.Match(line, @"""path""\s+""([^""]+)""");
                        if (match.Success && Directory.Exists(match.Groups[1].Value))
                        {
                            libraryFolders.Add(match.Groups[1].Value.Replace(@"\\", @"\"));
                        }
                    }
                }

                // Check appmanifest_<appid>.acf files across all discovered library folders
                foreach (var folder in libraryFolders.Distinct())
                {
                    string steamApps = Path.Combine(folder, "steamapps");
                    if (!Directory.Exists(steamApps)) continue;

                    var manifestFiles = Directory.GetFiles(steamApps, "appmanifest_*.acf");
                    foreach (var manifest in manifestFiles)
                    {
                        var match = Regex.Match(Path.GetFileName(manifest), @"appmanifest_(\d+)\.acf");
                        if (match.Success && long.TryParse(match.Groups[1].Value, out long appId))
                        {
                            installedAppIds.Add(appId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ForContext<SteamClient>().Warning(ex, "Error scanning local Steam manifests on disk.");
            }

            return installedAppIds;
        }

        /// <summary>
        /// Fetches public storefront metadata (descriptions, screenshots, developers, genres, release date)
        /// from the Steam Store API for games not matched on IGDB.
        /// </summary>
        public async Task<SteamStoreAppDetails?> FetchStoreAppDetailsAsync(long appId)
        {
            if (appId <= 0)
                return null;

            try
            {
                var url = $"https://{SteamStoreHost}/api/appdetails?appids={appId}&l=english";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty(appId.ToString(), out var appNode))
                    return null;

                if (!appNode.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
                    return null;

                if (!appNode.TryGetProperty("data", out var data))
                    return null;

                var result = new SteamStoreAppDetails
                {
                    AppId = appId,
                    Name = data.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? string.Empty : string.Empty,
                    ShortDescription = data.TryGetProperty("short_description", out var shortDesc) ? StripHtmlTags(shortDesc.GetString()) : string.Empty,
                    DetailedDescription = data.TryGetProperty("about_the_game", out var aboutProp) ? StripHtmlTags(aboutProp.GetString()) : string.Empty,
                    HeaderImage = data.TryGetProperty("header_image", out var headerProp) ? headerProp.GetString() ?? string.Empty : string.Empty
                };

                if (data.TryGetProperty("release_date", out var relNode) &&
                    relNode.TryGetProperty("date", out var dateProp))
                {
                    result.ReleaseDateText = dateProp.GetString() ?? string.Empty;
                }

                if (data.TryGetProperty("developers", out var devsArray) && devsArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var d in devsArray.EnumerateArray())
                    {
                        var val = d.GetString();
                        if (!string.IsNullOrWhiteSpace(val)) result.Developers.Add(val);
                    }
                }

                if (data.TryGetProperty("publishers", out var pubsArray) && pubsArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in pubsArray.EnumerateArray())
                    {
                        var val = p.GetString();
                        if (!string.IsNullOrWhiteSpace(val)) result.Publishers.Add(val);
                    }
                }

                if (data.TryGetProperty("genres", out var genresArray) && genresArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var g in genresArray.EnumerateArray())
                    {
                        if (g.TryGetProperty("description", out var descProp))
                        {
                            var val = descProp.GetString();
                            if (!string.IsNullOrWhiteSpace(val)) result.Genres.Add(val);
                        }
                    }
                }

                if (data.TryGetProperty("platforms", out var platNode) && platNode.ValueKind == JsonValueKind.Object)
                {
                    if (platNode.TryGetProperty("windows", out var win) && win.GetBoolean()) result.Platforms.Add("PC (Microsoft Windows)");
                    if (platNode.TryGetProperty("mac", out var mac) && mac.GetBoolean()) result.Platforms.Add("Mac");
                    if (platNode.TryGetProperty("linux", out var lin) && lin.GetBoolean()) result.Platforms.Add("Linux");
                }

                if (data.TryGetProperty("screenshots", out var shotsArray) && shotsArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in shotsArray.EnumerateArray())
                    {
                        if (s.TryGetProperty("path_full", out var fullProp))
                        {
                            var val = fullProp.GetString();
                            if (!string.IsNullOrWhiteSpace(val)) result.Screenshots.Add(val);
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.ForContext<SteamClient>().Warning(ex, "Failed to fetch Steam Store appdetails for AppId {AppId}.", appId);
                return null;
            }
        }

        private static string StripHtmlTags(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var decoded = WebUtility.HtmlDecode(input);
            var withoutTags = Regex.Replace(decoded, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
            withoutTags = Regex.Replace(withoutTags, "<[^>]+>", string.Empty);
            return withoutTags.Trim();
        }

        /// <summary>
        /// Fetches player achievements, unlock timestamps, icons, and global rarity for a specific Steam App ID.
        /// </summary>
        public async Task<SteamGameAchievementsResult?> FetchGameAchievementsAsync(long appId)
        {
            if (appId <= 0 || string.IsNullOrWhiteSpace(_steamId64) || string.IsNullOrWhiteSpace(_steamApiKey))
                return null;

            try
            {
                var playerUrl = $"https://{SteamApiHost}/ISteamUserStats/GetPlayerAchievements/v0001/?appid={appId}&key={_steamApiKey}&steamid={_steamId64}&l=english";
                var schemaUrl = $"https://{SteamApiHost}/ISteamUserStats/GetSchemaForGame/v2/?appid={appId}&key={_steamApiKey}&l=english";
                var rarityUrl = $"https://{SteamApiHost}/ISteamUserStats/GetGlobalAchievementPercentagesForApp/v0002/?gameid={appId}";

                var playerTask = _httpClient.GetAsync(playerUrl);
                var schemaTask = _httpClient.GetAsync(schemaUrl);
                var rarityTask = _httpClient.GetAsync(rarityUrl);

                await Task.WhenAll(playerTask, schemaTask, rarityTask);

                var playerResponse = await playerTask;
                if (!playerResponse.IsSuccessStatusCode)
                    return null;

                var playerJson = await playerResponse.Content.ReadAsStringAsync();
                var playerData = JsonSerializer.Deserialize<SteamPlayerAchievementsResponse>(playerJson);

                var rawAchievements = playerData?.PlayerStats?.Achievements;
                if (playerData?.PlayerStats?.Success != true || rawAchievements == null || rawAchievements.Count == 0)
                    return null;

                // Parse optional schema metadata (icons & hidden flags)
                var schemaLookup = new Dictionary<string, SteamSchemaAchievement>(StringComparer.OrdinalIgnoreCase);
                var schemaResponse = await schemaTask;
                if (schemaResponse.IsSuccessStatusCode)
                {
                    var schemaJson = await schemaResponse.Content.ReadAsStringAsync();
                    var schemaData = JsonSerializer.Deserialize<SteamGameSchemaResponse>(schemaJson);
                    var schemaItems = schemaData?.Game?.AvailableGameStats?.Achievements;

                    if (schemaItems != null)
                    {
                        schemaLookup = schemaItems
                            .Where(a => !string.IsNullOrWhiteSpace(a.Name))
                            .GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                    }
                }

                // Parse optional global rarity percentages
                var rarityLookup = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                var rarityResponse = await rarityTask;
                if (rarityResponse.IsSuccessStatusCode)
                {
                    var rarityJson = await rarityResponse.Content.ReadAsStringAsync();
                    var rarityData = JsonSerializer.Deserialize<SteamGlobalPercentagesResponse>(rarityJson);
                    var rarityItems = rarityData?.AchievementPercentages?.Achievements;

                    if (rarityItems != null)
                    {
                        rarityLookup = rarityItems
                            .Where(r => !string.IsNullOrWhiteSpace(r.Name))
                            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(g => g.Key, g => Math.Round(g.First().Percent, 2), StringComparer.OrdinalIgnoreCase);
                    }
                }

                var achievements = rawAchievements
                    .Select(raw =>
                    {
                        schemaLookup.TryGetValue(raw.ApiName, out var schema);
                        rarityLookup.TryGetValue(raw.ApiName, out double rarity);

                        bool isUnlocked = raw.Achieved == 1;
                        bool isHidden = schema?.Hidden == 1;
                        bool maskSecret = isHidden && !isUnlocked;

                        string displayName = !string.IsNullOrWhiteSpace(raw.Name)
                            ? raw.Name
                            : (schema?.DisplayName ?? raw.ApiName);

                        string description = !string.IsNullOrWhiteSpace(raw.Description)
                            ? raw.Description
                            : (schema?.Description ?? string.Empty);

                        DateTimeOffset? unlockDate = (isUnlocked && raw.UnlockTime > 0)
                            ? DateTimeOffset.FromUnixTimeSeconds(raw.UnlockTime)
                            : null;

                        return new SteamPlayerAchievement
                        {
                            Id = raw.ApiName,
                            Name = displayName,
                            Description = description,
                            Unlocked = isUnlocked,
                            UnlockDate = unlockDate,
                            RarityPercent = rarity,
                            UnlockedIconUrl = schema?.Icon,
                            LockedIconUrl = schema?.IconGray,
                            IsHidden = isHidden
                        };
                    })
                    .OrderByDescending(a => a.Unlocked)
                    .ThenByDescending(a => a.UnlockDate)
                    .ThenBy(a => a.RarityPercent)
                    .ToList();

                return new SteamGameAchievementsResult
                {
                    AppId = appId,
                    GameName = playerData.PlayerStats.GameName ?? string.Empty,
                    TotalAchievements = achievements.Count,
                    UnlockedAchievements = achievements.Count(a => a.Unlocked),
                    Achievements = achievements
                };
            }
            catch (Exception ex)
            {
                Log.ForContext<SteamClient>().Warning(ex, "Failed to fetch Steam achievements for App ID {AppId}.", appId);
                return null;
            }
        }

        /// <summary>
        /// Queries the Steam Store API in batch (with filters=basic) to identify deprecated or redirected
        /// duplicate AppIDs (e.g. old Mac aliases 34440 -> 3900, 34460 -> 8800).
        /// </summary>
        public async Task<HashSet<long>> GetNonCanonicalStoreAppIdsAsync(IEnumerable<long> candidateAppIds)
        {
            var invalidIds = new HashSet<long>();
            var distinctIds = candidateAppIds.Where(id => id > 0).Distinct().ToList();
            if (distinctIds.Count == 0)
                return invalidIds;

            var tasks = distinctIds.Select(async appId =>
            {
                try
                {
                    var url = $"https://{SteamStoreHost}/api/appdetails?appids={appId}";
                    var response = await _httpClient.GetAsync(url);
                    var json = await response.Content.ReadAsStringAsync();

                    // Fail-open on HTTP rate limit or transient errors so valid games aren't dropped
                    if (!response.IsSuccessStatusCode)
                        return;

                    using var doc = JsonDocument.Parse(json);

                    if (!doc.RootElement.TryGetProperty(appId.ToString(), out var appNode))
                        return;

                    if (!appNode.TryGetProperty("success", out var successProp) ||
                        successProp.ValueKind != JsonValueKind.True)
                    {
                        lock (invalidIds) invalidIds.Add(appId);
                        return;
                    }

                    if (appNode.TryGetProperty("data", out var dataNode) &&
                        dataNode.TryGetProperty("steam_appid", out var canonicalIdProp) &&
                        canonicalIdProp.TryGetInt64(out long canonicalId) &&
                        canonicalId != appId)
                    {
                        lock (invalidIds) invalidIds.Add(appId);
                    }
                }
                catch (Exception ex)
                {
                    Log.ForContext<SteamClient>().Warning(ex, "Failed canonical AppID check on Steam Store API for {AppId}.", appId);
                }
            });

            await Task.WhenAll(tasks);
            return invalidIds;
        }
    }
}