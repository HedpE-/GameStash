using GameStash.Core.Models;
using GameStash.Core.Services;
using GameStash.Web.GUI.DTO;
using GameStash.Web.GUI.Interfaces;
using GameStash.Web.GUI.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Controllers
{
    [ApiController]
    [Route("api/import")]
    public class ImportController(ISettingsService settingsService, ILibraryService libraryService) : ControllerBase
    {
        private readonly ISettingsService _settingsService = settingsService;
        private readonly ILibraryService _libraryService = libraryService;

        [HttpGet("folders")]
        public IActionResult GetFoundFolders()
        {
            var dtos = _settingsService.FoundFolders
                .OrderBy(f => f.MatchName)
                .Select(ImportFolderDto.FromModel)
                .ToArray();

            return Ok(dtos);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshRootFolders()
        {
            await _settingsService.RefreshAllRootFolders();
            return Ok(new { Success = true });
        }

        [HttpPost("match-selected")]
        public async Task<IActionResult> MatchSelectedFolders([FromBody] List<string> selectedFolderPaths)
        {
            if (selectedFolderPaths == null || selectedFolderPaths.Count == 0)
                return BadRequest("No folders selected.");

            var foldersToMatch = _settingsService.FoundFolders
                .Where(f => selectedFolderPaths.Contains(f.Folder, StringComparer.OrdinalIgnoreCase))
                .ToList();

            var igdbClient = _settingsService.GetIgdbClient();

            // Run IGDB match in parallel matching original WPF behavior
            await Task.WhenAll(foldersToMatch.Select(async game =>
            {
                await game.MatchIgdbGame(igdbClient);
                if (game.IgdbMatches != null && game.IgdbMatches.Any())
                {
                    game.IgdbSelectedMatch = game.IgdbMatches.First();
                }
            }));

            return Ok(foldersToMatch.Select(ImportFolderDto.FromModel).ToArray());
        }

        [HttpPost("search-single")]
        public async Task<IActionResult> SearchSingleFolderMatch([FromQuery] string folderPath, [FromQuery] string searchText)
        {
            var game = _settingsService.FoundFolders
                .FirstOrDefault(f => string.Equals(f.Folder, folderPath, StringComparison.OrdinalIgnoreCase));

            if (game == null) return NotFound("Target folder not registered.");

            game.MatchName = searchText;
            var igdbClient = _settingsService.GetIgdbClient();
            await game.MatchIgdbGame(igdbClient);

            if (game.IgdbMatches != null && game.IgdbMatches.Any())
            {
                game.IgdbSelectedMatch = game.IgdbMatches.First();
            }

            return Ok(ImportFolderDto.FromModel(game));
        }

        [HttpPost("select-match")]
        public IActionResult SetSelectedMatch([FromQuery] string folderPath, [FromQuery] long matchId)
        {
            var game = _settingsService.FoundFolders
                .FirstOrDefault(f => string.Equals(f.Folder, folderPath, StringComparison.OrdinalIgnoreCase));

            if (game == null) return NotFound("Target folder not registered.");

            var match = game.IgdbMatches?.FirstOrDefault(m => m.Id == matchId);
            if (match != null)
            {
                game.IgdbSelectedMatch = match;
            }

            return Ok(ImportFolderDto.FromModel(game));
        }

        [HttpPost("import-selected")]
        public async Task<IActionResult> ImportSelected([FromBody] List<string> selectedFolderPaths)
        {
            if (selectedFolderPaths == null || selectedFolderPaths.Count == 0)
                return BadRequest("No folders selected.");

            var gamesToImport = _settingsService.FoundFolders
                .Where(f => selectedFolderPaths.Contains(f.Folder, StringComparer.OrdinalIgnoreCase) && f.IgdbSelectedMatch != null)
                .ToList();

            int importedCount = 0;

            foreach (var game in gamesToImport)
            {
                try
                {
                    game.MatchIgdbGame(game.IgdbSelectedMatch);
                    if (!game.DateAdded.HasValue)
                    {
                        game.DateAdded = DateTime.Now;
                    }

                    // Add to static in-memory LocalLibrary and persist to SQLite
                    lock (LocalLibraryService.SyncLock)
                    {
                        if (!LocalLibraryService.LocalLibrary.Any(l => l.Folder == game.Folder))
                        {
                            LocalLibraryService.LocalLibrary.Add(game);
                        }
                    }

                    // Remove from unimported FoundFolders
                    var remaining = _settingsService.FoundFolders.Where(f => f.Folder != game.Folder).ToList();
                    while (_settingsService.FoundFolders.TryTake(out _)) { }
                    foreach (var item in remaining)
                    {
                        _settingsService.FoundFolders.Add(item);
                    }

                    await Task.Run(() => SqliteHelper.InsertOrUpdateLibraryEntry(game));
                    importedCount++;
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Failed to import game: {Folder}", game.Folder);
                }
            }

            return Ok(new { Success = true, ImportedCount = importedCount });
        }
    }
}