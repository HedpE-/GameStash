using GameStash.Core.Models;
using GameStash.Web.GUI.DTO;
using GameStash.Web.GUI.Interfaces;
using GameStash.Web.GUI.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq; // Resolves CS1061 Select extension method
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Controllers
{
    [ApiController]
    [Route("api/library/{type}")]
    public class LibraryController(ILibraryService libraryService, IGameDetailsService gameDetailsService) : ControllerBase
    {
        private readonly ILibraryService _libraryService = libraryService;
        private readonly IGameDetailsService _gameDetailsService = gameDetailsService;

        [HttpGet]
        public IActionResult GetLibrary(string type, [FromQuery] bool refreshDb = false)
        {
            switch (type.ToLower())
            {
                case "local":
                    if (refreshDb)
                    {
                        _ = _libraryService.InitializeAsync();
                    }

                    var localGames = _libraryService.GetLocalLibrary(type);
                    return Ok(localGames.Select(LibraryGameCardDto.FromModel));

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
                        Game fullGame = await _gameDetailsService.GetGameDetailsAsync<Game>(localGame.Id.Value);
                        if (fullGame != null)
                        {
                            localGame.MatchIgdbGame(fullGame);
                            return Ok(LibraryGameDetailsDto.FromModel(fullGame));
                        }
                    }

                    return Ok(LibraryGameDetailsDto.FromModel(localGame));
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
                    var success = await _libraryService.RemoveAsync(id, type);
                    if (!success) return NotFound();
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
                    return Ok(LibraryGameCardDto.FromModel(game));
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
    }
}