using GameStash.Core.Models;
using GameStash.Core.Services;
using GameStash.Web.GUI.Interfaces;
using IgdbSharp;
using IgdbSharp.Enumerators;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Services
{
    public class LocalLibraryService : ILibraryService
    {
        public static readonly object SyncLock = new();

        // Central in-memory collection aligned with database
        public static ConcurrentBag<Game> LocalLibrary { get; } = new();

        private readonly ISettingsService _settingsService;

        public string LibraryType { get; } = "local";

        public LocalLibraryService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public IReadOnlyList<Game> GetLocalLibrary(string type = "local")
        {
            lock (SyncLock)
            {
                // Returns snapshot list of in-memory concurrent bag
                return LocalLibrary.ToList();
            }
        }

        public async Task InitializeAsync()
        {
            try
            {
                Log.ForContext<LocalLibraryService>().Information("Loading library entries from SQLite repository...");
                var entries = await SqliteHelper.GetLibraryAsync();

                lock (SyncLock)
                {
                    LocalLibrary.Clear();
                    if (entries != null && entries.Count > 0)
                    {
                        // ConcurrentBag does not support AddRange; populate via iteration
                        foreach (var entry in entries)
                        {
                            LocalLibrary.Add(entry);
                        }
                    }
                }

                // Clean up matching folders from SettingsService.FoundFolders
                if (_settingsService != null)
                {
                    lock (SyncLock)
                    {
                        var libraryFolders = new HashSet<string>(
                            LocalLibrary.Where(g => !string.IsNullOrEmpty(g.Folder)).Select(g => g.Folder!),
                            StringComparer.OrdinalIgnoreCase
                        );

                        var remainingFound = _settingsService.FoundFolders
                            .Where(f => !libraryFolders.Contains(f.Folder))
                            .ToList();

                        while (_settingsService.FoundFolders.TryTake(out _)) { }
                        foreach (var item in remainingFound)
                        {
                            _settingsService.FoundFolders.Add(item);
                        }
                    }
                }

                Log.ForContext<LocalLibraryService>().Information("Local library loaded {Count} games from database.", LocalLibrary.Count);

                // Background batch refresh for IGDB metadata
                _ = Task.Run(async () => await RefreshMetadataInBatchesAsync());
            }
            catch (Exception ex)
            {
                Log.ForContext<LocalLibraryService>().Error(ex, "Failed to initialize local library entries from database.");
            }
        }

        // Semaphore to serialize SQLite writes asynchronously without thread-blocking or SQLite lock collisions
        private static readonly SemaphoreSlim _dbLock = new(1, 1);

        private async Task RefreshMetadataInBatchesAsync(int batchSize = 500)
        {
            List<Game> matchedGames;
            lock (SyncLock)
            {
                matchedGames = LocalLibrary.Where(g => g.Id.HasValue && g.Id.Value > 0).ToList();
            }

            if (matchedGames.Count == 0) return;

            try
            {
                var igdbClient = _settingsService.GetIgdbClient();
                const string minimalCardFields = "id, name, cover.*, first_release_date, rating, total_rating, rating_count, total_rating_count";

                // Enforce maximum 500 items per batch to comply with IGDB endpoint limits
                var safeBatchSize = Math.Min(batchSize, 500);
                var chunks = matchedGames.Chunk(safeBatchSize).ToList();
                Log.ForContext<LocalLibraryService>().Information("Dispatching {Chunks} concurrent batch pipeline(s) to IGDB for {Count} games...", chunks.Count, matchedGames.Count);

                int updatedCount = 0;

                // Each batch runs as a self-contained async pipeline
                var batchTasks = chunks.Select(async chunk =>
                {
                    var ids = string.Join(",", chunk.Select(g => g.Id!.Value));
                    var queryString = $"fields {minimalCardFields}; where id = ({ids}); limit {chunk.Length};";

                    // 1. Asynchronously query IGDB for this specific batch
                    var igdbResults = await igdbClient.QueryAsync<IgdbSharp.Models.Game>(
                        Endpoints.Games,
                        query: queryString,
                        requestPriority: RequestPriority.Low
                    );

                    if (igdbResults == null || igdbResults.Length == 0) return;

                    var igdbLookup = igdbResults.Where(g => g.Id.HasValue).ToDictionary(g => g.Id!.Value);
                    var updatedInBatch = new List<Game>();

                    // 2. Map updated metadata and run folder validation in memory
                    foreach (var localGame in chunk)
                    {
                        if (localGame.Id.HasValue && igdbLookup.TryGetValue(localGame.Id.Value, out var igdbData))
                        {
                            localGame.Cover = igdbData.Cover;
                            localGame.FirstReleaseDate = igdbData.FirstReleaseDate ?? localGame.FirstReleaseDate;
                            localGame.Rating = igdbData.Rating ?? localGame.Rating;
                            localGame.TotalRating = igdbData.TotalRating ?? localGame.TotalRating;
                            localGame.RatingCount = igdbData.RatingCount ?? localGame.RatingCount;
                            localGame.IsIgdbMatched = true;
                            localGame.CheckIsFolderValid();

                            updatedInBatch.Add(localGame);
                        }
                    }

                    // 3. Asynchronously persist this batch to SQLite through non-blocking lock
                    await _dbLock.WaitAsync();
                    try
                    {
                        await Task.Run(() =>
                        {
                            foreach (var game in updatedInBatch)
                            {
                                SqliteHelper.InsertOrUpdateLibraryEntry(game);
                            }
                        });
                    }
                    finally
                    {
                        _dbLock.Release();
                    }

                    // Atomically update progress count
                    Interlocked.Add(ref updatedCount, updatedInBatch.Count);
                });

                // Await all batch pipelines concurrently
                await Task.WhenAll(batchTasks);

                Log.ForContext<LocalLibraryService>().Information("Successfully synchronized {Count} library games with IGDB metadata.", updatedCount);
            }
            catch (Exception ex)
            {
                Log.ForContext<LocalLibraryService>().Error(ex, "Background IGDB bulk metadata synchronization encountered an error.");
            }
        }

        public static bool ContainsFolder(string? folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return false;
            lock (SyncLock)
            {
                return LocalLibrary.Any(g => string.Equals(g.Folder, folderPath, StringComparison.OrdinalIgnoreCase));
            }
        }

        public static void RemoveByRootFolder(string rootFolderPath)
        {
            lock (SyncLock)
            {
                var gamesToRemove = LocalLibrary
                    .Where(g => string.Equals(g.RootFolder, rootFolderPath, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (gamesToRemove.Count == 0) return;

                // ConcurrentBag has no Remove method; rebuild remaining items
                var remaining = LocalLibrary
                    .Where(g => !string.Equals(g.RootFolder, rootFolderPath, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                LocalLibrary.Clear();
                foreach (var item in remaining)
                {
                    LocalLibrary.Add(item);
                }

                foreach (var game in gamesToRemove)
                {
                    _ = Task.Run(() => SqliteHelper.RemoveEntryFromLibrary(game));
                }
            }
        }

        public Task<Game?> GetByIdAsync(long id, string type)
        {
            lock (SyncLock)
            {
                switch (type.ToLower())
                {
                    case "local":
                        var libraryGame = LocalLibrary.FirstOrDefault(g => g.Id == id);
                        return Task.FromResult(libraryGame);
                    default:
                        return Task.FromResult<Game?>(null);
                }
            }
        }

        public async Task<bool> ToggleFavoriteAsync(long id, bool isFavorite, string type)
        {
            Game? game;
            lock (SyncLock)
            {
                switch (type.ToLower())
                {
                    case "local":
                        game = LocalLibrary.FirstOrDefault(g => g.Id == id);
                        if (game == null) return false;
                        game.IsFavorite = isFavorite;
                        break;
                    default:
                        return false;
                }
            }

            await Task.Run(() => SqliteHelper.InsertOrUpdateLibraryEntry(game));
            return true;
        }

        public async Task<bool> ToggleInstalledAsync(long id, bool isInstalled, string type)
        {
            Game? game;
            lock (SyncLock)
            {
                switch (type.ToLower())
                {
                    case "local":
                        game = LocalLibrary.FirstOrDefault(g => g.Id == id);
                        if (game == null) return false;
                        game.IsInstalled = isInstalled;
                        break;
                    default:
                        return false;
                }
            }

            await Task.Run(() => SqliteHelper.InsertOrUpdateLibraryEntry(game));
            return true;
        }

        public async Task<bool> UpdateSortNameAsync(long id, string sortName, string type)
        {
            Game? game;
            lock (SyncLock)
            {
                switch (type.ToLower())
                {
                    case "local":
                        game = LocalLibrary.FirstOrDefault(g => g.Id == id);
                        if (game == null) return false;
                        game.SortName = sortName;
                        break;
                    default:
                        return false;
                }
            }

            await Task.Run(() => SqliteHelper.InsertOrUpdateLibraryEntry(game));
            return true;
        }

        public async Task<bool> RemoveAsync(long id, string type)
        {
            Game? game;
            lock (SyncLock)
            {
                switch (type.ToLower())
                {
                    case "local":
                        game = LocalLibrary.FirstOrDefault(g => g.Id == id);
                        if (game == null) return false;

                        // Rebuild ConcurrentBag excluding target item
                        var remaining = LocalLibrary.Where(g => g.Id != id).ToList();
                        LocalLibrary.Clear();
                        foreach (var item in remaining)
                        {
                            LocalLibrary.Add(item);
                        }
                        break;
                    default:
                        return false;
                }
            }

            await Task.Run(() => SqliteHelper.RemoveEntryFromLibrary(game));
            return true;
        }

        public async Task<Game?> RefreshGameAsync(long id, string type)
        {
            Game? game;
            lock (SyncLock)
            {
                switch (type.ToLower())
                {
                    case "local":
                        game = LocalLibrary.FirstOrDefault(g => g.Id == id);
                        break;
                    default:
                        return null;
                }
            }

            if (game == null) return null;

            game.CheckIsFolderValid();
            var igdbClient = _settingsService.GetIgdbClient();
            await game.Refresh(igdbClient);

            await Task.Run(() => SqliteHelper.InsertOrUpdateLibraryEntry(game));
            return game;
        }
    }
}