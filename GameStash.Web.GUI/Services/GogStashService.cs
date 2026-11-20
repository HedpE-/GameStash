using GameStash.Core.Data;
using GameStash.Core.Models;
using GameStash.Core.Models.Gog;
using GameStash.Core.Services;
using GameStash.Web.GUI.Interfaces;
using IgdbSharp;
using IgdbSharp.Enumerators;
using IgdbSharp.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Services
{
    public class GogStashService(ISettingsService settingsService) : StashServiceBase<GogStashGame>, IGogStashService
    {
        private readonly ISettingsService _settingsService = settingsService;
        private static readonly HashSet<long> _manuallyInstalledOverrides = new();

        protected override async Task ExecuteInitializeAsync()
        {
            Log.ForContext<GogStashService>().Information("Starting GOG library synchronization...");

            try
            {
                var gogClient = _settingsService.GetGogClient();
                if (gogClient == null)
                {
                    Log.ForContext<GogStashService>().Warning("GOG credentials not configured.");
                    return;
                }

                // STEP 1: Fetch owned games from GOG API and check Windows Registry for local installs
                var rawGogGames = await gogClient.FetchOwnedGamesAsync();
                if (rawGogGames == null || !rawGogGames.Any())
                {
                    Log.ForContext<GogStashService>().Information("No games returned from GOG API.");
                    return;
                }

                var locallyInstalledIds = gogClient.GetLocallyInstalledGameIds();
                var currentGogIds = rawGogGames.Select(g => g.Id).ToHashSet();

                // STEP 2: Load current stash from PostgreSQL
                await using var context = GameStashDbContext.CreateDbContext();
                var dbGames = await context.GogStash.ToListAsync();

                var existingLookup = dbGames
                    .Where(g => g.ExtId.HasValue)
                    .GroupBy(g => g.ExtId!.Value)
                    .ToDictionary(g => g.Key, g => g.First());

                var existingById = dbGames
                    .Where(g => g.Id.HasValue)
                    .ToDictionary(g => g.Id!.Value);

                // A. Remove games no longer owned on GOG
                var gamesToRemove = dbGames
                    .Where(g => g.ExtId.HasValue && !currentGogIds.Contains(g.ExtId.Value))
                    .ToList();

                // B. Resolve IGDB IDs for new games or games still holding a negative synthetic ID
                var gamesNeedingResolution = rawGogGames
                    .Where(g => !existingLookup.TryGetValue(g.Id, out var existing) || (existing.Id ?? 0) <= 0)
                    .ToList();

                var igdbClient = await _settingsService.GetIgdbClient();
                var gogToIgdbMap = new Dictionary<long, long>();

                if (gamesNeedingResolution.Any() && igdbClient != null)
                {
                    gogToIgdbMap = await ResolveGogIdsToIgdbAsync(gamesNeedingResolution, igdbClient);
                }

                var gamesToInsert = new List<GogStashGame>();
                var gamesToUpdate = new List<GogStashGame>();

                // Map GOG CDN cover URLs (GOG returns protocol-relative URLs like "//://gog-statics.com...")
                var gogCoverMap = rawGogGames
                    .Where(g => !string.IsNullOrWhiteSpace(g.Image))
                    .GroupBy(g => g.Id)
                    .ToDictionary(g => g.Key, g => FormatGogCoverUrl(g.First().Image));

                var playtimeMap = await gogClient.FetchPlaytimeAsync();

                foreach (var gogGame in rawGogGames)
                {
                    bool detectedLocally = locallyInstalledIds.Contains(gogGame.Id) ||
                                           _manuallyInstalledOverrides.Contains(gogGame.Id);

                    gogToIgdbMap.TryGetValue(gogGame.Id, out long resolvedIgdbId);

                    playtimeMap.TryGetValue(gogGame.Id, out int playtimeMinutes);

                    if (existingLookup.TryGetValue(gogGame.Id, out var existing))
                    {
                        bool finalIsInstalled = existing.IsInstalled || detectedLocally;

                        // Upgrade negative synthetic ID to real IGDB primary key if resolved now
                        if ((existing.Id ?? 0) <= 0 && resolvedIgdbId > 0)
                        {
                            context.GogStash.Remove(existing);

                            var upgradedGame = new GogStashGame
                            {
                                Id = resolvedIgdbId,
                                ExtId = gogGame.Id,
                                Name = gogGame.Title,
                                SortName = existing.SortName,
                                PlaytimeMinutes = playtimeMinutes,
                                IsInstalled = finalIsInstalled,
                                IsFavorite = existing.IsFavorite,
                                IsPlayed = existing.IsPlayed,
                                IsPlaying = existing.IsPlaying,
                                IsFinished = existing.IsFinished,
                                PlayedToEnd = existing.PlayedToEnd,
                                IsInPlaylist = existing.IsInPlaylist
                            };

                            if (!gamesToInsert.Any(x => x.Id == upgradedGame.Id) && !existingById.ContainsKey(upgradedGame.Id!.Value))
                            {
                                gamesToInsert.Add(upgradedGame);
                            }
                        }
                        else
                        {
                            existing.Name = gogGame.Title;
                            existing.PlaytimeMinutes = playtimeMinutes;
                            existing.IsInstalled = finalIsInstalled;

                            if (!gamesToUpdate.Contains(existing))
                                gamesToUpdate.Add(existing);
                        }
                    }
                    else
                    {
                        long targetId = resolvedIgdbId > 0 ? resolvedIgdbId : -Math.Abs(gogGame.Id);

                        // Prevent duplicate primary key insertion if account owns multiple editions of the same IGDB game
                        if (gamesToInsert.Any(x => x.Id == targetId) || existingById.ContainsKey(targetId))
                            continue;

                        var newGame = new GogStashGame
                        {
                            Id = targetId,
                            ExtId = gogGame.Id,
                            Name = gogGame.Title,
                            PlaytimeMinutes = playtimeMinutes,
                            IsInstalled = detectedLocally
                        };

                        gamesToInsert.Add(newGame);
                    }
                }

                if (gamesToRemove.Count > 0)
                    context.GogStash.RemoveRange(gamesToRemove);

                if (gamesToInsert.Count > 0)
                    await context.GogStash.AddRangeAsync(gamesToInsert);

                if (gamesToUpdate.Count > 0)
                    context.GogStash.UpdateRange(gamesToUpdate);

                await context.SaveChangesAsync();

                var activeGames = gamesToUpdate.Concat(gamesToInsert).ToList();

                // STEP 3: Fetch full IGDB metadata in bulk for matched games
                var igdbIds = activeGames
                    .Where(g => g.Id.HasValue && g.Id.Value > 0)
                    .Select(g => g.Id!.Value)
                    .Distinct()
                    .ToList();

                if (igdbIds.Any() && igdbClient != null)
                {
                    var igdbLookup = await FetchIgdbGamesBatchAsync(igdbIds, igdbClient);

                    foreach (var game in activeGames)
                    {
                        if (game.Id.HasValue && igdbLookup.TryGetValue(game.Id.Value, out var igdbGame))
                        {
                            game.MatchIgdbGame(igdbGame);
                        }

                        if (game.ExtId.HasValue && gogCoverMap.TryGetValue(game.ExtId.Value, out var fallbackUrl))
                        {
                            game.FallbackCoverUrl = fallbackUrl;
                        }
                    }
                }

                // STEP 4: Update in-memory cache
                lock (SyncLock)
                {
                    CachedGames = activeGames.OrderBy(g => g.Name).ToList();
                }

                Log.ForContext<GogStashService>().Information(
                    "GOG library successfully synchronized. Total: {Total} ({Added} added, {Updated} updated, {Removed} removed).",
                    activeGames.Count, gamesToInsert.Count, gamesToUpdate.Count, gamesToRemove.Count);
            }
            catch (Exception ex)
            {
                Log.ForContext<GogStashService>().Error(ex, "Failed to execute GOG library synchronization.");
                throw;
            }
        }

        protected override bool IsMatchingId(GogStashGame game, long id)
        {
            return game.Id == id || game.ExtId == id;
        }

        private async Task<Dictionary<long, long>> ResolveGogIdsToIgdbAsync(IEnumerable<GogProduct> gogProducts, IgdbClient igdbClient)
        {
            var gogToIgdbMap = new ConcurrentDictionary<long, long>();
            var productsList = gogProducts.ToList();

            // 1. Primary lookup via IGDB external_games (external_game_source = 5 is GOG)
            var chunkTasks = productsList.Select(p => p.Id).Distinct().Chunk(100).Select(async chunk =>
            {
                var quotedUids = string.Join(",", chunk.Select(id => $"\"{id}\""));
                var query = $"fields game, uid; where external_game_source = 5 & uid = ({quotedUids}); limit 500;";
                try
                {
                    var extResults = await igdbClient.QueryAsync<ExternalGame>(
                    Endpoints.ExternalGames,
                    query: query,
                    limitResults: 500,
                    requestPriority: RequestPriority.Low
                    );
                    if (extResults == null) return;
                    foreach (var ext in extResults)
                    {
                        if (long.TryParse(ext.Uid, out long gogId))
                        {
                            long? igdbId = ext.Game?.Id ?? ext.Game?.Value?.Id;
                            if (igdbId.HasValue && igdbId.Value > 0)
                            {
                                gogToIgdbMap[gogId] = igdbId.Value;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.ForContext<GogStashService>().Warning(ex, "Failed to resolve IGDB external_games chunk for GOG.");
                }
            });
            await Task.WhenAll(chunkTasks);
            // 2. Batch case-insensitive title fallback on /games for remaining unlinked GOG titles
            var unmatched = productsList
            .Where(p => !gogToIgdbMap.ContainsKey(p.Id) && !string.IsNullOrWhiteSpace(p.Title))
            .ToList();
            if (unmatched.Any())
            {
                var titleGroups = unmatched
                .GroupBy(p => CleanGogTitle(p.Title), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
                var batchTasks = titleGroups.Keys.Chunk(25).Select(async nameChunk =>
                {
                    var nameFilters = string.Join(" | ", nameChunk.Select(n => $"name ~ \"{n.Replace("\"", "\\\"")}\""));

                    var query = $"fields id, name, version_parent; where ({nameFilters}); limit 100;";
                    try
                    {
                        var results = await igdbClient.QueryAsync<Game>(
                        Endpoints.Games,
                        query: query,
                        limitResults: 100,
                        requestPriority: RequestPriority.Low
                        );
                        if (results == null) return;
                        foreach (var game in results)
                        {
                            if (string.IsNullOrWhiteSpace(game.Name) || !game.Id.HasValue || game.Id.Value <= 0) continue;
                            long targetIgdbId = game.VersionParent?.Id ?? game.VersionParent?.Value?.Id ?? game.Id.Value;
                            if (titleGroups.TryGetValue(game.Name, out var matchedProducts))
                            {
                                foreach (var product in matchedProducts)
                                {
                                    gogToIgdbMap.TryAdd(product.Id, targetIgdbId);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext<GogStashService>().Warning(ex, "Failed batch title match on IGDB for GOG.");
                    }
                });
                await Task.WhenAll(batchTasks);
            }
            return new Dictionary<long, long>(gogToIgdbMap);
        }
        private async Task<Dictionary<long, Game>> FetchIgdbGamesBatchAsync(List<long> igdbIds, IgdbClient igdbClient)
        {
            var lookup = new Dictionary<long, Game>();
            foreach (var chunk in igdbIds.Chunk(100))
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
        private static string CleanGogTitle(string title)
        {
            var cleaned = title.Replace("®", "").Replace("™", "").Trim();
            cleaned = Regex.Replace(
            cleaned,
            @"\s*[-:]?\s*(Standard Edition|Game of the Year Edition|GOTY Edition|Deluxe Edition|Gold Edition|Complete Edition|Enhanced Edition|Definitive Edition)$",
            "",
            RegexOptions.IgnoreCase);
            return cleaned.Trim();
        }
        private static string FormatGogCoverUrl(string rawImage)
        {
            var url = rawImage.StartsWith("//") ? $"https:{rawImage}" : rawImage;
            // GOG image paths often omit extension or use _product_tile_256; append _glx_vertical_cover.jpg if no extension exists
            if (!url.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) &&
            !url.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
            !url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            {
                url += "_glx_vertical_cover.jpg";
            }
            return url;
        }
    }
}