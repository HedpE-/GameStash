using GameStash.Core.Models;
using GameStash.Core.Services;
using GameStash.Web.GUI.Interfaces;
using IgdbSharp;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly string _databasePath;
        private IgdbClient? _igdbClient;

        // Triggers whenever root folders are added or removed to refresh system watchers
        public event Action? RootFoldersChanged;

        public string IgdbClientId { get; private set; } = string.Empty;
        public string IgdbSecret { get; private set; } = string.Empty;

        // Multi-threaded safe tracking collections
        public ConcurrentBag<RootFolder> RootFolders { get; } = new();
        public ConcurrentBag<Game> FoundFolders { get; } = new();
        public ConcurrentQueue<string> MessageQueue { get; } = new();

        public SettingsService()
        {
            _databasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GameStash", "gamestash.db");
        }

        public async Task InitializeAsync()
        {
            Log.ForContext<SettingsService>().Information("Initializing Database at {Path}...", _databasePath);

            await Task.Run(() => SqliteHelper.InitializeDatabase(_databasePath));

            var settings = await SqliteHelper.GetSettingsAsync();
            if (settings != null)
            {
                IgdbClientId = settings.IGDB_Client_ID ?? string.Empty;
                IgdbSecret = settings.IGDB_Secret ?? string.Empty;

                if (!string.IsNullOrEmpty(settings.RootFolders))
                {
                    var rawPaths = settings.RootFolders.Split(';', StringSplitOptions.RemoveEmptyEntries);

                    foreach (var path in rawPaths)
                    {
                        if (Directory.Exists(path) && !RootFolders.Any(f => f.Path == path))
                        {
                            var folder = new RootFolder(path);
                            RootFolders.Add(folder);

                            // Spin up asynchronous parsing in background without blocking boot
                            _ = ImportGamesFromFolderAsync(folder);
                        }
                    }
                }

                Log.ForContext<SettingsService>().Information("Database loaded successfully. Managed {Count} root folders.", RootFolders.Count);
            }
        }

        public IgdbClient GetIgdbClient()
        {
            if (_igdbClient == null)
            {
                if (string.IsNullOrEmpty(IgdbClientId) || string.IsNullOrEmpty(IgdbSecret))
                {
                    throw new InvalidOperationException("IGDB integration keys are unconfigured.");
                }
                _igdbClient = new IgdbClient(IgdbClientId, IgdbSecret);
            }
            return _igdbClient;
        }

        public async Task UpdateIgdbCredentialsAsync(string clientId, string secret)
        {
            IgdbClientId = clientId;
            IgdbSecret = secret;
            _igdbClient = new IgdbClient(clientId, secret);

            await SqliteHelper.UpdateSettingsEntry("IGDB_Client_Id", clientId);
            await SqliteHelper.UpdateSettingsEntry("IGDB_Secret", secret);
        }

        public void AddRootFolder(string path)
        {
            if (RootFolders.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var rootFolder = new RootFolder(path);
            RootFolders.Add(rootFolder);

            _ = ImportGamesFromFolderAsync(rootFolder);
            UpdateRootFolderEntry();

            // Notify BackgroundService to attach a physical FileSystemWatcher to this new path
            RootFoldersChanged?.Invoke();
        }

        public void RemoveRootFolder(RootFolder folder)
        {
            // Thread-safe filtering mutation for ConcurrentBag tracking states
            var remainingRoots = RootFolders.Where(f => !string.Equals(f.Path, folder.Path, StringComparison.OrdinalIgnoreCase)).ToList();
            RebuildConcurrentBag(RootFolders, remainingRoots);

            // Clean up the static LocalLibrary cache and SQLite database
            LocalLibraryService.RemoveByRootFolder(folder.Path);

            // Clean up cached found folders unimported results
            var remainingFound = FoundFolders.Where(g => !string.Equals(g.RootFolder, folder.Path, StringComparison.OrdinalIgnoreCase)).ToList();
            RebuildConcurrentBag(FoundFolders, remainingFound);

            UpdateRootFolderEntry();

            // Notify BackgroundService to safely kill and dispose of the target path watcher
            RootFoldersChanged?.Invoke();
        }

        public async Task RefreshAllRootFolders()
        {
            var currentRoots = RootFolders.ToList();

            await Task.Run(() => Parallel.ForEach(currentRoots, async rootFolder =>
            {
                var targetFolders = FoundFolders.Where(f => f.RootFolder == rootFolder.Path).ToList();

                // Remove previous unimported results from this root path to avoid overlaps
                var remainingFound = FoundFolders.Except(targetFolders).ToList();
                RebuildConcurrentBag(FoundFolders, remainingFound);

                await ImportGamesFromFolderAsync(rootFolder);
            }));
        }

        private async Task ImportGamesFromFolderAsync(RootFolder folder)
        {
            try
            {
                MessageQueue.Enqueue($"Importing game folders from {folder.Path}");
                await folder.FindGames();

                // Filters out directory matches that are already tracked or mapped in our static LocalLibrary state
                var identifiedGames = folder.FoundGames
                    .Where(x => !LocalLibraryService.ContainsFolder(x.Folder))
                    .OrderBy(g => g.MatchName)
                    .ToList();

                foreach (var game in identifiedGames)
                {
                    if (!FoundFolders.Any(f => string.Equals(f.Folder, game.Folder, StringComparison.OrdinalIgnoreCase)))
                    {
                        FoundFolders.Add(game);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ForContext<SettingsService>().Error(ex, "Failed folder imports from core path: {Path}", folder.Path);
            }
            finally
            {
                MessageQueue.Enqueue($"Finished importing game folders from {folder.Path}");
            }
        }

        private void UpdateRootFolderEntry()
        {
            string combinedPaths = string.Join(";", RootFolders.Select(f => f.Path));
            SqliteHelper.UpdateSettingsEntry("RootFolders", combinedPaths);
        }

        // Structural mutation utility to synchronize runtime ConcurrentBags without data race leaks
        private void RebuildConcurrentBag<T>(ConcurrentBag<T> bag, List<T> updatedItems)
        {
            while (bag.TryTake(out _)) { }
            foreach (var item in updatedItems)
            {
                bag.Add(item);
            }
        }
    }
}