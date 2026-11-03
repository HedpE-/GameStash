using GameStash.Core.Data;
using GameStash.Core.Models;
using GameStash.Core.Services;
using GameStash.Web.GUI.Helpers;
using GameStash.Web.GUI.Interfaces;
using IgdbSharp;
using IgdbSharp.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Services
{
    public class SettingsService : ISettingsService
    {
        private IgdbClient? _igdbClient;

        public string IgdbClientId { get; private set; } = string.Empty;
        public string IgdbSecret { get; private set; } = string.Empty;

        public async Task InitializeAsync()
        {
            Log.ForContext<SettingsService>().Information("Initializing Settings from PostgreSQL...");

            await using var context = GameStashDbContext.CreateDbContext();

            await context.Database.MigrateAsync();

            // 1. Load IGDB credentials from PostgreSQL 'settings' table
            var settings = await context.Settings.FirstOrDefaultAsync();
            if (settings != null)
            {
                IgdbClientId = settings.IGDB_Client_ID ?? string.Empty;
                IgdbSecret = settings.IGDB_Secret ?? string.Empty;
            }

            Log.ForContext<SettingsService>().Information(
                "Settings initialization from PostgreSQL completed."
            );
        }

        public IgdbClient GetIgdbClient()
        {
            if (_igdbClient == null)
            {
                if (string.IsNullOrEmpty(IgdbClientId) || string.IsNullOrEmpty(IgdbSecret))
                    throw new InvalidOperationException("IGDB credentials are not configured.");

                _igdbClient = new IgdbClient(IgdbClientId, IgdbSecret);
            }
            return _igdbClient;
        }

        public async Task UpdateIgdbCredentialsAsync(string clientId, string secret)
        {
            Log.ForContext<SettingsService>().Information("Updating credentials to PostgreSQL...");

            await using var context = GameStashDbContext.CreateDbContext();

            context.Settings.FirstOrDefault().IGDB_Client_ID = clientId;
            context.Settings.FirstOrDefault().IGDB_Secret = secret;

            await context.SaveChangesAsync();

            Log.ForContext<SettingsService>().Information("Updated credentials to PostgreSQL successfully.");
        }
    }
}