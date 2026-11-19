using GameStash.Core.Data;
using GameStash.Core.Models;
using GameStash.Core.Services;
using GameStash.Web.GUI.Interfaces;
using IgdbSharp;
using IgdbSharp.Enumerators;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Services
{
    public class LocalStashService(ISettingsService settingsService, IRootFoldersService rootFoldersService) : StashServiceBase<LocalStashGame>, ILocalStashService
    {
        // Central in-memory collection aligned with database
        //public static List<LocalStashGame> LocalStash { get; } = new();
        public static List<LocalStashGame> Playlist { get; } = new();

        private readonly ISettingsService _settingsService = settingsService;
        private readonly IRootFoldersService _rootFoldersService = rootFoldersService;

        protected override async Task ExecuteInitializeAsync()
        {
            try
            {
                Log.ForContext<LocalStashService>().Information("Loading stash entries from PostgreSQL repository...");

                using var context = new GameStashDbContext();

                try
                {
                    await context.Database.MigrateAsync();
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to apply migrations to PostgreSQL database. Please check your connection string and database configuration.\n" + ex.Message);
                }

                var entries = await context.LocalStash.ToListAsync();

                lock (SyncLock)
                {
                    CachedGames.Clear();
                    if (entries != null && entries.Count > 0)
                    {
                        foreach (var entry in entries)
                        {
                            CachedGames.Add(entry);
                            if (entry.IsInPlaylist)
                                Playlist.Add(entry);
                        }
                    }
                }

                // Clean up matching folders from SettingsService.FoundFolders
                if (_rootFoldersService != null)
                {
                    lock (SyncLock)
                    {
                        var stashFolders = new HashSet<string>(
                            CachedGames.Where(g => !string.IsNullOrEmpty(g.Folder)).Select(g => g.Folder!),
                            StringComparer.OrdinalIgnoreCase
                        );

                        var remainingFound = _rootFoldersService.FoundFolders
                            .Where(f => !stashFolders.Contains(f.Folder))
                            .ToList();

                        _rootFoldersService.RemoveFoundFolders(remainingFound.Select(f => f.Folder));
                        foreach (var item in remainingFound)
                        {
                            _rootFoldersService.AddRootFolderAsync(item.Folder);
                        }
                    }
                }

                Log.ForContext<LocalStashService>().Information("Local stash loaded {Count} games from PostgreSQL.", CachedGames.Count);

                // Background batch refresh for IGDB metadata
                _ = Task.Run(async () => await RefreshMetadataInBatchesAsync());
            }
            catch (Exception ex)
            {
                Log.ForContext<LocalStashService>().Error(ex, "Failed to initialize local library entries from PostgreSQL database.");
            }
        }
        protected override bool IsMatchingId(LocalStashGame game, long id)
        {
            return game.Id == id;
        }

        public IReadOnlyList<LocalStashGame> GetPlaylist()
        {
            lock (SyncLock)
            {
                return Playlist;
            }
        }

        public async Task Flush()
        {
            try
            {
                using var context = new GameStashDbContext();
                lock (SyncLock)
                {
                    context.LocalStash.RemoveRange(CachedGames);
                    CachedGames.Clear();
                }
                await context.SaveChangesAsync();
                Log.ForContext<LocalStashService>().Information("Local library flushed successfully.");
            }
            catch (Exception ex)
            {
                Log.ForContext<LocalStashService>().Error(ex, "Failed to flush local library entries from PostgreSQL database.");
            }
        }

        public async Task AddGameToLibrary(LocalStashGame game)
        {
            await using var context = GameStashDbContext.CreateDbContext();
            // Add to static in-memory LocalLibrary and persist to DB
            lock (SyncLock)
            {
                if (!CachedGames.Any(l => l.Folder == game.Folder))
                {
                    CachedGames.Add(game);
                    context.LocalStash.Add(game);
                }
            }
            await context.SaveChangesAsync();
        }

        public async Task RemoveGameFromStash(string id)
        {
            var gameToRemove = CachedGames.FirstOrDefault(g => g.Id.ToString() == id);
            if (gameToRemove != null)
            {
                await RemoveGameFromLibrary(gameToRemove);
            }
        }

        public async Task RemoveGameFromLibrary(LocalStashGame game)
        {
            await using var context = GameStashDbContext.CreateDbContext();

            if (CachedGames.Any(l => l.Folder == game.Folder))
            {
                CachedGames.Remove(game);
                context.LocalStash.Remove(game);
                await context.SaveChangesAsync();
            }

            await game.Reset(await _settingsService.GetIgdbClient());
            _rootFoldersService.AddFoundFolder(game);
        }

        public async Task AddGameToPlaylist(LocalStashGame game)
        {
            await using var context = GameStashDbContext.CreateDbContext();
            // Add to static in-memory LocalLibrary and persist to DB
            lock (SyncLock)
            {
                if (!Playlist.Any(l => l.Folder == game.Folder) && !game.IsInPlaylist)
                {
                    game.IsInPlaylist = true;
                    Playlist.Add(game);
                    context.LocalStash.Update(game);
                }
            }
            await context.SaveChangesAsync();
        }

        public async Task RemoveGameFromPlaylist(LocalStashGame game)
        {
            await using var context = GameStashDbContext.CreateDbContext();
            // Add to static in-memory LocalLibrary and persist to DB
            lock (SyncLock)
            {
                if (Playlist.Any(l => l.Folder == game.Folder) && game.IsInPlaylist)
                {
                    game.IsInPlaylist = false;
                    Playlist.Remove(game);
                    context.LocalStash.Update(game);
                }
            }
            await context.SaveChangesAsync();
        }

        private async Task RefreshMetadataInBatchesAsync(int batchSize = 300)
        {
            var matchedGames = CachedGames.Where(g => g.Id.HasValue && g.Id.Value > 0).ToList();
            if (matchedGames.Count == 0) return;

            try
            {
                var igdbClient = await _settingsService.GetIgdbClient();

                var safeBatchSize = Math.Min(batchSize, 300);
                var chunks = matchedGames.Chunk(safeBatchSize).ToList();
                Log.ForContext<LocalStashService>().Information(
                    "Dispatching {Chunks} concurrent batch pipeline(s) to IGDB for {Count} games...",
                    chunks.Count, matchedGames.Count);

                int updatedCount = 0;

                var batchTasks = chunks.Select(async chunk =>
                {
                    var ids = string.Join(",", chunk.Select(g => g.Id!.Value));
                    var queryString = $"fields {StashGame.StashGameCardQueryFields}; where id = ({ids});";

                    var igdbResults = await igdbClient.QueryAsync<IgdbSharp.Models.Game>(
                        Endpoints.Games,
                        query: queryString,
                        limitResults: chunk.Length,
                        requestPriority: RequestPriority.Low
                    );

                    if (igdbResults == null || igdbResults.Length == 0) return;

                    // Fast O(1) dictionary lookup by IGDB ID
                    var igdbLookup = igdbResults
                        .Where(g => g.Id.HasValue)
                        .ToDictionary(g => g.Id!.Value);

                    var updatedInBatch = new List<LocalStashGame>();

                    foreach (var localGame in chunk)
                    {
                        if (localGame.Id.HasValue && igdbLookup.TryGetValue(localGame.Id.Value, out var queryGame))
                        {
                            // Apply deep reflection update (preserving existing loaded objects)
                            localGame.Update(queryGame);
                            localGame.CheckIsFolderValid();
                            updatedInBatch.Add(localGame);
                        }
                    }

                    if (updatedInBatch.Count > 0)
                    {
                        // Single batch update to PostgreSQL instead of individual AnyAsync queries
                        using var context = new GameStashDbContext();
                        context.LocalStash.UpdateRange(updatedInBatch);
                        await context.SaveChangesAsync();

                        // Thread-safe increment across concurrent batch tasks
                        Interlocked.Add(ref updatedCount, updatedInBatch.Count);
                    }
                });

                await Task.WhenAll(batchTasks);
                Log.ForContext<LocalStashService>().Information(
                    "Successfully synchronized {Count} library games with IGDB metadata.", updatedCount);
            }
            catch (Exception ex)
            {
                Log.ForContext<LocalStashService>().Error(ex, "Background IGDB bulk metadata synchronization encountered an error.");
            }
        }

        public async Task RemoveByRootFolder(string rootFolderPath)
        {
            var gamesToRemove = CachedGames
                .Where(g => string.Equals(g.RootFolder, rootFolderPath, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (gamesToRemove.Count == 0) return;

            _ = Task.Run(async () =>
            {
                foreach (var game in gamesToRemove)
                    await RemoveGameFromLibrary(game);
            });            
        }

        public Task<LocalStashGame> GetByIdAsync(long id)
        {
            var libraryGame = CachedGames.FirstOrDefault(g => g.Id == id);
            return Task.FromResult(libraryGame);
        }

        public async Task<LocalStashGame> RefreshGameAsync(long id)
        {
            LocalStashGame game;
            lock (SyncLock)
            {
                game = CachedGames.FirstOrDefault(g => g.Id == id);
            }
            if (game == null) return null;
            game.CheckIsFolderValid();
            var igdbClient = await _settingsService.GetIgdbClient();
            await game.Refresh(igdbClient);
            using var context = new GameStashDbContext();
            context.LocalStash.Update(game);
            await context.SaveChangesAsync();
            return game;
        }

        public async Task<bool> UpdateGamePropertyAsync(long id, string propertyName, object? value)
        {
            var prop = typeof(LocalStashGame).GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (prop == null || !prop.CanWrite)
                return false;

            LocalStashGame? game;

            lock (SyncLock)
            {
                game = CachedGames.FirstOrDefault(g => g.Id == id);
                if (game == null)
                    return false;

                // Unwrap System.Text.Json JsonElement if incoming value is from the API
                object? targetValue = value;
                if (value is System.Text.Json.JsonElement jsonElement)
                {
                    targetValue = jsonElement.ValueKind switch
                    {
                        System.Text.Json.JsonValueKind.True => true,
                        System.Text.Json.JsonValueKind.False => false,
                        System.Text.Json.JsonValueKind.String => jsonElement.GetString(),
                        System.Text.Json.JsonValueKind.Number => jsonElement.TryGetInt64(out var l) ? l : jsonElement.GetDouble(),
                        System.Text.Json.JsonValueKind.Null => null,
                        _ => jsonElement.GetRawText()
                    };
                }

                // Handle type conversion (e.g. primitives, nullables)
                if (targetValue != null && !prop.PropertyType.IsInstanceOfType(targetValue))
                {
                    var underlyingType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                    targetValue = Convert.ChangeType(targetValue, underlyingType);
                }

                prop.SetValue(game, targetValue);
            }

            using var context = new GameStashDbContext();
            context.LocalStash.Update(game);
            await context.SaveChangesAsync();

            return true;
        }
    }
}