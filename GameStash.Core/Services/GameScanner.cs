using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Serilog; // Added for core infrastructure log mapping

namespace GameStash.Core.Services
{
    public class GameScanner
    {

        // Public method to initiate folder scanning synchronously
        public IEnumerable<string> ScanFolder(string folderPath)
        {
            Log.ForContext<GameScanner>().Information("Initiating synchronous root directory exploration scan for path: {Path}", folderPath);

            // Check if the specified folder exists
            if (!Directory.Exists(folderPath))
            {
                var ex = new DirectoryNotFoundException($"The directory '{folderPath}' does not exist.");
                Log.ForContext<GameScanner>().Error(ex, "Synchronous scan aborted. Target hosting volume boundary not found.");
                throw ex;
            }

            // Begin recursive scanning synchronously
            var foundFiles = ScanDirectoryRecursively(folderPath);

            Log.ForContext<GameScanner>().Information("Synchronous filesystem scan completed. Discovered {Count} target entities.", foundFiles.Count());
            return foundFiles.Order();
        }

        // Public method to initiate folder scanning asynchronously
        public async Task<IEnumerable<string>> ScanFolderAsync(string folderPath)
        {
            Log.ForContext<GameScanner>().Information("Initiating multi-threaded asynchronous directory tree discovery for path: {Path}", folderPath);

            // Check if the specified folder exists
            if (!Directory.Exists(folderPath))
            {
                var ex = new DirectoryNotFoundException($"The directory '{folderPath}' does not exist.");
                Log.ForContext<GameScanner>().Error(ex, "Asynchronous background pipeline aborted. Target volume route missing.");
                throw ex;
            }

            // Begin recursive scanning asynchronously
            var foundFiles = await ScanDirectoryRecursivelyAsync(folderPath).ConfigureAwait(false);

            Log.ForContext<GameScanner>().Information("Asynchronous parallel discovery completed. Discovered {Count} processing targets.", foundFiles.Count());
            return foundFiles.Order();
        }

        // Recursive method to scan directories for specific files synchronously
        private IEnumerable<string> ScanDirectoryRecursively(string folderPath)
        {
            List<string> foundFiles = new List<string>();

            if (Directory.Exists(folderPath))
            {
                Log.ForContext<GameScanner>().Debug("Traversing structural directory node: {Path}", folderPath);
                foundFiles.AddRange(ProcessFolder(folderPath)); // Process files and add to foundFiles

                if (!foundFiles.Contains("gamestash.ignore"))
                {
                    // Process subdirectories, only if gamestash.ignore file isn't present and regardless of whether gamestash.json was found
                    var subDirectories = Directory.GetDirectories(folderPath);
                    foreach (var directory in subDirectories)
                    {
                        foundFiles.AddRange(ScanDirectoryRecursively(directory));
                    }
                }
                else
                {
                    Log.ForContext<GameScanner>().Warning("Skipping structural folder tree descent branch. Exclusion marker detected at: {Path}", folderPath);
                    return Enumerable.Empty<string>();
                }
            }

            return foundFiles;
        }

        // Recursive method to scan directories for specific files asynchronously
        private async Task<IEnumerable<string>> ScanDirectoryRecursivelyAsync(string folderPath)
        {
            ConcurrentBag<string> foundFiles = new ConcurrentBag<string>();

            Log.ForContext<GameScanner>().Debug("Asynchronously inspecting partition sub-tree layer: {Path}", folderPath);
            var result = await ProcessFolderAsync(folderPath).ConfigureAwait(false);

            if (!result.Contains("gamestash.ignore"))
            {
                Parallel.ForEach(result, file => foundFiles.Add(file));

                // Always process subdirectories asynchronously, regardless of whether gamestash.json was found
                var subDirectories = await Task.Run(() => Directory.GetDirectories(folderPath))
                                               .ConfigureAwait(false);

                await Task.WhenAll(subDirectories.Select(directory => Task.Run(() =>
                {
                    var recursiveResult = ScanDirectoryRecursively(directory);
                    Parallel.ForEach(recursiveResult, file => foundFiles.Add(file));
                }))).ConfigureAwait(false);
            }
            else
            {
                Log.ForContext<GameScanner>().Warning("Skipping asynchronous folder processing sub-tree thread. Exclusion marker found at: {Path}", folderPath);
                return Enumerable.Empty<string>();
            }

            return foundFiles;
        }

        // Helper method to process files synchronously (returns IEnumerable<string>)
        private IEnumerable<string> ProcessFolder(string folderPath)
        {
            List<string> foundFiles = new List<string>();

            if (Directory.Exists(folderPath))
            {
                var ignoreFolderFile = Path.Combine(folderPath, "gamestash.ignore");
                var jsonFile = Path.Combine(folderPath, "gamestash.json");

                // Check for the gamestash.ignore file
                if (File.Exists(ignoreFolderFile))
                {
                    Log.ForContext<GameScanner>().Warning("Pruning directory branch assignment. Found 'gamestash.ignore' marker inside: {Path}", folderPath);
                    foundFiles.Add("gamestash.ignore");
                    return foundFiles; // If gamestash.ignore exists, skip this folder
                }

                // Check for the gamestash.json file
                if (File.Exists(jsonFile))
                {
                    Log.ForContext<GameScanner>().Information("Found valid local metadata manifest token: {Path}", jsonFile);
                    foundFiles.Add(jsonFile);
                }
                else
                {
                    // Retrieve files in the current directory
                    var files = Directory.GetFiles(folderPath);
                    foreach (var file in files)
                    {
                        // Only then check .exe files that contain "setup" or "install" in their description or name
                        if (Path.GetExtension(file).Equals(".exe", StringComparison.OrdinalIgnoreCase) &&
                                 FileHasSetupOrInstallInNameOrDescription(file))
                        {
                            Log.ForContext<GameScanner>().Information("Discovered installer signature distribution binary target: {Path}", file);
                            foundFiles.Add(file);
                        }
                    }
                }
            }

            return foundFiles;
        }

        // Helper method to process files asynchronously (returns IEnumerable<string>)
        private async Task<IEnumerable<string>> ProcessFolderAsync(string folderPath)
        {
            ConcurrentBag<string> foundFiles = new ConcurrentBag<string>();

            if (Directory.Exists(folderPath))
            {
                var ignoreFolderFile = Path.Combine(folderPath, "gamestash.ignore");
                var jsonFile = Path.Combine(folderPath, "gamestash.json");

                // Check for the gamestash.ignore file
                if (File.Exists(ignoreFolderFile))
                {
                    Log.ForContext<GameScanner>().Warning("Pruning asynchronous execution task lane. 'gamestash.ignore' file detected at: {Path}", folderPath);
                    return foundFiles;
                }

                // First, check for the gamestash.json file
                if (File.Exists(jsonFile))
                {
                    Log.ForContext<GameScanner>().Information("Found localized manifest descriptor target via async workers: {Path}", jsonFile);
                    foundFiles.Add(jsonFile);
                }
                else
                {
                    await Task.Run(() =>
                    {
                        // Retrieve files in the current directory
                        var files = Directory.GetFiles(folderPath);
                        Parallel.ForEach(files, file =>
                        {
                            // Only then check .exe files that contain "setup" or "install" in their description
                            if (Path.GetExtension(file).Equals(".exe", StringComparison.OrdinalIgnoreCase) &&
                                     FileHasSetupOrInstallInNameOrDescription(file))
                            {
                                Log.ForContext<GameScanner>().Information("Discovered installer verification signature token via concurrent processing loop: {Path}", file);
                                foundFiles.Add(file);
                            }
                        });
                    }).ConfigureAwait(false);
                }
            }

            return foundFiles;
        }

        // Helper method to check if a file's description contains "Setup" or "Install"
        private static bool FileHasSetupOrInstallInNameOrDescription(string filePath)
        {
            try
            {
                var filename = Path.GetFileNameWithoutExtension(filePath);
                // Obtain file description using FileVersionInfo
                var fileInfo = FileVersionInfo.GetVersionInfo(filePath);
                string description = fileInfo.FileDescription ?? string.Empty;

                // Check if the description or filename contains "setup" or "install" (case-insensitive)
                bool isMatch = description.IndexOf("setup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                description.IndexOf("install", StringComparison.OrdinalIgnoreCase) >= 0 ||
                filename.IndexOf("setup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                filename.IndexOf("install", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isMatch)
                {
                    Log.ForContext<GameScanner>().Debug("Binary metadata lookup hit compiled installer signatures inside file: {Name}", Path.GetFileName(filePath));
                }
                return isMatch;
            }
            catch (Exception ex)
            {
                // In case of error (e.g., trying to access properties of a non-executable file)
                Log.ForContext<GameScanner>().Warning(ex, "Failed to analyze portable executable descriptor tables on file boundary target: {Path}", filePath);
                return false;
            }
        }
    }
}