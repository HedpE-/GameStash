using GameStash.Core.Models;
using GameStash.Core.Services;
using GameStash.Web.GUI.DTO;
using GameStash.Web.GUI.Interfaces;
using IgdbSharp.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Controllers
{
    [ApiController]
    [Route("api/stash/{type}")]
    public class StashController(
        ILocalStashService localStashService,
        IGameDetailsService gameDetailsService,
        IRootFoldersService rootFoldersService,
        IRecommendationService recommendationService,
        ISteamStashService steamStashService,
        IEpicStashService epicStashService,
        IGogStashService gogStashService,
        IAmazonStashService amazonStashService,
        IUnifiedStashService unifiedStashService) : ControllerBase
    {
        private readonly ILocalStashService _localStashService = localStashService;
        private readonly IGameDetailsService _gameDetailsService = gameDetailsService;
        private readonly IRootFoldersService _rootFoldersService = rootFoldersService;
        private readonly IRecommendationService _recommendationService = recommendationService;
        private readonly ISteamStashService _steamStashService = steamStashService;
        private readonly IEpicStashService _epicStashService = epicStashService;
        private readonly IGogStashService _gogStashService = gogStashService;
        private readonly IAmazonStashService _amazonStashService = amazonStashService;
        private readonly IUnifiedStashService _unifiedStashService = unifiedStashService;

        [HttpGet]
        public Task<IActionResult> GetStash([FromRoute] string type, [FromQuery] bool refresh = false) =>
            type.ToLower() switch
            {
                "local" => GetStashCardsAsync(_localStashService, refresh),
                "steam" => GetStashCardsAsync(_steamStashService, refresh),
                "epic" => GetStashCardsAsync(_epicStashService, refresh),
                "gog" => GetStashCardsAsync(_gogStashService, refresh),
                "amazon" => GetStashCardsAsync(_amazonStashService, refresh),
                "unified" => GetUnifiedStashCardsAsync(refresh),
                _ => Task.FromResult<IActionResult>(BadRequest(new { Message = $"Unsupported library type: {type}" }))
            };

        [HttpGet("{id:long}/details")]
        public Task<IActionResult> GetStashGameDetails(long id, [FromRoute] string type) =>
            type.ToLower() switch
            {
                "local" => GetGameDetailsAsync(_localStashService, id, type, g => g.CheckIsFolderValid()),
                "steam" => GetGameDetailsAsync(_steamStashService, id, type),
                "epic" => GetGameDetailsAsync(_epicStashService, id, type),
                "gog" => GetGameDetailsAsync(_gogStashService, id, type),
                "amazon" => GetGameDetailsAsync(_amazonStashService, id, type),
                _ => Task.FromResult<IActionResult>(BadRequest(new { Message = $"Unsupported library type: {type}" }))
            };

        [HttpPut("{id:long}/details")]
        public Task<IActionResult> UpdateStashGameDetails(long id, [FromBody] UpdatePropertyDto request, [FromRoute] string type) =>
            type.ToLower() switch
            {
                "local" => UpdateGameDetailsAsync(_localStashService, id, request),
                "steam" => UpdateGameDetailsAsync(_steamStashService, id, request),
                "epic" => UpdateGameDetailsAsync(_epicStashService, id, request),
                "gog" => UpdateGameDetailsAsync(_gogStashService, id, request),
                "amazon" => UpdateGameDetailsAsync(_amazonStashService, id, request),
                _ => Task.FromResult<IActionResult>(BadRequest(new { Message = $"Unsupported library type: {type}" }))
            };

        // ---------------------------------------------------------
        // GENERIC STASH HELPERS
        // ---------------------------------------------------------

        private async Task<IActionResult> GetStashCardsAsync<TEntity>(IStashServiceBase<TEntity> service, bool refresh) where TEntity : StashGame
        {
            var games = await service.GetStashAsync(refresh);
            return Ok(games.Select(StashGameCardDto.FromModel));
        }

        private async Task<IActionResult> GetGameDetailsAsync<TEntity>(IStashServiceBase<TEntity> service, long id, string type, Action<TEntity>? onBeforeMatch = null) where TEntity : StashGame
        {
            var game = await service.GetByIdAsync(id);
            if (game == null)
                return NotFound(new { Message = $"Game with ID {id} was not found in the {type} library." });

            onBeforeMatch?.Invoke(game);

            // Fetch deep IGDB graph (Summary, Storyline, Developer, Platforms, etc.)
            if (game.Id.HasValue && game.Id.Value > 0)
            {
                var fullGame = await _gameDetailsService.GetGameDetailsAsync<Game>(game.Id.Value);
                if (fullGame != null)
                {
                    game.MatchIgdbGame(fullGame);
                }
            }

            return Ok(StashGameDetailsDto.FromModel(game));
        }

        private async Task<IActionResult> UpdateGameDetailsAsync<TEntity>(IStashServiceBase<TEntity> service, long id, UpdatePropertyDto request) where TEntity : StashGame
        {
            bool success = await service.UpdateGamePropertyAsync(id, request.PropertyName, request.Value);
            return success ? Ok(new { Success = true }) : NotFound();
        }

        private async Task<IActionResult> GetUnifiedStashCardsAsync(bool refresh)
        {
            var games = await _unifiedStashService.GetAllUnifiedGamesAsync(refresh);
            return Ok(games);
        }

        // ---------------------------------------------------------
        // LOCAL-SPECIFIC ENDPOINTS
        // ---------------------------------------------------------

        [HttpPut]
        public async Task<IActionResult> AddGameToStash([FromRoute] string type, [FromQuery] string folderPath)
        {
            if (!type.Equals("local", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { Message = $"Unsupported stash type: {type}" });

            var game = _rootFoldersService.FoundFolders.FirstOrDefault(f => f.Folder == folderPath);
            if (game == null)
                return NotFound(new { Message = $"Game with path '{folderPath}' was not found in the Root Folders discovered games." });

            var existingGame = await _localStashService.GetByIdAsync(game.IgdbSelectedMatch.Id.Value);
            if (existingGame != null)
                return BadRequest(new { Message = $"Game '{game.Name}' already in stash type: {type}" });

            try
            {
                game.MatchIgdbGame(game.IgdbSelectedMatch);
                game.DateAdded ??= DateTime.Now;

                await _localStashService.AddGameToLibrary(game);
                _rootFoldersService.RemoveFoundFolders(_rootFoldersService.FoundFolders.Where(f => f.Folder == game.Folder).Select(f => f.Folder));

                return Ok(new
                {
                    Success = true,
                    Id = game.Id,
                    Message = $"Success adding {game.Name} to {type} Stash"
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to import game: {Folder}", game.Folder);
                return BadRequest(new { Message = $"Failed to import game: {game.Folder}\n{ex.Message}{(ex.InnerException != null ? $"\n{ex.InnerException.Message}" : "")}" });
            }
        }

        [HttpDelete]
        public async Task<IActionResult> FlushStash([FromRoute] string type)
        {
            if (!type.Equals("local", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { Message = $"Unsupported library type: {type}" });

            await _localStashService.Flush();
            return Ok(new { Success = true, Message = "PostgreSQL library catalog successfully flushed." });
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> RemoveGame(long id, [FromRoute] string type)
        {
            if (!type.Equals("local", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { Message = $"Unsupported library type: {type}" });

            await _localStashService.RemoveGameFromStash(id.ToString());
            return Ok(new { Success = true });
        }

        [HttpPost("{id:long}/refresh")]
        public async Task<IActionResult> RefreshGame(long id, [FromRoute] string type)
        {
            if (!type.Equals("local", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { Message = $"Unsupported library type: {type}" });

            var game = await _localStashService.RefreshGameAsync(id);
            return game == null ? NotFound() : Ok(StashGameCardDto.FromModel(game));
        }

        [HttpGet("playlist")]
        public IActionResult GetPlaylist([FromRoute] string type)
        {
            if (!type.Equals("local", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { Message = $"Unsupported library type: {type}" });

            var localGames = _localStashService.GetPlaylist();
            return Ok(localGames.Select(StashGameCardDto.FromModel));
        }

        [HttpPut("playlist/{gameId:int}")]
        public async Task<IActionResult> AddToPlaylist([FromRoute] string type, [FromRoute] int gameId)
        {
            if (!type.Equals("local", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { Message = $"Unsupported library type: {type}" });

            var game = await _localStashService.GetByIdAsync(gameId);
            if (game == null) return NotFound();

            await _localStashService.AddGameToPlaylist(game);
            return Ok(StashGameCardDto.FromModel(game));
        }

        [HttpDelete("playlist/{gameId:int}")]
        public async Task<IActionResult> RemoveFromPlaylist([FromRoute] string type, [FromRoute] int gameId)
        {
            if (!type.Equals("local", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { Message = $"Unsupported library type: {type}" });

            var game = await _localStashService.GetByIdAsync(gameId);
            if (game == null) return NotFound();

            await _localStashService.RemoveGameFromPlaylist(game);
            return Ok(StashGameCardDto.FromModel(game));
        }

        [HttpPost("{id:long}/open-folder")]
        public async Task<IActionResult> OpenGameFolder([FromRoute] long id, [FromRoute] string type)
        {
            if (!type.Equals("local", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { Message = $"Unsupported library type: {type}" });
            var game = await _localStashService.GetByIdAsync(id);
            if (game == null || string.IsNullOrWhiteSpace(game.Folder))
                return BadRequest(new { Message = "Game folder path is invalid or empty." });
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
        }

        [HttpPost("import-selected")]
        public async Task<IActionResult> ImportSelected([FromBody] List<string> selectedFolderPaths, [FromRoute] string type)
        {
            if (!type.Equals("local", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { Message = $"Unsupported library type: {type}" });

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
                    game.DateAdded ??= DateTime.Now;

                    await _localStashService.AddGameToLibrary(game);
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

        [HttpGet("recommendations/playlist")]
        public async Task<IActionResult> GetPlaylistRecommendations([FromRoute] string type, [FromQuery] int limit = 10)
        {
            if (!type.Equals("local", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { Message = $"Unsupported library type: {type}" });

            var recommendations = await _recommendationService.GetPlaylistSuggestionsAsync(type, limit);
            return Ok(recommendations);
        }

        [HttpGet("recommendations/stash")]
        public async Task<IActionResult> GetStashRecommendations([FromRoute] string type, [FromQuery] int limit = 10)
        {
            if (!type.Equals("local", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { Message = $"Unsupported library type: {type}" });

            var recommendations = await _recommendationService.GetStashSuggestionsAsync(type, limit);
            return Ok(recommendations);
        }

    }
}