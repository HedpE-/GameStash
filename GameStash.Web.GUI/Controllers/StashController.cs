using GameStash.Core.Models;
using GameStash.Web.GUI.DTO;
using GameStash.Web.GUI.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; // Resolves CS1061 Select extension method
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Controllers
{
    [ApiController]
    [Route("api/stash/{type}")]
    public class StashController(IStashService libraryService, IGameDetailsService gameDetailsService, IRootFoldersService rootFoldersService) : ControllerBase
    {
        private readonly IStashService _libraryService = libraryService;
        private readonly IGameDetailsService _gameDetailsService = gameDetailsService;
        private readonly IRootFoldersService _rootFoldersService = rootFoldersService;

        [HttpDelete]
        public async Task<IActionResult> FlushLibrary(string type)
        {
            switch (type.ToLower())
            {
                case "local":
                    // Invoke the cleanup sequence directly on the data layer service
                    await _libraryService.Flush();
                    return Ok(new { Success = true, Message = "PostgreSQL library catalog successfully flushed." });
                default:
                    return BadRequest(new { Message = $"Unsupported library type: {type}" });
            }
        }

        [HttpGet]
        public IActionResult GetStash([FromRoute] string type, [FromQuery] bool refreshDb = false)
        {
            switch (type.ToLower())
            {
                case "local":
                    if (refreshDb)
                    {
                        _ = _libraryService.InitializeAsync();
                    }

                    var localGames = _libraryService.GetLibrary(type);
                    return Ok(localGames.Select(StashGameCardDto.FromModel));

                default:
                    return BadRequest(new { Message = $"Unsupported library type: {type}" });
            }
        }

        [HttpPut]
        public async Task<IActionResult> AddGameToStash([FromRoute] string type, [FromQuery] string folderPath)
        {
            switch (type.ToLower())
            {
                case "local":
                    var game = _rootFoldersService.FoundFolders.FirstOrDefault(f => f.Folder == folderPath);
                    if (game == null)
                        return NotFound(new { Message = $"Game with path '{folderPath} was not found in the Root Folders discovered games." });

                    try
                    {
                        game.MatchIgdbGame(game.IgdbSelectedMatch);
                        if (!game.DateAdded.HasValue)
                        {
                            game.DateAdded = DateTime.Now;
                        }

                        await _libraryService.AddGameToLibrary(game);

                        // Remove from unimported FoundFolders
                        _rootFoldersService.RemoveFoundFolders(_rootFoldersService.FoundFolders.Where(f => f.Folder == game.Folder).Select(f => f.Folder));
                        return Ok(new
                        {
                            Success = true,
                            Id = game.Id,
                            Message = $"Success adding game to {type} Stash"
                        });
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "Failed to import game: {Folder}", game.Folder);
                        return this.BadRequest($"Failed to import game: {game.Folder}\n{ex.Message}");
                    }
                default:
                    return BadRequest(new { Message = $"Unsupported library type: {type}" });
            }
        }

        [HttpGet("{id:long}/details")]
        public async Task<IActionResult> GetLibraryGameDetails(long id, string type)
        {
            switch (type.ToLower())
            {
                case "local":
                    var localGame = await _libraryService.GetByIdAsync(id, type);
                    if (localGame == null)
                        return NotFound(new { Message = $"Game with ID {id} was not found in the {type} library." });

                    localGame.CheckIsFolderValid();

                    // Fetch deep IGDB graph (Summary, Storyline, Developer, Platforms, etc.)
                    if (localGame.Id.HasValue && localGame.Id.Value > 0)
                    {
                        StashGame fullGame = await _gameDetailsService.GetGameDetailsAsync<StashGame>(localGame.Id.Value);
                        if (fullGame != null)
                        {
                            localGame.MatchIgdbGame(fullGame);
                            return Ok(StashGameDetailsDto.FromModel(fullGame));
                        }
                    }

                    return Ok(StashGameDetailsDto.FromModel(localGame));
                // Future cases for other library types can be added here
                default:
                    return BadRequest(new { Message = $"Unsupported library type: {type}" });
            }
        }

        [HttpPut("{id:long}/favorite")]
        public async Task<IActionResult> ToggleFavorite(long id, [FromBody] bool isFavorite, string type)
        {
            switch (type.ToLower())
            {
                case "local":
                    var success = await _libraryService.ToggleFavoriteAsync(id, isFavorite, type);
                    if (!success) return NotFound();
                    return Ok(new { Success = true, IsFavorite = isFavorite });
                // Future cases for other library types can be added here
                default:
                    return BadRequest(new { Message = $"Unsupported library type: {type}" });
            }
        }

        [HttpPut("{id:long}/installed")]
        public async Task<IActionResult> ToggleInstalled(long id, [FromBody] bool isInstalled, string type)
        {
            switch (type.ToLower())
            {
                case "local":
                    var success = await _libraryService.ToggleInstalledAsync(id, isInstalled, type);
                    if (!success) return NotFound();
                    return Ok(new { Success = true, IsInstalled = isInstalled });
                // Future cases for other library types can be added here
                default:
                    return BadRequest(new { Message = $"Unsupported library type: {type}" });
            }
        }

        [HttpPut("{id:long}/sortname")]
        public async Task<IActionResult> UpdateSortName(long id, [FromBody] string sortName, string type)
        {
            switch (type.ToLower())
            {
                case "local":
                    var success = await _libraryService.UpdateSortNameAsync(id, sortName, type);
                    if (!success) return NotFound();
                    return Ok(new { Success = true, SortName = sortName });
                // Future cases for other library types can be added here
                default:
                    return BadRequest(new { Message = $"Unsupported library type: {type}" });
            }
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> RemoveGame(long id, string type)
        {
            switch (type.ToLower())
            {
                case "local":
                    await _libraryService.RemoveGameFromLibrary(id.ToString());
                    return Ok(new { Success = true });
                // Future cases for other library types can be added here
                default:
                    return BadRequest(new { Message = $"Unsupported library type: {type}" });
            }
        }

        [HttpPost("{id:long}/refresh")]
        public async Task<IActionResult> RefreshGame(long id, string type)
        {
            switch (type.ToLower())
            {
                case "local":
                    var game = await _libraryService.RefreshGameAsync(id, type);
                    if (game == null) return NotFound();
                    return Ok(StashGameCardDto.FromModel(game));
                // Future cases for other library types can be added here
                default:
                    return BadRequest(new { Message = $"Unsupported library type: {type}" });
            }
        }

        [HttpPost("{id:long}/open-folder")]
        public async Task<IActionResult> OpenGameFolder(long id, string type)
        {
            switch (type.ToLower())
            {
                case "local":
                    var game = await _libraryService.GetByIdAsync(id, type);
                    if (game == null || string.IsNullOrWhiteSpace(game.Folder))
                    {
                        return BadRequest(new { Message = "Game folder path is invalid or empty." });
                    }

                    try
                    {
                        if (Directory.Exists(game.Folder))
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = game.Folder,
                                UseShellExecute = true,
                                Verb = "open"
                            });
                            return Ok(new { Success = true });
                        }

                        return NotFound(new { Message = "Target folder does not exist on disk." });
                    }
                    catch (Exception ex)
                    {
                        return StatusCode(500, new { Message = "Failed to launch folder explorer.", Details = ex.Message });
                    }
                default:
                    return BadRequest(new { Message = $"Unsupported library type: {type}" });
            }
        }

        [HttpPost("import-selected")]
        public async Task<IActionResult> ImportSelected([FromBody] List<string> selectedFolderPaths)
        {
            if (selectedFolderPaths == null || selectedFolderPaths.Count == 0)
                return BadRequest("No folders selected.");

            var gamesToImport = _rootFoldersService.FoundFolders
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

                    await _libraryService.AddGameToLibrary(game);

                    // Remove from unimported FoundFolders
                    _rootFoldersService.RemoveFoundFolders(_rootFoldersService.FoundFolders.Where(f => f.Folder == game.Folder).Select(f => f.Folder));

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