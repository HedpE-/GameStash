using GameStash.Core.Data;
using GameStash.Core.Models;
using GameStash.Core.Models.Steam;
using GameStash.Core.Services;
using GameStash.Web.GUI.Interfaces;
using IgdbSharp;
using IgdbSharp.Enumerators;
using IgdbSharp.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Services
{
    public class SteamStashService(ISettingsService settingsService) : StashServiceBase<SteamStashGame>, ISteamStashService
    {
        private readonly ISettingsService _settingsService = settingsService;

        // In-memory cache for fast UI retrieval
        //private static List<SteamStashGame>? _cachedSteamGames = null;
        private static readonly HashSet<long> _manuallyInstalledOverrides = new();

        protected override async Task ExecuteInitializeAsync()
        {
            Log.ForContext<SteamStashService>().Information("Starting streamlined Steam library synchronization...");

            try
            {
                var steamClient = _settingsService.GetSteamClient();
                if (steamClient == null)
                {
                    Log.ForContext<SteamStashService>().Warning("Steam credentials not configured.");
                    return;
                }

                // STEP 1: Extract Steam library from API and local installation state
                var rawSteamGames = await steamClient.FetchOwnedGamesAsync();
                if (rawSteamGames == null || !rawSteamGames.Any())
                {
                    Log.ForContext<SteamStashService>().Information("No games returned from Steam Web API.");
                    return;
                }

                // Filter out deprecated/redirected duplicate AppIDs in a single batch call (e.g. 34440 -> 3900, 34460 -> 8800)
                var duplicateNameCandidates = rawSteamGames
                    .GroupBy(g => NormalizeSteamTitle(g.Name), StringComparer.OrdinalIgnoreCase)
                    .Where(grp => grp.Count() > 1)
                    .SelectMany(grp => grp)
                    .Select(g => g.AppId)
                    .ToList();

                if (duplicateNameCandidates.Any())
                {
                    var invalidAppIds = await steamClient.GetNonCanonicalStoreAppIdsAsync(duplicateNameCandidates);
                    if (invalidAppIds.Count > 0)
                    {
                        rawSteamGames = rawSteamGames
                            .Where(g => !invalidAppIds.Contains(g.AppId))
                            .ToList();

                        Log.ForContext<SteamStashService>().Information(
                            "Discarded {Count} deprecated/redirected Steam AppID(s): {Ids}",
                            invalidAppIds.Count, string.Join(", ", invalidAppIds));
                    }
                }

                var locallyInstalledIds = steamClient.GetLocallyInstalledAppIds();
                var currentSteamAppIds = rawSteamGames.Select(g => g.AppId).ToHashSet();

                // STEP 2: Extract current stash from PostgreSQL
                await using var context = GameStashDbContext.CreateDbContext();
                var dbGames = await context.SteamStash.ToListAsync();

                var existingLookup = dbGames
                    .Where(g => g.ExtId.HasValue)
                    .ToDictionary(g => g.ExtId!.Value);

                // A. Remove games from DB that are no longer owned on Steam
                var gamesToRemove = dbGames
                    .Where(g => g.ExtId.HasValue && !currentSteamAppIds.Contains(g.ExtId.Value))
                    .ToList();

                // B. Detect new games that need IGDB ID resolution before inserting
                var newSteamGames = rawSteamGames
                    .Where(g => !existingLookup.ContainsKey(g.AppId))
                    .ToList();

                var newAppToIgdbMap = new Dictionary<long, long>();
                if (newSteamGames.Any())
                {
                    var igdbClient = await _settingsService.GetIgdbClient();
                    newAppToIgdbMap = await ResolveSteamAppIdsToIgdbAsync(newSteamGames.Select(g => g.AppId), igdbClient);
                }

                var gamesToInsert = new List<SteamStashGame>();
                var gamesToUpdate = new List<SteamStashGame>();

                foreach (var steamGame in rawSteamGames)
                {
                    bool isInstalled = locallyInstalledIds.Contains(steamGame.AppId) ||
                                       _manuallyInstalledOverrides.Contains(steamGame.AppId);

                    if (existingLookup.TryGetValue(steamGame.AppId, out var existing))
                    {
                        // Update mutable Steam metrics (preserves DB flags like IsFavorite, IsInPlaylist, SortName)
                        existing.Name = steamGame.Name;
                        existing.PlaytimeMinutes = steamGame.PlaytimeForeverMinutes;
                        existing.IsInstalled = isInstalled;

                        gamesToUpdate.Add(existing);
                    }
                    else
                    {
                        // Create clean SteamStashGame entity for insertion
                        newAppToIgdbMap.TryGetValue(steamGame.AppId, out long resolvedIgdbId);

                        var newGame = new SteamStashGame
                        {
                            Id = resolvedIgdbId > 0 ? resolvedIgdbId : -steamGame.AppId, // Synthetic negative key if unindexed
                            ExtId = steamGame.AppId,
                            Name = steamGame.Name,
                            PlaytimeMinutes = steamGame.PlaytimeForeverMinutes,
                            IsInstalled = isInstalled
                        };

                        gamesToInsert.Add(newGame);
                    }
                }

                // Apply DB changes
                if (gamesToRemove.Count > 0)
                    context.SteamStash.RemoveRange(gamesToRemove);

                if (gamesToInsert.Count > 0)
                    await context.SteamStash.AddRangeAsync(gamesToInsert);

                if (gamesToUpdate.Count > 0)
                    context.SteamStash.UpdateRange(gamesToUpdate);

                await context.SaveChangesAsync();

                // Active list of all synchronized games
                var activeGames = gamesToUpdate.Concat(gamesToInsert).ToList();

                // STEP 3: Query IGDB in bulk and execute MatchIgdbGame on each in-memory entry
                var igdbIds = activeGames
                    .Where(g => g.Id.HasValue && g.Id.Value > 0)
                    .Select(g => g.Id!.Value)
                    .Distinct()
                    .ToList();

                if (igdbIds.Any())
                {
                    var igdbClient = await _settingsService.GetIgdbClient();
                    var igdbLookup = await FetchIgdbGamesBatchAsync(igdbIds, igdbClient);

                    foreach (var game in activeGames)
                    {
                        if (game.Id.HasValue && igdbLookup.TryGetValue(game.Id.Value, out var igdbGame))
                        {
                            // Enriches in-memory game with Cover, Platforms, Rating, etc.
                            game.MatchIgdbGame(igdbGame);
                        }
                    }
                }

                // STEP 4: Save fully enriched in-memory list to cache
                CachedGames = activeGames.OrderBy(g => g.Name).ToList();

                Log.ForContext<SteamStashService>().Information(
                    "Steam library successfully synchronized. Total: {Total} ({Added} added, {Updated} updated, {Removed} removed).",
                    activeGames.Count, gamesToInsert.Count, gamesToUpdate.Count, gamesToRemove.Count);
            }
            catch (Exception ex)
            {
                Log.ForContext<SteamStashService>().Error(ex, "Failed to execute Steam library synchronization pipeline.");
                throw;
            }
        }

        protected override bool IsMatchingId(SteamStashGame game, long id)
        {
            return game.Id == id || game.ExtId == id;
        }

        private async Task<Dictionary<long, long>> ResolveSteamAppIdsToIgdbAsync(IEnumerable<long> appIds, IgdbClient igdbClient)
        {
            var appToIgdbMap = new Dictionary<long, long>();
            var chunks = appIds.Chunk(100).ToList();

            foreach (var chunk in chunks)
            {
                var quotedUids = string.Join(",", chunk.Select(id => $"\"{id}\""));
                var query = $"fields game, uid; where external_game_source = 1 & uid = ({quotedUids});";

                var extResults = await igdbClient.QueryAsync<ExternalGame>(
                    Endpoints.ExternalGames,
                    query: query,
                    limitResults: 500,
                    requestPriority: RequestPriority.Low
                );

                if (extResults != null)
                {
                    foreach (var ext in extResults)
                    {
                        if (long.TryParse(ext.Uid, out long appId))
                        {
                            long? igdbId = ext.Game?.Id ?? ext.Game?.Value?.Id;
                            if (igdbId.HasValue && igdbId.Value > 0)
                            {
                                appToIgdbMap[appId] = igdbId.Value;
                            }
                        }
                    }
                }
            }

            return appToIgdbMap;
        }

        private async Task<Dictionary<long, Game>> FetchIgdbGamesBatchAsync(List<long> igdbIds, IgdbClient igdbClient)
        {
            var lookup = new Dictionary<long, Game>();
            var chunks = igdbIds.Chunk(100).ToList();

            foreach (var chunk in chunks)
            {
                var idsString = string.Join(",", chunk);
                var query = $"fields {StashGame.StashGameCardQueryFields}, platforms.*; where id = ({idsString}); limit 500;";

                var games = await igdbClient.QueryAsync<Game>(
                    Endpoints.Games,
                    query: query,
                    limitResults: 500,
                    requestPriority: RequestPriority.Low
                );

                if (games != null)
                {
                    foreach (var g in games)
                    {
                        if (g.Id.HasValue)
                            lookup[g.Id.Value] = g;
                    }
                }
            }

            return lookup;
        }

        private static string NormalizeSteamTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return string.Empty;
            return new string([.. title
                .Replace("®", "")
                .Replace("™", "")
                .ToLowerInvariant()
                .Where(char.IsLetterOrDigit)]);
        }
    }
}