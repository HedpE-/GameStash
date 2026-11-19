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
        private string IgdbClientId { get; set; } = string.Empty;
        private string IgdbSecret { get; set; } = string.Empty;

        // Steam Credentials
        private string SteamId64 { get; set; } = string.Empty;
        private string SteamApiKey { get; set; } = string.Empty;

        // Epic Games Credentials & Tokens
        private string EpicRefreshToken { get; set; } = string.Empty;
        private string EpicAccessToken { get; set; } = string.Empty;

        // GOG Credentials & Tokens
        private string GogRefreshToken { get; set; } = string.Empty;
        private string GogAccessToken { get; set; } = string.Empty;
        public string GogUsername { get; private set; } = string.Empty;

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

            // 1. Load all stored credentials from PostgreSQL 'settings' table
            var settings = await context.Settings.FirstOrDefaultAsync();
            if (settings != null)
            {
                IgdbClientId = settings.IGDB_Client_ID ?? string.Empty;
                IgdbSecret = settings.IGDB_Secret ?? string.Empty;
                SteamId64 = settings.SteamId64 ?? string.Empty;
                SteamApiKey = settings.SteamApiKey ?? string.Empty;
                EpicRefreshToken = settings.EpicRefreshToken ?? string.Empty;
                EpicAccessToken = settings.EpicAccessToken ?? string.Empty;
                GogRefreshToken = settings.GogRefreshToken ?? string.Empty;
                GogAccessToken = settings.GogAccessToken ?? string.Empty;
            }

            Log.ForContext<SettingsService>().Information("Settings initialization from PostgreSQL completed.");
        }

        public Settings GetCurrentSettings()
        {
            return new Settings
            {
                IGDB_Client_ID = IgdbClientId,
                IGDB_Secret = IgdbSecret,
                SteamId64 = SteamId64,
                SteamApiKey = SteamApiKey,
                EpicRefreshToken = EpicRefreshToken,
                EpicAccessToken = EpicAccessToken,
                GogRefreshToken = GogRefreshToken,
                GogAccessToken = GogAccessToken
            };
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

        public async Task UpdateCredentialsAsync(string apiName, string userClientId, string secret)
        {
            Log.ForContext<SettingsService>().Information("Updating {ApiName} credentials...", apiName);

            await using var context = GameStashDbContext.CreateDbContext();
            var settings = await context.Settings.FirstOrDefaultAsync();

            if (settings == null)
            {
                settings = new Settings();
                await context.Settings.AddAsync(settings);
            }

            switch (apiName.ToLowerInvariant())
            {
                case "igdb":
                    if (string.IsNullOrWhiteSpace(userClientId) || string.IsNullOrWhiteSpace(secret))
                        throw new ArgumentException("Both Client ID and Secret are required for IGDB.");

                    settings.IGDB_Client_ID = userClientId.Trim();
                    settings.IGDB_Secret = secret.Trim();

                    IgdbClientId = settings.IGDB_Client_ID;
                    IgdbSecret = settings.IGDB_Secret;
                    break;

                case "steam":
                    if (string.IsNullOrWhiteSpace(userClientId) || string.IsNullOrWhiteSpace(secret))
                        throw new ArgumentException("Both SteamID64 and Steam Web API Key are required.");

                    settings.SteamId64 = userClientId.Trim();
                    settings.SteamApiKey = secret.Trim();

                    SteamId64 = settings.SteamId64;
                    SteamApiKey = settings.SteamApiKey;
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

                    settings.EpicRefreshToken = epicRefreshToken;
                    settings.EpicAccessToken = epicAccessToken;

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

                    settings.GogRefreshToken = refreshToken;
                    settings.GogAccessToken = accessToken;

                    GogRefreshToken = refreshToken;
                    GogAccessToken = accessToken;
                    break;
                default:
                    throw new ArgumentException($"Unsupported credentials provider: '{apiName}'.", nameof(apiName));
            }

            await context.SaveChangesAsync();
            Log.ForContext<SettingsService>().Information("Successfully updated {ApiName} credentials.", apiName);
        }
    }
}