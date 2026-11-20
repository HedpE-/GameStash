using GameStash.Core.Data;
using GameStash.Core.Models;
using GameStash.Core.Models.Epic;
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
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Services
{
    public class EpicStashService(ISettingsService settingsService) : StashServiceBase<EpicStashGame>, IEpicStashService
    {
        private readonly ISettingsService _settingsService = settingsService;

        private static readonly HashSet<string> _excludedCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            "addons",
            "dlc",
            "digitalextras",
            "editors",
            "modkit",
            "soundtrack",
            "artbook",
            "wallpapers",
            "engines"
        };

        private static readonly HashSet<string> _manuallyInstalledOverrides = new(StringComparer.OrdinalIgnoreCase);
        private record IgdbEpicMatch(long IgdbId, string? EpicUid);

        protected override async Task ExecuteInitializeAsync()
        {
            Log.ForContext<EpicStashService>().Information("Starting Epic Games library synchronization...");

            try
            {
                var epicClient = _settingsService.GetEpicClient();
                if (epicClient == null)
                {
                    Log.ForContext<EpicStashService>().Warning("Epic Games credentials not configured.");
                    return;
                }

                // STEP 1: Fetch owned games from Epic API and check local .item manifests
                var allCatalogItems = await epicClient.FetchOwnedGamesAsync();
                if (allCatalogItems == null || !allCatalogItems.Any())
                {
                    Log.ForContext<EpicStashService>().Information("No games returned from Epic Games API.");
                    return;
                }

                var rawEpicGames = FilterValidGames(allCatalogItems);

                var locallyInstalledIds = epicClient.GetLocallyInstalledCatalogIds();

                // STEP 2: Load current stash from PostgreSQL
                await using var context = GameStashDbContext.CreateDbContext();
                var dbGames = await context.EpicStash.ToListAsync();

                // Lookups para encontrar jogos já gravados (por Uid do IGDB, por syntheticId ou por Name)
                var existingByUid = dbGames
                    .Where(g => !string.IsNullOrWhiteSpace(g.Uid))
                    .GroupBy(g => g.Uid!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var existingById = dbGames
                    .Where(g => g.Id.HasValue)
                    .ToDictionary(g => g.Id!.Value);

                var existingByName = dbGames
                    .Where(g => !string.IsNullOrWhiteSpace(g.Name))
                    .GroupBy(g => g.Name!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                // Resolver IGDB ID + Uid para todos os jogos que ainda não têm match válido na BD (ou todos os novos)
                var itemsToResolve = rawEpicGames
                    .Where(g => !string.IsNullOrWhiteSpace(g.Id))
                    .Where(g =>
                    {
                        long syntheticId = GetSyntheticId(g.Id);
                        // Se já existe na BD com ID positivo (> 0) e Uid preenchido, não precisa de ir ao IGDB
                        if (existingByName.TryGetValue(g.Title ?? string.Empty, out var byName) && byName.Id > 0 && !string.IsNullOrWhiteSpace(byName.Uid))
                            return false;
                        if (existingById.TryGetValue(syntheticId, out var bySynth) && bySynth.Id > 0)
                            return false;
                        return true;
                    })
                    .ToList();

                var igdbClient = await _settingsService.GetIgdbClient();
                var resolvedMap = new Dictionary<string, IgdbEpicMatch>(StringComparer.OrdinalIgnoreCase);

                if (itemsToResolve.Any() && igdbClient != null)
                {
                    resolvedMap = await ResolveEpicIdsToIgdbAsync(itemsToResolve, igdbClient);
                }

                var playtimeMap = await epicClient.FetchPlaytimeAsync();

                var gamesToInsert = new List<EpicStashGame>();
                var gamesToUpdate = new List<EpicStashGame>();
                var matchedDbIds = new HashSet<long>();

                foreach (var epicGame in rawEpicGames)
                {
                    if (string.IsNullOrWhiteSpace(epicGame.Id)) continue;

                    bool isInstalled = locallyInstalledIds.Contains(epicGame.Id) ||
                                       _manuallyInstalledOverrides.Contains(epicGame.Id);

                    // 2. Calculate playtime in minutes for this game using its AppName
                    int playtimeMinutes = (!string.IsNullOrWhiteSpace(epicGame.AppName) &&
                                           playtimeMap.TryGetValue(epicGame.AppName, out var mins))
                        ? mins
                        : 0;

                    long syntheticId = GetSyntheticId(epicGame.Id);
                    resolvedMap.TryGetValue(epicGame.Id, out var igdbMatch);

                    // Tenta localizar o registo existente na BD por ordem de fiabilidade
                    EpicStashGame? existing = null;
                    if (igdbMatch != null && !string.IsNullOrWhiteSpace(igdbMatch.EpicUid) && existingByUid.TryGetValue(igdbMatch.EpicUid, out var matchByUid))
                        existing = matchByUid;
                    else if (igdbMatch != null && existingById.TryGetValue(igdbMatch.IgdbId, out var matchByIgdbId))
                        existing = matchByIgdbId;
                    else if (existingById.TryGetValue(syntheticId, out var matchBySynth))
                        existing = matchBySynth;
                    else if (!string.IsNullOrWhiteSpace(epicGame.Title) && existingByName.TryGetValue(epicGame.Title, out var matchByName))
                        existing = matchByName;

                    if (existing != null)
                    {
                        bool finalIsInstalled = existing.IsInstalled || isInstalled;
                        // Se o jogo tinha um ID negativo (syntheticId) e agora conseguimos resolver o ID real do IGDB,
                        // temos de remover o registo antigo e inserir o novo porque 'Id' é Primary Key no EF Core.
                        if (existing.Id <= 0 && igdbMatch != null && igdbMatch.IgdbId > 0)
                        {
                            context.EpicStash.Remove(existing);

                            var upgradedGame = new EpicStashGame
                            {
                                Id = igdbMatch.IgdbId,
                                Uid = igdbMatch.EpicUid ?? existing.Uid,
                                CatalogItemId = epicGame.Id,
                                Namespace = epicGame.Namespace,
                                AppName = epicGame.AppName,
                                Name = epicGame.Title,
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
                                matchedDbIds.Add(upgradedGame.Id!.Value);
                            }
                        }
                        else
                        {
                            existing.Name = epicGame.Title;
                            existing.CatalogItemId = epicGame.Id;
                            existing.Namespace = epicGame.Namespace;
                            existing.AppName = epicGame.AppName;
                            existing.PlaytimeMinutes = playtimeMinutes;
                            existing.IsInstalled = finalIsInstalled;
                            if (!string.IsNullOrWhiteSpace(igdbMatch?.EpicUid))
                                existing.Uid = igdbMatch.EpicUid;

                            if (existing.Id.HasValue)
                                matchedDbIds.Add(existing.Id.Value);

                            if (!gamesToUpdate.Contains(existing))
                                gamesToUpdate.Add(existing);
                        }
                    }
                    else
                    {
                        long targetId = (igdbMatch != null && igdbMatch.IgdbId > 0) ? igdbMatch.IgdbId : syntheticId;

                        // Evita duplicados caso a conta Epic tenha 2 edições diferentes do mesmo jogo IGDB
                        if (gamesToInsert.Any(x => x.Id == targetId) || existingById.ContainsKey(targetId))
                            continue;

                        var newGame = new EpicStashGame
                        {
                            Id = targetId,
                            Uid = igdbMatch?.EpicUid,
                            CatalogItemId = epicGame.Id,
                            Namespace = epicGame.Namespace,
                            AppName = epicGame.AppName,
                            Name = epicGame.Title,
                            PlaytimeMinutes = playtimeMinutes,
                            IsInstalled = isInstalled
                        };

                        gamesToInsert.Add(newGame);
                        matchedDbIds.Add(targetId);
                    }
                }

                // Remove da BD jogos que já não vêm da conta Epic
                var gamesToRemove = dbGames
                    .Where(g => g.Id.HasValue && !matchedDbIds.Contains(g.Id.Value))
                    .ToList();

                if (gamesToRemove.Count > 0)
                    context.EpicStash.RemoveRange(gamesToRemove);

                if (gamesToInsert.Count > 0)
                    await context.EpicStash.AddRangeAsync(gamesToInsert);

                if (gamesToUpdate.Count > 0)
                    context.EpicStash.UpdateRange(gamesToUpdate);

                await context.SaveChangesAsync();

                // Map CatalogItemId -> Epic Store KeyImage URL
                var epicCoverMap = rawEpicGames
                    .Where(g => !string.IsNullOrWhiteSpace(g.Id))
                    .ToDictionary(g => g.Id, ExtractEpicTallCoverUrl, StringComparer.OrdinalIgnoreCase);

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

                        if (!string.IsNullOrWhiteSpace(game.CatalogItemId) &&  epicCoverMap.TryGetValue(game.CatalogItemId, out var epicCoverUrl))
                        {
                            game.FallbackCoverUrl = epicCoverUrl;
                        }
                    }
                }

                // STEP 4: Store in memory cache
                lock (SyncLock)
                {
                    CachedGames = activeGames.OrderBy(g => g.Name).ToList();
                }

                Log.ForContext<EpicStashService>().Information(
                    "Epic Games library synchronized. Total: {Total} ({Added} added, {Updated} updated, {Removed} removed).",
                    activeGames.Count, gamesToInsert.Count, gamesToUpdate.Count, gamesToRemove.Count);
            }
            catch (Exception ex)
            {
                Log.ForContext<EpicStashService>().Error(ex, "Failed to execute Epic Games library synchronization.");
                throw;
            }
        }

        private static long GetSyntheticId(string catalogItemId)
        {
            long syntheticId = -Math.Abs((long)catalogItemId.GetHashCode(StringComparison.OrdinalIgnoreCase));
            return syntheticId == 0 ? -1 : syntheticId;
        }

        protected override bool IsMatchingId(EpicStashGame game, long id)
        {
            return game.Id == id;
        }

        private async Task<Dictionary<string, IgdbEpicMatch>> ResolveEpicIdsToIgdbAsync(IEnumerable<EpicCatalogItem> epicItems, IgdbClient igdbClient)
        {
            var catalogToMatchMap = new ConcurrentDictionary<string, IgdbEpicMatch>(StringComparer.OrdinalIgnoreCase);
            var searchTokenToCatalogIds = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in epicItems)
            {
                if (string.IsNullOrWhiteSpace(item.Id)) continue;

                if (!string.IsNullOrWhiteSpace(item.Title))
                {
                    foreach (var candidateTitle in GetTitleCandidates(item.Title))
                    {
                        var slug = GenerateEpicSlug(candidateTitle);
                        if (!string.IsNullOrWhiteSpace(slug))
                        {
                            if (!searchTokenToCatalogIds.TryGetValue(slug, out var list))
                                searchTokenToCatalogIds[slug] = list = [];
                            if (!list.Contains(item.Id))
                                list.Add(item.Id);
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(item.Namespace) && item.Namespace != "ue")
                {
                    if (!searchTokenToCatalogIds.TryGetValue(item.Namespace, out var list))
                        searchTokenToCatalogIds[item.Namespace] = list = [];
                    if (!list.Contains(item.Id))
                        list.Add(item.Id);
                }
            }

            var allSearchTokens = searchTokenToCatalogIds.Keys.ToList();
            using var throttler = new System.Threading.SemaphoreSlim(4);

            // ---------------------------------------------------------
            // STEP 1: Match by Epic Store URL / Slug in external_games
            // ---------------------------------------------------------
            var chunkTasks = allSearchTokens.Chunk(35).Select(async chunk =>
            {
                await throttler.WaitAsync();
                try
                {
                    var urlFilters = string.Join(" | ", chunk.Select(token => $"url ~ *\"/{token.Replace("\"", "\\\"")}\"*"));
                    var query = $"fields game, uid, url; where external_game_source = 26 & ({urlFilters});";

                    var extResults = await igdbClient.QueryAsync<ExternalGame>(
                        Endpoints.ExternalGames,
                        query: query,
                        limitResults: 500,
                        requestPriority: RequestPriority.Low
                    );

                    if (extResults == null) return;

                    foreach (var ext in extResults)
                    {
                        long? igdbId = ext.Game?.Id ?? ext.Game?.Value?.Id;
                        if (!igdbId.HasValue || igdbId.Value <= 0) continue;

                        List<string>? matchedCatalogIds = null;

                        if (!string.IsNullOrWhiteSpace(ext.Uid) &&
                            searchTokenToCatalogIds.TryGetValue(ext.Uid, out var byUid))
                        {
                            matchedCatalogIds = byUid;
                        }
                        else if (!string.IsNullOrWhiteSpace(ext.Url))
                        {
                            var cleanUrl = ext.Url.TrimEnd('/').Split('?')[0];
                            var lastSegment = cleanUrl.Split('/').LastOrDefault();

                            if (!string.IsNullOrWhiteSpace(lastSegment))
                            {
                                if (searchTokenToCatalogIds.TryGetValue(lastSegment, out var exactMatch))
                                {
                                    matchedCatalogIds = exactMatch;
                                }
                                else
                                {
                                    var epicSuffixMatch = System.Text.RegularExpressions.Regex.Match(lastSegment, @"^(.+)-[0-9a-f]{6}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                    if (epicSuffixMatch.Success &&
                                        searchTokenToCatalogIds.TryGetValue(epicSuffixMatch.Groups[1].Value, out var baseSlugMatch))
                                    {
                                        matchedCatalogIds = baseSlugMatch;
                                    }
                                }
                            }
                        }

                        if (matchedCatalogIds != null)
                        {
                            foreach (var catId in matchedCatalogIds)
                            {
                                catalogToMatchMap.TryAdd(catId, new IgdbEpicMatch(igdbId.Value, ext.Uid));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.ForContext<EpicStashService>().Warning(ex, "Failed to resolve IGDB external_games chunk.");
                }
                finally
                {
                    throttler.Release();
                }
            });

            await Task.WhenAll(chunkTasks);

            // ---------------------------------------------------------
            // STEP 2: Batch case-insensitive title match on /games
            // ---------------------------------------------------------
            var unmatchedAfterUrl = epicItems
                .Where(i => !string.IsNullOrWhiteSpace(i.Id) && !catalogToMatchMap.ContainsKey(i.Id) && !string.IsNullOrWhiteSpace(i.Title))
                .ToList();

            if (unmatchedAfterUrl.Any())
            {
                var candidateToItems = new Dictionary<string, List<EpicCatalogItem>>(StringComparer.OrdinalIgnoreCase);

                foreach (var item in unmatchedAfterUrl)
                {
                    foreach (var candidate in GetTitleCandidates(item.Title))
                    {
                        if (!candidateToItems.TryGetValue(candidate, out var list))
                            candidateToItems[candidate] = list = [];
                        list.Add(item);
                    }
                }

                var batchNameTasks = candidateToItems.Keys.Chunk(25).Select(async nameChunk =>
                {
                    await throttler.WaitAsync();
                    try
                    {
                        var nameFilters = string.Join(" | ", nameChunk.Select(n => $"name ~ \"{n.Replace("\"", "\\\"")}\""));
                        // Includes version_parent to map special editions back to their base game ID
                        var query = $"fields id, name, version_parent; where ({nameFilters});";

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

                            if (candidateToItems.TryGetValue(game.Name, out var matchedItems))
                            {
                                foreach (var item in matchedItems)
                                {
                                    catalogToMatchMap.TryAdd(item.Id, new IgdbEpicMatch(targetIgdbId, null));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext<EpicStashService>().Warning(ex, "Failed batch case-insensitive title match on IGDB.");
                    }
                    finally
                    {
                        throttler.Release();
                    }
                });

                await Task.WhenAll(batchNameTasks);
            }

            // ---------------------------------------------------------
            // STEP 3: Fuzzy search fallback for remaining edge cases
            // ---------------------------------------------------------
            var stillUnmatched = epicItems
                .Where(i => !string.IsNullOrWhiteSpace(i.Id) && !catalogToMatchMap.ContainsKey(i.Id) && !string.IsNullOrWhiteSpace(i.Title))
                .ToList();

            if (stillUnmatched.Any())
            {
                var fuzzyTasks = stillUnmatched.Select(async item =>
                {
                    await throttler.WaitAsync();
                    try
                    {
                        var bestCandidate = GetTitleCandidates(item.Title).LastOrDefault() ?? item.Title;
                        var escaped = bestCandidate.Replace("\"", "\\\"");
                        var query = $"search \"{escaped}\"; fields id, name, version_parent;";

                        var results = await igdbClient.QueryAsync<Game>(
                            Endpoints.Games,
                            query: query,
                            limitResults: 3,
                            requestPriority: RequestPriority.Low
                        );

                        var match = results?.FirstOrDefault(g => g.Id.HasValue && g.Id.Value > 0);
                        if (match != null)
                        {
                            long targetIgdbId = match.VersionParent?.Id ?? match.VersionParent?.Value?.Id ?? match.Id!.Value;
                            catalogToMatchMap.TryAdd(item.Id, new IgdbEpicMatch(targetIgdbId, null));
                        }

                        await Task.Delay(250);
                    }
                    catch
                    {
                        // Ignore individual search errors
                    }
                    finally
                    {
                        throttler.Release();
                    }
                });

                await Task.WhenAll(fuzzyTasks);
            }

            return new Dictionary<string, IgdbEpicMatch>(catalogToMatchMap, StringComparer.OrdinalIgnoreCase);
        }


        private static string GenerateEpicSlug(string title)
        {
            var lower = title.Trim().ToLowerInvariant();
            // Remove caracteres especiais comuns em títulos (®, ™, :, ', etc.) e substitui espaços por hífen
            var cleaned = System.Text.RegularExpressions.Regex.Replace(lower, @"[^a-z0-9\s-]", "");
            var slug = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", "-").Trim('-');
            return slug;
        }

        private async Task<Dictionary<long, Game>> FetchIgdbGamesBatchAsync(List<long> igdbIds, IgdbClient igdbClient)
        {
            var lookup = new ConcurrentDictionary<long, Game>();

            var batchTasks = igdbIds.Chunk(100).Select(async chunk =>
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
            });

            await Task.WhenAll(batchTasks);

            return new Dictionary<long, Game>(lookup);
        }

        private static IEnumerable<string> GetTitleCandidates(string rawTitle)
        {
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Strip trademark symbols, normalize dashes and fix missing space after colons (e.g., "Warface:GO" -> "Warface: GO")
            var clean = rawTitle
                .Replace("®", "")
                .Replace("™", "")
                .Replace("–", "-")
                .Replace("—", "-")
                .Replace(" : ", ": ")
                .Trim();

            if (!string.IsNullOrWhiteSpace(clean))
            {
                if (clean.Equals("Telltale Batman Season 1", StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add("Batman: The Telltale Series");
                }
                candidates.Add(clean);
                // Also add version with space after colon if missing (e.g., "Warface:GO" -> "Warface: GO")
                var spacedColon = System.Text.RegularExpressions.Regex.Replace(clean, @":([^\s])", ": $1");
                candidates.Add(spacedColon);
            }

            // 2. Strip common edition and marketing suffixes
            var withoutEdition = System.Text.RegularExpressions.Regex.Replace(
                clean,
                @"(\s*[-:|]\s*|\s+)(Standard Edition|Game of the Year Edition|GOTY Edition|Deluxe Edition|Gold Edition|Complete Edition|Enhanced Edition|Definitive Edition|Ecto Edition|The Final Cut|Complete Story|20 Year Celebration|Starring Lara Croft|A Post Nuclear Role Playing Game|Premium|Farm Game)$",
                "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

            if (!string.IsNullOrWhiteSpace(withoutEdition))
                candidates.Add(withoutEdition);

            // 3. Extract base title before subtitle separators (e.g., "Fallout 2: A Post Nuclear..." -> "Fallout 2")
            var beforeSeparator = System.Text.RegularExpressions.Regex.Split(withoutEdition, @"\s*[:|]\s*|\s+-\s+")[0].Trim();
            if (beforeSeparator.Length >= 3)
                candidates.Add(beforeSeparator);

            return candidates;
        }

        private static string? ExtractEpicTallCoverUrl(EpicCatalogItem item)
        {
            if (item.KeyImages == null || item.KeyImages.Count == 0)
                return null;

            var preferredTypes = new[]
            {
                "DieselGameBoxTall",
                "OfferImageTall",
                "Thumbnail",
                "DieselGameBox",
                "OfferImageWide"
            };

            foreach (var type in preferredTypes)
            {
                var match = item.KeyImages.FirstOrDefault(k =>
                    string.Equals(k.Type, type, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(k.Url));

                if (match != null)
                    return match.Url;
            }

            return item.KeyImages.FirstOrDefault(k => !string.IsNullOrWhiteSpace(k.Url))?.Url;
        }

        private static List<EpicCatalogItem> FilterValidGames(IEnumerable<EpicCatalogItem> items)
        {
            return items
                .Where(item => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Title))
                .Where(item =>
                {
                    // 1. Platform check: Exclude mobile-only items (Android/iOS without Windows support)
                    if (item.ReleaseInfo != null && item.ReleaseInfo.Count > 0)
                    {
                        bool supportsWindows = item.ReleaseInfo.Any(r =>
                            r.Platform != null &&
                            r.Platform.Any(p => p.Equals("Windows", StringComparison.OrdinalIgnoreCase)));

                        if (!supportsWindows)
                            return false;
                    }

                    var categoryPaths = item.Categories?
                        .Select(c => c.Path)
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .ToList() ?? [];

                    // 2. Include base games, editions, and Fortnite experiences (e.g., LEGO Fortnite Odyssey)
                    bool isPlayableEntry = categoryPaths.Any(p =>
                        p.Equals("games", StringComparison.OrdinalIgnoreCase) ||
                        p.StartsWith("games/edition", StringComparison.OrdinalIgnoreCase) ||
                        p.Equals("experiences", StringComparison.OrdinalIgnoreCase) ||
                        p.Equals("applications", StringComparison.OrdinalIgnoreCase));

                    bool isExcludedCategory = categoryPaths.Any(p => _excludedCategories.Contains(p));

                    var title = item.Title.Trim();

                    // 3. Exclude non-game utilities, artbooks, wallpapers, and DLC/cosmetic packs
                    bool isExcludedTitle =
                        title.EndsWith(" Content", StringComparison.OrdinalIgnoreCase) ||
                        title.EndsWith(" Pre-Game Editor", StringComparison.OrdinalIgnoreCase) ||
                        title.EndsWith(" Resource Archiver", StringComparison.OrdinalIgnoreCase) ||
                        title.EndsWith(" Editor", StringComparison.OrdinalIgnoreCase) ||
                        title.EndsWith(" Soundtrack", StringComparison.OrdinalIgnoreCase) ||
                        title.EndsWith(" Artbook", StringComparison.OrdinalIgnoreCase) ||
                        title.EndsWith(" Art Book", StringComparison.OrdinalIgnoreCase) ||
                        title.EndsWith(" Wallpaper", StringComparison.OrdinalIgnoreCase) ||
                        title.EndsWith(" Mod Kit", StringComparison.OrdinalIgnoreCase) ||
                        title.EndsWith(" Dedicated Server", StringComparison.OrdinalIgnoreCase) ||
                        title.EndsWith(" Expansion Pack", StringComparison.OrdinalIgnoreCase) ||
                        title.EndsWith(" Texture Pack", StringComparison.OrdinalIgnoreCase) ||
                        title.EndsWith(" Puzzle Pack 1", StringComparison.OrdinalIgnoreCase) ||
                        title.EndsWith(" Puzzle Pack 2", StringComparison.OrdinalIgnoreCase) ||
                        title.EndsWith(" Skin", StringComparison.OrdinalIgnoreCase) ||
                        title.Equals("Festival Suite", StringComparison.OrdinalIgnoreCase);

                    bool isKnownDlc =
                        title.Equals("Dying Light The Following", StringComparison.OrdinalIgnoreCase) ||
                        title.Equals("Dying Light The Bozak", StringComparison.OrdinalIgnoreCase) ||
                        title.Equals("The Outer Worlds Peril on Gorgon", StringComparison.OrdinalIgnoreCase) ||
                        title.Equals("The Outer Worlds: Murder on Eridanos", StringComparison.OrdinalIgnoreCase);

                    return isPlayableEntry && !isExcludedCategory && !isExcludedTitle && !isKnownDlc;
                })
                .ToList();
        }

    }
}