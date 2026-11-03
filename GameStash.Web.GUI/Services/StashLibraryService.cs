using GameStash.Core.Data;
using GameStash.Core.Models;
using GameStash.Web.GUI.Helpers;
using GameStash.Web.GUI.Interfaces;
using IgdbSharp;
using IgdbSharp.Enumerators;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Services
{
    public class StashLibraryService(ISettingsService settingsService, IRootFoldersService rootFoldersService) : IStashService
    {
        public static readonly object SyncLock = new();

        // Central in-memory collection aligned with database
        public static List<StashGame> LocalLibrary { get; } = new();

        private readonly ISettingsService _settingsService = settingsService;
        private readonly IRootFoldersService _rootFoldersService = rootFoldersService;

        public string LibraryType { get; }

        public IReadOnlyList<StashGame> GetLibrary(string type)
        {
            lock (SyncLock)
            {
                switch (type.ToLower())
                {
                    case "local":
                        return LocalLibrary.ToList();
                    default:
                        throw new ArgumentException($"Unsupported library type: {type}");
                }
            }
        }

        public async Task InitializeAsync()
        {
            try
            {
                Log.ForContext<StashLibraryService>().Information("Loading library entries from PostgreSQL repository...");

                using var context = new GameStashDbContext();
                await context.Database.MigrateAsync();
                var entries = await context.LocalLibrary.ToListAsync();

                lock (SyncLock)
                {
                    LocalLibrary.Clear();
                    if (entries != null && entries.Count > 0)
                    {
                        foreach (var entry in entries)
                        {
                            LocalLibrary.Add(entry);
                        }
                    }
                }

                // Clean up matching folders from SettingsService.FoundFolders
                if (_rootFoldersService != null)
                {
                    lock (SyncLock)
                    {
                        var libraryFolders = new HashSet<string>(
                            LocalLibrary.Where(g => !string.IsNullOrEmpty(g.Folder)).Select(g => g.Folder!),
                            StringComparer.OrdinalIgnoreCase
                        );

                        var remainingFound = _rootFoldersService.FoundFolders
                            .Where(f => !libraryFolders.Contains(f.Folder))
                            .ToList();

                        _rootFoldersService.RemoveFoundFolders(remainingFound.Select(f => f.Folder));
                        foreach (var item in remainingFound)
                        {
                            _rootFoldersService.AddRootFolderAsync(item.Folder);
                        }
                    }
                }

                Log.ForContext<StashLibraryService>().Information("Local library loaded {Count} games from PostgreSQL.", LocalLibrary.Count);

                // Background batch refresh for IGDB metadata
                _ = Task.Run(async () => await RefreshMetadataInBatchesAsync());
            }
            catch (Exception ex)
            {
                Log.ForContext<StashLibraryService>().Error(ex, "Failed to initialize local library entries from PostgreSQL database.");
            }
        }

        public async Task Flush()
        {
            try
            {
                using var context = new GameStashDbContext();
                lock (SyncLock)
                {
                    context.LocalLibrary.RemoveRange(LocalLibrary);
                    LocalLibrary.Clear();
                }
                await context.SaveChangesAsync();
                Log.ForContext<StashLibraryService>().Information("Local library flushed successfully.");
            }
            catch (Exception ex)
            {
                Log.ForContext<StashLibraryService>().Error(ex, "Failed to flush local library entries from PostgreSQL database.");
            }
        }

        public async Task AddGameToLibrary(StashGame game)
        {
            await using var context = GameStashDbContext.CreateDbContext();
            // Add to static in-memory LocalLibrary and persist to DB
            lock (SyncLock)
            {
                if (!LocalLibrary.Any(l => l.Folder == game.Folder))
                {
                    LocalLibrary.Add(game);
                    context.LocalLibrary.Add(game);
                }
            }
            await context.SaveChangesAsync();
        }

        public async Task RemoveGameFromLibrary(string id)
        {
            var gameToRemove = LocalLibrary.FirstOrDefault(g => g.Id.ToString() == id);
            if (gameToRemove != null)
            {
                await RemoveGameFromLibrary(gameToRemove);
            }
        }

        public async Task RemoveGameFromLibrary(StashGame game)
        {
            await using var context = GameStashDbContext.CreateDbContext();

            if (LocalLibrary.Any(l => l.Folder == game.Folder))
            {
                LocalLibrary.Remove(game);
                context.LocalLibrary.Remove(game);
                await context.SaveChangesAsync();
            }

            await game.Reset(_settingsService.GetIgdbClient());
            _rootFoldersService.AddFoundFolder(game);
        }

        private async Task RefreshMetadataInBatchesAsync(int batchSize = 500)
        {
            List<StashGame> matchedGames;
            matchedGames = LocalLibrary.Where(g => g.Id.HasValue && g.Id.Value > 0).ToList();

            if (matchedGames.Count == 0) return;

            try
            {
                var igdbClient = _settingsService.GetIgdbClient();
                const string minimalCardFields = "id, name, cover.*, first_release_date, rating, total_rating, rating_count, total_rating_count";

                var safeBatchSize = Math.Min(batchSize, 500);
                var chunks = matchedGames.Chunk(safeBatchSize).ToList();
                Log.ForContext<StashLibraryService>().Information("Dispatching {Chunks} concurrent batch pipeline(s) to IGDB for {Count} games...", chunks.Count, matchedGames.Count);

                int updatedCount = 0;

                var batchTasks = chunks.Select(async chunk =>
                {
                    var ids = string.Join(",", chunk.Select(g => g.Id!.Value));
                    var queryString = $"fields {minimalCardFields}; where id = ({ids}); limit {chunk.Length};";

                    var igdbResults = await igdbClient.QueryAsync<IgdbSharp.Models.Game>(
                        Endpoints.Games,
                        query: queryString,
                        requestPriority: RequestPriority.Low
                    );

                    if (igdbResults == null || igdbResults.Length == 0) return;

                    var igdbLookup = igdbResults.Where(g => g.Id.HasValue).ToDictionary(g => g.Id!.Value);
                    var updatedInBatch = new List<StashGame>();

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

                    // Gravação assíncrona isolada por lote no PostgreSQL através do DbContext do EF Core
                    using var context = new GameStashDbContext();
                    foreach (var game in updatedInBatch)
                    {
                        var exists = await context.LocalLibrary.AnyAsync(g => g.Id == game.Id);
                        if (exists)
                            context.LocalLibrary.Update(game);
                        else
                            await context.LocalLibrary.AddAsync(game);
                    }
                    await context.SaveChangesAsync();

                    lock (SyncLock)
                    {
                        updatedCount += updatedInBatch.Count;
                    }
                });

                await Task.WhenAll(batchTasks);
                Log.ForContext<StashLibraryService>().Information("Successfully synchronized {Count} library games with IGDB metadata.", updatedCount);
            }
            catch (Exception ex)
            {
                Log.ForContext<StashLibraryService>().Error(ex, "Background IGDB bulk metadata synchronization encountered an error.");
            }
        }

        public static bool ContainsFolder(string? folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return false;

            return LocalLibrary.Any(g => string.Equals(g.Folder, folderPath, StringComparison.OrdinalIgnoreCase));
        }

        public async Task RemoveByRootFolder(string rootFolderPath)
        {
            var gamesToRemove = LocalLibrary
                .Where(g => string.Equals(g.RootFolder, rootFolderPath, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (gamesToRemove.Count == 0) return;

            _ = Task.Run(async () =>
            {
                foreach (var game in gamesToRemove)
                    await RemoveGameFromLibrary(game);
            });            
        }

        public Task<StashGame?> GetByIdAsync(long id, string type)
        {
            if (string.Equals(type, "local", StringComparison.OrdinalIgnoreCase))
            {
                var libraryGame = LocalLibrary.FirstOrDefault(g => g.Id == id);
                return Task.FromResult(libraryGame);
            }
            return Task.FromResult<StashGame?>(null);
        }// Exemplo de como ficam os métodos de atualização (SEM LOCK)
        public async Task<bool> ToggleFavoriteAsync(long id, bool isFavorite, string type)
        {
            if (!string.Equals(type, "local", StringComparison.OrdinalIgnoreCase)) return false;

            // ConcurrentBag permite ler concorrentemente sem crashar
            var game = LocalLibrary.FirstOrDefault(g => g.Id == id);
            if (game == null) return false;

            game.IsFavorite = isFavorite;

            using var context = new GameStashDbContext();
            context.LocalLibrary.Update(game);
            await context.SaveChangesAsync(); // Postgres trata da concorrência aqui
            return true;
        }


        public async Task<bool> ToggleInstalledAsync(long id, bool isInstalled, string type)
        {
            StashGame? game;
            lock (SyncLock)
            {
                if (!string.Equals(type, "local", StringComparison.OrdinalIgnoreCase)) return false;

                game = LocalLibrary.FirstOrDefault(g => g.Id == id);
                if (game == null) return false;
                game.IsInstalled = isInstalled;
            }
            using var context = new GameStashDbContext();
            context.LocalLibrary.Update(game);
            await context.SaveChangesAsync();
            return true;
        }
        public async Task<bool> UpdateSortNameAsync(long id, string sortName, string type)
        {
            StashGame? game;
            lock (SyncLock)
            {
                if (!string.Equals(type, "local", StringComparison.OrdinalIgnoreCase)) return false;
                game = LocalLibrary.FirstOrDefault(g => g.Id == id);
                if (game == null) return false;
                game.SortName = sortName;
            }
            using var context = new GameStashDbContext();
            context.LocalLibrary.Update(game);
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<StashGame?> RefreshGameAsync(long id, string type)
        {
            StashGame? game;
            lock (SyncLock)
            {
                if (!string.Equals(type, "local", StringComparison.OrdinalIgnoreCase)) return null;
                game = LocalLibrary.FirstOrDefault(g => g.Id == id);
            }
            if (game == null) return null;
            game.CheckIsFolderValid();
            var igdbClient = _settingsService.GetIgdbClient();
            await game.Refresh(igdbClient);
            using var context = new GameStashDbContext();
            context.LocalLibrary.Update(game);
            await context.SaveChangesAsync();
            return game;
        }
    }
}