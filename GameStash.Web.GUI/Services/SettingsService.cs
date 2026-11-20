using GameStash.Core.Data;
using GameStash.Core.Models;
using GameStash.Core.Services;
using GameStash.Web.GUI.Interfaces;
using IgdbSharp;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Services
{
    public class SettingsService : ISettingsService
    {
        // IGDB Credentials
        private string IgdbClientId
        {
            get
            {
                using var context = GameStashDbContext.CreateDbContext();
                return context.Settings.FirstOrDefault()?.IGDB_Client_ID ?? string.Empty;
            }
            set {
                EnsureSettingsEntityExists();
                using var context = GameStashDbContext.CreateDbContext();
                context.Settings.FirstOrDefault().IGDB_Client_ID = value;
                context.SaveChanges();
            }
        }

        private string IgdbSecret
        {
            get
            {
                using var context = GameStashDbContext.CreateDbContext();
                return context.Settings.FirstOrDefault()?.IGDB_Secret ?? string.Empty;
            }
            set
            {
                EnsureSettingsEntityExists();
                using var context = GameStashDbContext.CreateDbContext();
                context.Settings.FirstOrDefault().IGDB_Secret = value;
                context.SaveChanges();
            }
        }

        // Steam Credentials
        private string SteamId64
        {
            get
            {
                using var context = GameStashDbContext.CreateDbContext();
                return context.Settings.FirstOrDefault()?.SteamId64 ?? string.Empty;
            }
            set
            {
                EnsureSettingsEntityExists();
                using var context = GameStashDbContext.CreateDbContext();
                context.Settings.FirstOrDefault().SteamId64 = value;
                context.SaveChanges();
            }
        }

        private string SteamApiKey
        {
            get
            {
                using var context = GameStashDbContext.CreateDbContext();
                return context.Settings.FirstOrDefault()?.SteamApiKey ?? string.Empty;
            }
            set
            {
                EnsureSettingsEntityExists();
                using var context = GameStashDbContext.CreateDbContext();
                context.Settings.FirstOrDefault().SteamApiKey = value;
                context.SaveChanges();
            }
        }

        // Epic Games Credentials & Tokens
        private string EpicRefreshToken
        {
            get
            {
                using var context = GameStashDbContext.CreateDbContext();
                return context.Settings.FirstOrDefault()?.EpicRefreshToken ?? string.Empty;
            }
            set
            {
                EnsureSettingsEntityExists();
                using var context = GameStashDbContext.CreateDbContext();
                context.Settings.FirstOrDefault().EpicRefreshToken = value;
                context.SaveChanges();
            }
        }

        private string EpicAccessToken
        {
            get
            {
                using var context = GameStashDbContext.CreateDbContext();
                return context.Settings.FirstOrDefault()?.EpicAccessToken ?? string.Empty;
            }
            set
            {
                EnsureSettingsEntityExists();
                using var context = GameStashDbContext.CreateDbContext();
                context.Settings.FirstOrDefault().EpicAccessToken = value;
                context.SaveChanges();
            }
        }

        // GOG Credentials & Tokens
        private string GogRefreshToken
        {
            get
            {
                using var context = GameStashDbContext.CreateDbContext();
                return context.Settings.FirstOrDefault()?.GogRefreshToken ?? string.Empty;
            }
            set
            {
                EnsureSettingsEntityExists();
                using var context = GameStashDbContext.CreateDbContext();
                context.Settings.FirstOrDefault().GogRefreshToken = value;
                context.SaveChanges();
            }
        }

        private string GogAccessToken
        {
            get
            {
                using var context = GameStashDbContext.CreateDbContext();
                return context.Settings.FirstOrDefault()?.GogAccessToken ?? string.Empty;
            }
            set
            {
                EnsureSettingsEntityExists();
                using var context = GameStashDbContext.CreateDbContext();
                context.Settings.FirstOrDefault().GogAccessToken = value;
                context.SaveChanges();
            }
        }

        // Amazon Credentials & Tokens
        private string AmazonRefreshToken
        {
            get
            {
                using var context = GameStashDbContext.CreateDbContext();
                return context.Settings.FirstOrDefault()?.AmazonRefreshToken ?? string.Empty;
            }
            set
            {
                EnsureSettingsEntityExists();
                using var context = GameStashDbContext.CreateDbContext();
                context.Settings.FirstOrDefault().AmazonRefreshToken = value;
                context.SaveChanges();
            }
        }

        private string AmazonAccessToken
        {
            get
            {
                using var context = GameStashDbContext.CreateDbContext();
                return context.Settings.FirstOrDefault()?.AmazonAccessToken ?? string.Empty;
            }
            set
            {
                EnsureSettingsEntityExists();
                using var context = GameStashDbContext.CreateDbContext();
                context.Settings.FirstOrDefault().AmazonAccessToken = value;
                context.SaveChanges();
            }
        }

        private (string CodeVerifier, string DeviceSerial)? _pendingAmazonPkce;

        private void EnsureSettingsEntityExists()
        {
            using var context = GameStashDbContext.CreateDbContext();
            if (!context.Settings.Any())
            {
                context.Settings.Add(new Settings());
                context.SaveChanges();
            }
        }

        public async Task InitializeAsync()
        {
            Log.ForContext<SettingsService>().Information("Initializing Settings from PostgreSQL...");

            await using var context = GameStashDbContext.CreateDbContext();

            try
            {
                await context.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to apply migrations to PostgreSQL database. Please check your connection string and database configuration.\n" + ex.Message);
            }

            Log.ForContext<SettingsService>().Information("Settings initialization from PostgreSQL completed.");
        }

        public async Task<Settings> GetCurrentSettings()
        {
            await using var context = GameStashDbContext.CreateDbContext();
            var settings = await context.Settings.AsNoTracking().FirstOrDefaultAsync();

            return settings ?? new Settings();
        }

        public async Task<IgdbClient> GetIgdbClient()
        {
            if (string.IsNullOrEmpty(IgdbClientId) || string.IsNullOrEmpty(IgdbSecret))
                throw new InvalidOperationException("IGDB credentials are not configured.");

            var igdbClient = new IgdbClient(IgdbClientId, IgdbSecret);
            await igdbClient.GetAuthenticationStatusAsync();
            return igdbClient;
        }

        public SteamClient GetSteamClient()
        {
            if (string.IsNullOrEmpty(SteamId64) || string.IsNullOrEmpty(SteamApiKey))
                throw new InvalidOperationException("Steam credentials are not configured.");

            return new SteamClient(SteamId64, SteamApiKey);
        }

        public EpicClient GetEpicClient()
        {
            if (string.IsNullOrEmpty(EpicRefreshToken))
                throw new InvalidOperationException("Epic Games credentials are not configured.");

            return new EpicClient(EpicRefreshToken, EpicAccessToken);
        }

        public GogClient GetGogClient()
        {
            if (string.IsNullOrEmpty(GogRefreshToken))
                throw new InvalidOperationException("GOG credentials are not configured.");

            return new GogClient(GogRefreshToken, GogAccessToken);
        }

        public AmazonClient GetAmazonClient()
        {
            if (string.IsNullOrEmpty(AmazonRefreshToken))
                throw new InvalidOperationException("Amazon credentials are not configured.");

            return new AmazonClient(
                AmazonRefreshToken,
                AmazonAccessToken,
                null,
                async (newRefresh, newAccess) => await UpdateCredentialsAsync("amazon", newRefresh, newAccess)
            );
        }

        public string GenerateAmazonLoginUrl()
        {
            var (loginUrl, codeVerifier, deviceSerial) = AmazonClient.GenerateLoginUrl();
            _pendingAmazonPkce = (codeVerifier, deviceSerial);
            return loginUrl;
        }

        public async Task UpdateCredentialsAsync(string apiName, string userClientId, string secret)
        {
            Log.ForContext<SettingsService>().Information("Updating {ApiName} credentials...", apiName);

            switch (apiName.ToLowerInvariant())
            {
                case "igdb":
                    if (string.IsNullOrWhiteSpace(userClientId) || string.IsNullOrWhiteSpace(secret))
                        throw new ArgumentException("Both Client ID and Secret are required for IGDB.");

                    IgdbClientId = userClientId.Trim();
                    IgdbSecret = secret.Trim();
                    break;
                case "steam":
                    if (string.IsNullOrWhiteSpace(userClientId) || string.IsNullOrWhiteSpace(secret))
                        throw new ArgumentException("Both SteamID64 and Steam Web API Key are required.");

                    SteamId64 = userClientId.Trim();
                    SteamApiKey = secret.Trim();
                    break;
                case "epic":
                    if (string.IsNullOrWhiteSpace(secret))
                        throw new ArgumentException("Epic Games authorization code or access token is required.");

                    if (EpicRefreshToken == userClientId && EpicAccessToken == secret)
                    {
                        Log.ForContext<SettingsService>().Information("Epic Games credentials unchanged. No update required.");
                        return;
                    }

                    string epicRefreshToken;
                    string epicAccessToken;

                    // Cenário A: Renovação automática de tokens em segundo plano (userClientId é refreshToken, secret é accessToken)
                    if (!string.IsNullOrWhiteSpace(userClientId) &&
                        !string.Equals(userClientId, "epic", StringComparison.OrdinalIgnoreCase) &&
                        !secret.Contains("authorizationCode"))
                    {
                        epicRefreshToken = userClientId.Trim();
                        epicAccessToken = secret.Trim();
                    }
                    // Cenário B: Autenticação inicial vinda da UI (secret é o código de autorização ou JSON colado)
                    else
                    {
                        var epicClient = new EpicClient();
                        var tokenData = await epicClient.ExchangeCodeForTokenAsync(secret);

                        if (tokenData == null || string.IsNullOrWhiteSpace(tokenData.RefreshToken))
                            throw new InvalidOperationException("Invalid or expired Epic Games authorization code. Please log in again.");

                        epicRefreshToken = tokenData.RefreshToken;
                        epicAccessToken = tokenData.AccessToken;
                    }

                    EpicRefreshToken = epicRefreshToken;
                    EpicAccessToken = epicAccessToken;
                    break;
                case "gog":
                    if (string.IsNullOrWhiteSpace(secret))
                        throw new ArgumentException("GOG authorization code or access token is required.");

                    if(GogRefreshToken == userClientId && GogAccessToken == secret)
                    {
                        Log.ForContext<SettingsService>().Information("GOG credentials unchanged. No update required.");
                        return;
                    }

                    string refreshToken;
                    string accessToken;

                    // Scenario A: Internal token renewal (userClientId is refreshToken, secret is accessToken)
                    if (!string.IsNullOrWhiteSpace(userClientId) &&
                        !string.Equals(userClientId, "gog", StringComparison.OrdinalIgnoreCase) &&
                        !secret.Contains("code="))
                    {
                        refreshToken = userClientId.Trim();
                        accessToken = secret.Trim();
                    }
                    // Scenario B: Initial setup from UI (secret is the one-time authorization code or redirect URL)
                    else
                    {
                        var gogClient = new GogClient();
                        var tokenData = await gogClient.ExchangeCodeForTokenAsync(secret);

                        if (tokenData == null || string.IsNullOrWhiteSpace(tokenData.RefreshToken))
                            throw new InvalidOperationException("Invalid or expired GOG authorization code. Please log in again.");

                        refreshToken = tokenData.RefreshToken;
                        accessToken = tokenData.AccessToken;
                    }

                    GogRefreshToken = refreshToken;
                    GogAccessToken = accessToken;
                    break;
                case "amazon":
                    if (string.IsNullOrWhiteSpace(secret))
                        throw new ArgumentException("Amazon Games authorization code or access token is required.");

                    if (AmazonRefreshToken == userClientId && AmazonAccessToken == secret)
                    {
                        Log.ForContext<SettingsService>().Information("Amazon Games credentials unchanged. No update required.");
                        return;
                    }

                    string amazonRefreshToken;
                    string amazonAccessToken;

                    // Scenario A: Internal token renewal via callback (userClientId is refreshToken, secret is accessToken)
                    if (!string.IsNullOrWhiteSpace(userClientId) &&
                        !string.Equals(userClientId, "amazon", StringComparison.OrdinalIgnoreCase) &&
                        !secret.Contains("authorization_code="))
                    {
                        amazonRefreshToken = userClientId.Trim();
                        amazonAccessToken = secret.Trim();
                    }
                    // Scenario B: Initial login from UI (secret contains the maplanding URL or authorization code)
                    else
                    {
                        if (_pendingAmazonPkce == null)
                            throw new InvalidOperationException("Please click '1. Open Amazon Login' first to initialize a login session.");

                        var (codeVerifier, deviceSerial) = _pendingAmazonPkce.Value;
                        string? exchangedRefresh = null;
                        string? exchangedAccess = null;

                        var tempAmazonClient = new AmazonClient(
                            onTokensRefreshed: (newRefresh, newAccess) =>
                            {
                                exchangedRefresh = newRefresh;
                                exchangedAccess = newAccess;
                                return Task.CompletedTask;
                            });

                        bool exchanged = await tempAmazonClient.ExchangeCodeForTokenAsync(secret, codeVerifier, deviceSerial);
                        if (!exchanged || string.IsNullOrWhiteSpace(exchangedRefresh) || string.IsNullOrWhiteSpace(exchangedAccess))
                            throw new InvalidOperationException("Failed to exchange Amazon authorization code. Please log in again.");

                        amazonRefreshToken = exchangedRefresh;
                        amazonAccessToken = exchangedAccess;
                        _pendingAmazonPkce = null;
                    }

                    AmazonRefreshToken = amazonRefreshToken;
                    AmazonAccessToken = amazonAccessToken;
                    break;
                default:
                    throw new ArgumentException($"Unsupported credentials provider: '{apiName}'.", nameof(apiName));
            }

            Log.ForContext<SettingsService>().Information("Successfully updated {ApiName} credentials.", apiName);
        }
    }
}