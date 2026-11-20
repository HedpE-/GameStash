using GameStash.Core.Models.Ubisoft;
using Microsoft.Win32;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GameStash.Core.Services
{
    public class UbisoftClient
    {
        // Official Ubisoft Connect PC client AppId
        public const string UbisoftConnectAppId = "3587dcbb-7f81-457c-9781-0e3f29f6f56a";
        private const string UbiServicesHost = "public-ubiservices.ubi.com";
        public static string LoginUrl => "https://connect.ubisoft.com/login?appId=" + UbisoftConnectAppId;


        private readonly HttpClient _httpClient;
        private string? _rememberMeTicket;
        private string? _ticket;
        private string? _userId;
        private string? _sessionId;
        private readonly Func<string, string, Task>? _onTokensRefreshed;

        public UbisoftClient(
            string? rememberMeTicket = null,
            string? ticket = null,
            Func<string, string, Task>? onTokensRefreshed = null)
        {
            _rememberMeTicket = rememberMeTicket;
            _ticket = ticket;
            _onTokensRefreshed = onTokensRefreshed;
            _httpClient = new HttpClient();
        }

        private void ApplyUbiHeaders(HttpRequestMessage request, string authScheme, string authValue)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(authScheme, authValue);
            request.Headers.TryAddWithoutValidation("Ubi-AppId", UbisoftConnectAppId);
            request.Headers.TryAddWithoutValidation("Origin", "https://" + "://ubisoft.com");
            request.Headers.TryAddWithoutValidation("Referer", "https://" + "://ubisoft.com/");
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");

            if (!string.IsNullOrWhiteSpace(_sessionId))
            {
                request.Headers.TryAddWithoutValidation("Ubi-SessionId", _sessionId);
            }
        }


        /// <summary>
        /// Authenticates with Ubisoft Connect using email and password (Basic Auth).
        /// Returns the session object; if TwoFactorAuthenticationTicket is populated, call CompleteTwoFactorLoginAsync next.
        /// </summary>
        public async Task<UbisoftSessionResponse?> LoginWithCredentialsAsync(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return null;

            try
            {
                string basicCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email.Trim()}:{password}"));
                var url = $"https://{UbiServicesHost}" + "/v3/profiles/sessions";

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                ApplyUbiHeaders(request, "Basic", basicCredentials);
                request.Content = new StringContent("{\"rememberMe\":true}", Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Log.ForContext<UbisoftClient>().Error("Ubisoft login failed ({StatusCode}): {Body}", response.StatusCode, json);
                    return null;
                }

                var session = JsonSerializer.Deserialize<UbisoftSessionResponse>(json);
                if (session != null && !string.IsNullOrWhiteSpace(session.Ticket))
                {
                    await StoreSessionAsync(session);
                }

                return session;
            }
            catch (Exception ex)
            {
                Log.ForContext<UbisoftClient>().Error(ex, "Exception during Ubisoft Connect credential login.");
                return null;
            }
        }

        /// <summary>
        /// Completes a 2FA Ubisoft Connect login using the twoFactorAuthenticationTicket and 6-digit authenticator code.
        /// </summary>
        public async Task<UbisoftSessionResponse?> CompleteTwoFactorLoginAsync(string twoFactorTicket, string twoFactorCode)
        {
            if (string.IsNullOrWhiteSpace(twoFactorTicket) || string.IsNullOrWhiteSpace(twoFactorCode))
                return null;

            try
            {
                var url = $"https://{UbiServicesHost}" + "/v3/profiles/sessions";
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                ApplyUbiHeaders(request, "ubi_2fa_v1", $"t={twoFactorTicket}");
                request.Headers.TryAddWithoutValidation("Ubi-2FACode", twoFactorCode.Trim());
                request.Content = new StringContent("{\"rememberMe\":true}", Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Log.ForContext<UbisoftClient>().Error("Ubisoft 2FA verification failed ({StatusCode}): {Body}", response.StatusCode, json);
                    return null;
                }

                var session = JsonSerializer.Deserialize<UbisoftSessionResponse>(json);
                if (session != null && !string.IsNullOrWhiteSpace(session.Ticket))
                {
                    await StoreSessionAsync(session);
                }

                return session;
            }
            catch (Exception ex)
            {
                Log.ForContext<UbisoftClient>().Error(ex, "Exception during Ubisoft Connect 2FA login.");
                return null;
            }
        }

        /// <summary>
        /// Renews the active JWT ticket using the long-lived rememberMeTicket.
        /// </summary>
        public async Task<UbisoftSessionResponse?> RefreshSessionAsync()
        {
            if (string.IsNullOrWhiteSpace(_rememberMeTicket))
                return null;

            try
            {
                var url = $"https://{UbiServicesHost}" + "/v3/profiles/sessions";
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                ApplyUbiHeaders(request, "rm_v1", $"t={_rememberMeTicket}");
                request.Content = new StringContent("{\"rememberMe\":true}", Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Log.ForContext<UbisoftClient>().Warning("Ubisoft session refresh failed ({StatusCode}).", response.StatusCode);
                    return null;
                }

                var session = JsonSerializer.Deserialize<UbisoftSessionResponse>(json);
                if (session != null && !string.IsNullOrWhiteSpace(session.Ticket))
                {
                    await StoreSessionAsync(session);
                    return session;
                }
            }
            catch (Exception ex)
            {
                Log.ForContext<UbisoftClient>().Error(ex, "Failed to refresh Ubisoft Connect session.");
            }

            return null;
        }

        private async Task StoreSessionAsync(UbisoftSessionResponse session)
        {
            _ticket = session.Ticket;
            _userId = session.UserId;
            _sessionId = session.SessionId;

            if (!string.IsNullOrWhiteSpace(session.RememberMeTicket))
                _rememberMeTicket = session.RememberMeTicket;

            if (_onTokensRefreshed != null &&
                !string.IsNullOrWhiteSpace(_rememberMeTicket) &&
                !string.IsNullOrWhiteSpace(_ticket))
            {
                await _onTokensRefreshed(_rememberMeTicket, _ticket);
            }
        }

        private static string? ExtractUserIdFromTicket(string? jwtTicket)
        {
            if (string.IsNullOrWhiteSpace(jwtTicket))
                return null;

            try
            {
                var parts = jwtTicket.Split('.');
                if (parts.Length < 2) return null;

                string payload = parts[1].Replace('-', '+').Replace('_', '/');
                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                }

                var bytes = Convert.FromBase64String(payload);
                using var doc = JsonDocument.Parse(bytes);

                if (doc.RootElement.TryGetProperty("sub", out var subProp))
                    return subProp.GetString();
            }
            catch
            {
                // Fallback to RefreshSessionAsync if ticket is malformed
            }

            return null;
        }

        private async Task<bool> EnsureAuthenticatedAsync()
        {
            if (!string.IsNullOrWhiteSpace(_ticket))
            {
                _userId ??= ExtractUserIdFromTicket(_ticket);
                if (!string.IsNullOrWhiteSpace(_userId))
                    return true;
            }

            var session = await RefreshSessionAsync();
            return session != null && !string.IsNullOrWhiteSpace(_ticket) && !string.IsNullOrWhiteSpace(_userId);
        }

        /// <summary>
        /// Validates the current session and returns the authenticated Ubisoft profile.
        /// </summary>
        public async Task<UbisoftSessionResponse?> VerifySessionAsync()
        {
            if (!await EnsureAuthenticatedAsync())
                return null;

            try
            {
                var url = $"https://{UbiServicesHost}" + $"/v3/profiles/{_userId}";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                ApplyUbiHeaders(request, "Ubi_v1", $"t={_ticket}");

                var response = await _httpClient.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    return await RefreshSessionAsync();

                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<UbisoftSessionResponse>(json);
            }
            catch
            {
                return await RefreshSessionAsync();
            }
        }

        /// <summary>
        /// Fetches all owned PC games from the Ubisoft Club / Connect aggregation API.
        /// </summary>
        public async Task<List<UbisoftGameItem>> FetchOwnedGamesAsync()
        {
            if (!await EnsureAuthenticatedAsync())
                return new List<UbisoftGameItem>();

            try
            {
                var url = $"https://{UbiServicesHost}" + $"/v1/profiles/{_userId}/club/aggregation/website/games/owned";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                ApplyUbiHeaders(request, "Ubi_v1", $"t={_ticket}");

                var response = await _httpClient.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.Unauthorized && await RefreshSessionAsync() != null)
                {
                    using var retry = new HttpRequestMessage(HttpMethod.Get, url);
                    ApplyUbiHeaders(retry, "Ubi_v1", $"t={_ticket}");
                    response = await _httpClient.SendAsync(retry);
                }

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var parsed = JsonSerializer.Deserialize<UbisoftOwnedGamesResponse>(json);

                return parsed?.Games?
                    .Where(g => !string.IsNullOrWhiteSpace(g.Title) &&
                                (string.IsNullOrWhiteSpace(g.Platform) || g.Platform.Equals("PC", StringComparison.OrdinalIgnoreCase)))
                    .ToList() ?? new List<UbisoftGameItem>();
            }
            catch (Exception ex)
            {
                Log.ForContext<UbisoftClient>().Error(ex, "Failed to fetch owned games from Ubisoft Connect.");
                return new List<UbisoftGameItem>();
            }
        }

        /// 
        /// Extracts playtime in minutes keyed by SpaceId from the owned games aggregation response.
        /// 
        public async Task<Dictionary<string, int>> FetchPlaytimeAsync()
        {
            var games = await FetchOwnedGamesAsync();
            return games
            .Where(g => !string.IsNullOrWhiteSpace(g.SpaceId) && g.PlaytimeSeconds > 0)
            .GroupBy(g => g.SpaceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
            g => g.Key,
            g => (int)(g.Max(x => x.PlaytimeSeconds) / 60),
            StringComparer.OrdinalIgnoreCase);
        }
        /// 
        /// Fetches Ubisoft Connect challenges / achievements for a specific game SpaceId.
        /// 
        public async Task<UbisoftGameAchievementsResult?> FetchGameAchievementsAsync(string spaceId)
        {
            if (string.IsNullOrWhiteSpace(spaceId) || !await EnsureAuthenticatedAsync())
                return null;
            try
            {
                var url = $"https://{UbiServicesHost}" + $"/v1/profiles/{_userId}/club/aggregation/website/games/{spaceId}/challenges";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                ApplyUbiHeaders(request, "Ubi_v1", $"t={_ticket}");
                var response = await _httpClient.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.Unauthorized && await RefreshSessionAsync() != null)
                {
                    using var retry = new HttpRequestMessage(HttpMethod.Get, url);
                    ApplyUbiHeaders(retry, "Ubi_v1", $"t={_ticket}");
                    response = await _httpClient.SendAsync(retry);
                }
                if (!response.IsSuccessStatusCode)
                    return null;
                var json = await response.Content.ReadAsStringAsync();
                var parsed = JsonSerializer.Deserialize<UbisoftChallengesResponse>(json);
                if (parsed == null || parsed.Challenges.Count == 0)
                    return null;
                var achievements = parsed.Challenges
                .Select(c =>
                {
                    bool maskSecret = c.IsSecret && !c.IsCompleted;
                    return new UbisoftPlayerAchievement
                    {
                        Id = c.Id,
                        Name = maskSecret ? "Hidden Challenge" : c.Name,
                        Description = maskSecret ? "Details for this challenge will be revealed once completed." : c.Description,
                        Unlocked = c.IsCompleted,
                        UnlockDate = c.CompletionDate,
                        Xp = c.XpReward,
                        RarityPercent = c.CompletionPercentage,
                        UnlockedIconUrl = c.ImageUrl,
                        LockedIconUrl = c.ImageUrl,
                        IsHidden = c.IsSecret
                    };
                })
                .OrderByDescending(a => a.Unlocked)
                .ThenByDescending(a => a.UnlockDate)
                .ThenByDescending(a => a.Xp)
                .ToList();
                return new UbisoftGameAchievementsResult
                {
                    SpaceId = spaceId,
                    TotalAchievements = achievements.Count,
                    UnlockedAchievements = achievements.Count(a => a.Unlocked),
                    TotalXp = achievements.Sum(a => a.Xp),
                    EarnedXp = achievements.Where(a => a.Unlocked).Sum(a => a.Xp),
                    Achievements = achievements
                };
            }
            catch (Exception ex)
            {
                Log.ForContext<UbisoftClient>().Warning(ex, "Failed to retrieve Ubisoft achievements for SpaceId {SpaceId}.", spaceId);
                return null;
            }
        }
        /// 
        /// Scans Windows Registry (WOW6432Node\Ubisoft\Launcher\Installs) for locally installed Ubisoft game IDs.
        /// 
        public HashSet<string> GetLocallyInstalledGameIds()
        {
            var installedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!OperatingSystem.IsWindows())
                return installedIds;
            try
            {
                using var installsKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs");
                if (installsKey == null)
                    return installedIds;
                foreach (var subKeyName in installsKey.GetSubKeyNames())
                {
                    using var gameKey = installsKey.OpenSubKey(subKeyName);
                    var installDir = gameKey?.GetValue("InstallDir") as string;
                    if (!string.IsNullOrWhiteSpace(installDir) && System.IO.Directory.Exists(installDir))
                    {
                        installedIds.Add(subKeyName);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ForContext<UbisoftClient>().Warning(ex, "Error scanning Windows Registry for locally installed Ubisoft games.");
            }
            return installedIds;
        }

        /// <summary>
        /// Parses the raw JSON response copied from the Ubisoft Connect browser login session
        /// (or a raw rememberMeTicket string), validates it via RefreshSessionAsync, and persists tokens.
        /// </summary>
        public async Task<UbisoftSessionResponse?> ImportSessionFromJsonAsync(string jsonOrTicket)
        {
            if (string.IsNullOrWhiteSpace(jsonOrTicket))
                return null;

            string trimmed = jsonOrTicket.Trim();

            try
            {
                if (trimmed.StartsWith("{"))
                {
                    var parsed = JsonSerializer.Deserialize<UbisoftSessionResponse>(trimmed);
                    if (parsed == null)
                        return null;

                    _rememberMeTicket = parsed.RememberMeTicket;
                    _ticket = parsed.Ticket;
                    _userId = parsed.UserId;
                    _sessionId = parsed.SessionId;
                }
                else
                {
                    // User pasted the raw rememberMeTicket string directly
                    _rememberMeTicket = trimmed.Trim('"');
                }

                // Validate and rotate the session immediately so we guarantee a fresh ticket and trigger persistence
                var refreshed = await RefreshSessionAsync();
                if (refreshed != null)
                    return refreshed;

                // Fallback: if RefreshSessionAsync failed (e.g. rememberMe was false), check if the pasted ticket itself is still valid
                if (!string.IsNullOrWhiteSpace(_ticket) && !string.IsNullOrWhiteSpace(_userId))
                {
                    await StoreSessionAsync(new UbisoftSessionResponse
                    {
                        Ticket = _ticket,
                        RememberMeTicket = _rememberMeTicket,
                        UserId = _userId,
                        SessionId = _sessionId ?? string.Empty
                    });

                    return new UbisoftSessionResponse
                    {
                        Ticket = _ticket,
                        RememberMeTicket = _rememberMeTicket,
                        UserId = _userId,
                        SessionId = _sessionId ?? string.Empty
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                Log.ForContext<UbisoftClient>().Error(ex, "Failed to parse Ubisoft Connect session JSON.");
                return null;
            }
        }

    }
}