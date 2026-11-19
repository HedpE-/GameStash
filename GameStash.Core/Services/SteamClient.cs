using GameStash.Core.Models.Steam;
using Microsoft.Win32;
using Serilog;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GameStash.Core.Services
{
    public class SteamClient
    {
        private readonly string _steamId64;
        private readonly string _steamApiKey;

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

            var url = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={_steamApiKey}&steamids={_steamId64}";
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
                var url = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key={_steamApiKey}&steamid={_steamId64}&" +
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
    }
}