using GameStash.Core.Data;
using GameStash.Core.Models;
using GameStash.Core.Models.Amazon;
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
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Services
{
    public class AmazonStashService(ISettingsService settingsService) : StashServiceBase<AmazonStashGame>, IAmazonStashService
    {
        private readonly ISettingsService _settingsService = settingsService;
        private static readonly HashSet<string> _manuallyInstalledOverrides = new(StringComparer.OrdinalIgnoreCase);

        protected override async Task ExecuteInitializeAsync()
        {
            Log.ForContext<AmazonStashService>().Information("Starting Amazon Games library synchronization...");

            try
            {
                var amazonClient = _settingsService.GetAmazonClient();
                if (amazonClient == null)
                {
                    Log.ForContext<AmazonStashService>().Warning("Amazon Games credentials not configured.");
                    return;
                }

                // STEP 1: Fetch owned entitlements from Amazon API and scan local installs
                var entitlements = await amazonClient.FetchOwnedGamesAsync();
                if (entitlements == null || !entitlements.Any())
                {
                    Log.ForContext<AmazonStashService>().Information("No entitlements returned from Amazon Games API.");
                    return;
                }

                // Deduplicate by Product.Id in case multiple entitlement records point to the same product
                var uniqueProducts = entitlements
                    .Where(e => e.Product != null && !string.IsNullOrWhiteSpace(e.Product.Id) && !string.IsNullOrWhiteSpace(e.Product.Title))
                    .Select(e => e.Product!)
                    .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                var locallyInstalledIds = amazonClient.GetLocallyInstalledGameIds();
                var currentAmazonIds = uniqueProducts.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

                // STEP 2: Load current stash from PostgreSQL
                await using var context = GameStashDbContext.CreateDbContext();
                var dbGames = await context.AmazonStash.ToListAsync();

                var existingLookup = dbGames
                    .Where(g => !string.IsNullOrWhiteSpace(g.ExtId))
                    .GroupBy(g => g.ExtId!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var existingById = dbGames
                    .Where(g => g.Id.HasValue)
                    .ToDictionary(g => g.Id!.Value);

                // A. Remove games no longer present in Amazon entitlements
                var gamesToRemove = dbGames
                    .Where(g => !string.IsNullOrWhiteSpace(g.ExtId) && !currentAmazonIds.Contains(g.ExtId!))
                    .ToList();

                // B. Resolve IGDB IDs for new games or games still holding a negative synthetic ID
                var productsNeedingResolution = uniqueProducts
                    .Where(p => !existingLookup.TryGetValue(p.Id, out var existing) || (existing.Id ?? 0) <= 0)
                    .ToList();

                var igdbClient = await _settingsService.GetIgdbClient();
                var amazonToIgdbMap = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

                if (productsNeedingResolution.Any() && igdbClient != null)
                {
                    amazonToIgdbMap = await ResolveAmazonProductsToIgdbAsync(productsNeedingResolution, igdbClient);
                }

                var gamesToInsert = new List<AmazonStashGame>();
                var gamesToUpdate = new List<AmazonStashGame>();

                // Map Amazon CDN artwork URLs as fallback covers
                var amazonCoverMap = uniqueProducts
                    .ToDictionary(p => p.Id, ExtractAmazonCoverUrl, StringComparer.OrdinalIgnoreCase);

                foreach (var product in uniqueProducts)
                {
                    bool detectedLocally = locallyInstalledIds.Contains(product.Id) ||
                                           _manuallyInstalledOverrides.Contains(product.Id);

                    amazonToIgdbMap.TryGetValue(product.Id, out long resolvedIgdbId);

                    if (existingLookup.TryGetValue(product.Id, out var existing))
                    {
                        bool finalIsInstalled = existing.IsInstalled || detectedLocally;

                        // Upgrade negative synthetic ID to real IGDB primary key if resolved now
                        if ((existing.Id ?? 0) <= 0 && resolvedIgdbId > 0)
                        {
                            context.AmazonStash.Remove(existing);

                            var upgradedGame = new AmazonStashGame
                            {
                                Id = resolvedIgdbId,
                                ExtId = product.Id,
                                Name = product.Title,
                                SortName = existing.SortName,
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
                            existing.Name = product.Title;
                            existing.IsInstalled = finalIsInstalled;

                            if (!gamesToUpdate.Contains(existing))
                                gamesToUpdate.Add(existing);
                        }
                    }
                    else
                    {
                        long targetId = resolvedIgdbId > 0 ? resolvedIgdbId : GetSyntheticId(product.Id);

                        // Prevent duplicate primary key insertion if account owns multiple editions of the same IGDB game
                        if (gamesToInsert.Any(x => x.Id == targetId) || existingById.ContainsKey(targetId))
                            continue;

                        var newGame = new AmazonStashGame
                        {
                            Id = targetId,
                            ExtId = product.Id,
                            Name = product.Title,
                            IsInstalled = detectedLocally
                        };

                        gamesToInsert.Add(newGame);
                    }
                }

                if (gamesToRemove.Count > 0)
                    context.AmazonStash.RemoveRange(gamesToRemove);

                if (gamesToInsert.Count > 0)
                    await context.AmazonStash.AddRangeAsync(gamesToInsert);

                if (gamesToUpdate.Count > 0)
                    context.AmazonStash.UpdateRange(gamesToUpdate);

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

                        if (!string.IsNullOrWhiteSpace(game.ExtId) && amazonCoverMap.TryGetValue(game.ExtId, out var fallbackUrl))
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

                Log.ForContext<AmazonStashService>().Information(
                    "Amazon Games library successfully synchronized. Total: {Total} ({Added} added, {Updated} updated, {Removed} removed).",
                    activeGames.Count, gamesToInsert.Count, gamesToUpdate.Count, gamesToRemove.Count);
            }
            catch (Exception ex)
            {
                Log.ForContext<AmazonStashService>().Error(ex, "Failed to execute Amazon Games library synchronization.");
                throw;
            }
        }

        protected override bool IsMatchingId(AmazonStashGame game, long id)
        {
            return game.Id == id || (!string.IsNullOrWhiteSpace(game.ExtId) && GetSyntheticId(game.ExtId) == id);
        }

        private async Task<Dictionary<string, long>> ResolveAmazonProductsToIgdbAsync(IEnumerable<AmazonProduct> products, IgdbClient igdbClient)
        {
            var amazonToIgdbMap = new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var productsList = products.ToList();
            using var throttler = new SemaphoreSlim(4);

            // 1. Batch case-insensitive title match on /games
            var candidateToProducts = new Dictionary<string, List<AmazonProduct>>(StringComparer.OrdinalIgnoreCase);
            foreach (var product in productsList)
            {
                foreach (var candidate in GetTitleCandidates(product.Title))
                {
                    if (!candidateToProducts.TryGetValue(candidate, out var list))
                        candidateToProducts[candidate] = list = [];
                    list.Add(product);
                }
            }

            var batchTasks = candidateToProducts.Keys.Chunk(25).Select(async nameChunk =>
            {
                await throttler.WaitAsync();
                try
                {
                    var nameFilters = string.Join(" | ", nameChunk.Select(n => $"name ~ \"{n.Replace("\"", "\\\"")}\""));
                    var query = $"fields id, name, version_parent; where ({nameFilters}); limit 100;";

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

                        if (candidateToProducts.TryGetValue(game.Name, out var matchedProducts))
                        {
                            foreach (var p in matchedProducts)
                            {
                                amazonToIgdbMap.TryAdd(p.Id, targetIgdbId);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.ForContext<AmazonStashService>().Warning(ex, "Failed batch title match on IGDB for Amazon Games.");
                }
                finally
                {
                    throttler.Release();
                }
            });

            await Task.WhenAll(batchTasks);

            // 2. Fuzzy search fallback for remaining unmatched titles
            var stillUnmatched = productsList
                .Where(p => !amazonToIgdbMap.ContainsKey(p.Id))
                .ToList();

            if (stillUnmatched.Any())
            {
                var fuzzyTasks = stillUnmatched.Select(async product =>
                {
                    await throttler.WaitAsync();
                    try
                    {
                        var bestCandidate = GetTitleCandidates(product.Title).LastOrDefault() ?? product.Title;
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
                            amazonToIgdbMap.TryAdd(product.Id, targetIgdbId);
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

            return new Dictionary<string, long>(amazonToIgdbMap, StringComparer.OrdinalIgnoreCase);
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

        private static IEnumerable<string> GetTitleCandidates(string rawTitle)
        {
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var clean = rawTitle
                .Replace("®", "")
                .Replace("™", "")
                .Replace("–", "-")
                .Replace("—", "-")
                .Trim();

            if (!string.IsNullOrWhiteSpace(clean))
                candidates.Add(clean);

            var withoutEdition = Regex.Replace(
                clean,
                @"(\s*[-:|]\s*|\s+)(Standard Edition|Game of the Year Edition|GOTY Edition|Deluxe Edition|Gold Edition|Complete Edition|Enhanced Edition|Definitive Edition|Legacy Edition)$",
                "",
                RegexOptions.IgnoreCase).Trim();

            if (!string.IsNullOrWhiteSpace(withoutEdition))
                candidates.Add(withoutEdition);

            var beforeSeparator = Regex.Split(withoutEdition, @"\s*[:|]\s*|\s+-\s+")[0].Trim();
            if (beforeSeparator.Length >= 3)
                candidates.Add(beforeSeparator);

            return candidates;
        }

        private static string? ExtractAmazonCoverUrl(AmazonProduct product)
        {
            var detail = product.ProductDetail;
            if (detail == null) return null;

            if (!string.IsNullOrWhiteSpace(detail.IconUrl))
                return detail.IconUrl;

            return detail.Details?.BackgroundUrl1 ?? detail.Details?.BackgroundUrl2;
        }

        private static long GetSyntheticId(string productId)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(productId));
            long val = BitConverter.ToInt64(hash, 0);
            return val == 0 ? -1 : -Math.Abs(val);
        }
    }
}
