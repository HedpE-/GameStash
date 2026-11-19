using GameStash.Core.Data;
using GameStash.Core.Models;
using GameStash.Web.GUI.Helpers;
using GameStash.Web.GUI.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Services
{
    public class RootFoldersService : IRootFoldersService
    {
        private readonly List<RootFolder> _rootFolders = new();
        private readonly List<LocalStashGame> _foundFolders = new();

        public event Action? RootFoldersChanged;

        public IReadOnlyList<RootFolder> RootFolders { get => _rootFolders.ToList(); }

        public IReadOnlyList<LocalStashGame> FoundFolders { get => _foundFolders.ToList(); }

        public async Task InitializeAsync()
        {
            Log.ForContext<SettingsService>().Information("Initializing Settings and Root Folders from PostgreSQL...");

            await using var context = GameStashDbContext.CreateDbContext();

            // 2. Load and sanitize monitored root folders from 'root_folders' table
            var rootFolders = await context.RootFolders.ToListAsync();

            if (rootFolders.Count > 0)
            {
                var existingFolders = rootFolders
                    .Where(f => Directory.Exists(f.Path))
                    .OrderBy(f => f.Path.Length) // Process parent directories before subdirectories
                    .ToList();

                var validRootFolders = new List<RootFolder>();
                var foldersToRemove = new List<RootFolder>();

                foreach (var folder in existingFolders)
                {
                    string normalized = Utils.NormalizePath(folder.Path);
                    bool isSubfolder = validRootFolders.Any(parent => Utils.IsSubdirectoryOf(normalized, Utils.NormalizePath(parent.Path)));

                    if (!isSubfolder)
                    {
                        validRootFolders.Add(folder);
                    }
                    else
                    {
                        // Mark obsolete subdirectories for database cleanup
                        foldersToRemove.Add(folder);
                    }
                }

                // Also purge records whose path no longer exists on disk
                var nonExistentFolders = rootFolders.Where(f => !Directory.Exists(f.Path)).ToList();
                foldersToRemove.AddRange(nonExistentFolders);

                if (foldersToRemove.Count > 0)
                {
                    context.RootFolders.RemoveRange(foldersToRemove);
                    await context.SaveChangesAsync();
                    Log.ForContext<SettingsService>().Information(
                        "Purged {Count} obsolete or subfolder entries from 'root_folders' table in PostgreSQL.",
                        foldersToRemove.Count
                    );
                }

                _rootFolders.Clear();
                foreach (var folder in validRootFolders)
                {
                    _rootFolders.Add(folder);
                    // Trigger background scan for each root folder
                    _ = ImportGamesFromFolderAsync(folder);
                }
            }

            Log.ForContext<SettingsService>().Information(
                "PostgreSQL initialization completed. Monitored root folders: {Count}",
                _rootFolders.Count
            );
        }

        public async Task AddRootFolderAsync(string path)
        {
            // Persist changes to PostgreSQL
            await using var context = GameStashDbContext.CreateDbContext();

            string normalized = Utils.NormalizePath(path.Trim());

            if (!Directory.Exists(normalized))
                throw new DirectoryNotFoundException($"Directory '{normalized}' does not exist on filesystem.");

            RootFolder newFolder;
            List<RootFolder> childFoldersToRemove = new();

            // Prevent duplicate entries
            if (_rootFolders.Any(f => string.Equals(Utils.NormalizePath(f.Path), normalized, StringComparison.OrdinalIgnoreCase)))
                return;

            // Reject if path is inside an already registered root folder
            if (_rootFolders.Any(f => Utils.IsSubdirectoryOf(normalized, Utils.NormalizePath(f.Path))))
                throw new InvalidOperationException($"'{normalized}' is already a subfolder of an existing root folder.");

            // If adding a parent folder, remove child root folders
            childFoldersToRemove = _rootFolders.Where(f => Utils.IsSubdirectoryOf(Utils.NormalizePath(f.Path), normalized)).ToList();
            foreach (var child in childFoldersToRemove)
            {
                await RemoveRootFolderAsync(child.Path);
            }

            newFolder = new RootFolder(normalized);
            _rootFolders.Add(newFolder);

            if (childFoldersToRemove.Count > 0)
            {
                var pathsToRemove = childFoldersToRemove.Select(c => Utils.NormalizePath(c.Path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var dbChildren = await context.RootFolders
                    .Where(r => pathsToRemove.Contains(r.Path))
                    .ToListAsync();

                context.RootFolders.RemoveRange(dbChildren);
            }

            context.RootFolders.Add(newFolder);
            await context.SaveChangesAsync();

            RootFoldersChanged?.Invoke();

            // Run scanner in background
            _ = ImportGamesFromFolderAsync(newFolder);
        }

        public async Task RemoveRootFolderAsync(string path)
        {
            string normalized = Utils.NormalizePath(path.Trim());

            var folder = _rootFolders.FirstOrDefault(f => string.Equals(Utils.NormalizePath(f.Path), normalized, StringComparison.OrdinalIgnoreCase));
            if (folder == null) return;

            _rootFolders.Remove(folder);

            // Remove from PostgreSQL 'root_folders' table
            await using var context = GameStashDbContext.CreateDbContext();
            var dbFolder = await context.RootFolders.FirstOrDefaultAsync(r => r.Path == normalized || r.Path == path);
            if (dbFolder != null)
            {
                context.RootFolders.Remove(dbFolder);
                await context.SaveChangesAsync();
            }

            _foundFolders.RemoveAll(g => string.Equals(Utils.NormalizePath(g.RootFolder), normalized, StringComparison.OrdinalIgnoreCase));

            RootFoldersChanged?.Invoke();
        }

        public async Task RefreshRootFolderAsync(string path)
        {
            string normalized = Utils.NormalizePath(path.Trim());
            RootFolder? folder;

            folder = _rootFolders.FirstOrDefault(f => string.Equals(Utils.NormalizePath(f.Path), normalized, StringComparison.OrdinalIgnoreCase));

            if (folder != null)
            {
                _foundFolders.RemoveAll(f => string.Equals(Utils.NormalizePath(f.RootFolder), normalized, StringComparison.OrdinalIgnoreCase));
                await folder.FindGames();

                await ImportGamesFromFolderAsync(folder);
            }
        }

        public async Task RefreshRootFolderAsync(int id)
        {
            RootFolder? folder = _rootFolders.FirstOrDefault(f => f.Id == id);
            if (folder == null) return;

            folder = _rootFolders.FirstOrDefault(f => string.Equals(Utils.NormalizePath(f.Path), folder.Path, StringComparison.OrdinalIgnoreCase));

            if (folder != null)
            {
                _foundFolders.RemoveAll(f => string.Equals(Utils.NormalizePath(f.RootFolder), folder.Path, StringComparison.OrdinalIgnoreCase));

                await ImportGamesFromFolderAsync(folder);
            }
        }

        public async Task RefreshAllRootFoldersAsync()
        {
            List<RootFolder> foldersToRefresh;
            foldersToRefresh = _rootFolders.ToList();

            var scanTasks = foldersToRefresh.Select(async folder =>
            {
                 _foundFolders.RemoveAll(f => string.Equals(Utils.NormalizePath(f.RootFolder), folder.Path, StringComparison.OrdinalIgnoreCase));
                await ImportGamesFromFolderAsync(folder);
            });

            await Task.WhenAll(scanTasks);
        }

        public void AddFoundFolder(LocalStashGame game)
        {
            if (game.IsFolderValid)
            {
                if (!FoundFolders.Any(f => f.Folder == game.Folder))
                {
                    _foundFolders.Add(game);
                    //_foundFolders.Sort((a, b) => string.Compare(a.MatchName, b.MatchName, StringComparison.OrdinalIgnoreCase));
                }
            }
        }

        public void AddFoundFolders(IEnumerable<LocalStashGame> games)
        {
            foreach (LocalStashGame game in games)
                AddFoundFolder(game);
        }

        public void RemoveFoundFolder(string folderPath)
        {
            string normalized = Utils.NormalizePath(folderPath);
            _foundFolders.RemoveAll(g => string.Equals(Utils.NormalizePath(g.Folder), normalized, StringComparison.OrdinalIgnoreCase));
            //_foundFolders.Sort((a, b) => string.Compare(a.MatchName, b.MatchName, StringComparison.OrdinalIgnoreCase));
        }

        public void RemoveFoundFolders(IEnumerable<string> folderPaths)
        {
            _foundFolders.RemoveAll(g => !string.IsNullOrEmpty(g.Folder) && folderPaths.Contains(Utils.NormalizePath(g.Folder)));
            //_foundFolders.Sort((a, b) => string.Compare(a.MatchName, b.MatchName, StringComparison.OrdinalIgnoreCase));
        }

        public void ClearFoundFolders()
        {
            _foundFolders.Clear();
        }

        private async Task ImportGamesFromFolderAsync(RootFolder folder)
        {
            try
            {
                await folder.FindGames();

                await using var context = GameStashDbContext.CreateDbContext();
                var importedPaths = await context.LocalStash
                    .Where(g => g.Folder != null)
                    .Select(g => g.Folder)
                    .ToListAsync();

                var importedSet = new HashSet<string>(importedPaths, StringComparer.OrdinalIgnoreCase);

                var identifiedGames = (folder.FoundGames ?? Enumerable.Empty<LocalStashGame>())
                    .Where(x => !string.IsNullOrEmpty(x.Folder) && !importedSet.Contains(x.Folder))
                    .OrderBy(g => g.MatchName)
                    .ToList();

                foreach (var game in identifiedGames)
                {
                    game.IsIgdbMatched = false;
                    if (!_foundFolders.Any(f => string.Equals(Utils.NormalizePath(f.Folder), Utils.NormalizePath(game.Folder), StringComparison.OrdinalIgnoreCase)))
                    {
                        _foundFolders.Add(game);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ForContext<SettingsService>().Error(ex, "Failed scanning root folder: {Path}", folder.Path);
            }
            finally
            {
                Log.ForContext<SettingsService>().Information("Completed scanning directory: {Path}", folder.Path);
            }
        }

        public void IgnoreFoundFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new ArgumentException("Folder path cannot be null or empty.", nameof(folderPath));

            var folder = _foundFolders.FirstOrDefault(f => string.Equals(f.Folder, folderPath, StringComparison.OrdinalIgnoreCase)) ??
                throw new InvalidOperationException($"No found folder matches the provided path: {folderPath}");

            try
            {
                // Ensure the directory exists before creating the ignore marker
                if (!Directory.Exists(folderPath))
                    throw new InvalidOperationException($"The provided path does not exist: {folderPath}");
                
                string ignoreFilePath = Path.Combine(folderPath, "gamestash.ignore");

                // Create an empty ignore file and immediately release the file stream handle
                File.Create(ignoreFilePath).Dispose();

                Log.ForContext<LocalStashService>().Information("Created ignore marker file at: {Path}", ignoreFilePath);
                
            }
            catch (Exception ex)
            {
                Log.ForContext<LocalStashService>().Error(ex, "Failed to create 'gamestash.ignore' file inside folder: {Path}\n{ex}", folderPath, ex.Message);
                throw new InvalidOperationException($"Failed to ignore the folder: {folderPath}.");
            }

            _foundFolders.Remove(folder);
        }
    }
}