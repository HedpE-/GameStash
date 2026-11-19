using GameStash.Core.Models;
using GameStash.Web.GUI.DTO;
using GameStash.Web.GUI.Interfaces;
using GameStash.Web.GUI.Services;
using IgdbSharp;
using IgdbSharp.Enumerators;
using IgdbSharp.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Controllers
{
    [ApiController]
    [Route("api/games")]
    public class GamesController(IGameDetailsService gameDetailsService, ISettingsService settingsService) : ControllerBase
    {
        private readonly IGameDetailsService _gameDetailsService = gameDetailsService;
        private readonly ISettingsService _settingsService = settingsService;

        [HttpPost("")]
        public async Task<IActionResult> GetGamesCards([FromBody] long[] ids)
        {
            var games = await _gameDetailsService.GetGamesAsync<Game>(ids);

            if (games == null)
            {
                return NotFound(new { Message = $"Game record with IDs {string.Join(", ", ids)} could not be resolved." });
            }
            return Ok(games.Select(g => GameCardDto.FromModel(g)));
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetGameDetails([FromRoute] long id)
        {
            var game = await _gameDetailsService.GetGameDetailsAsync<Game>(id);

            if (game == null)
            {
                return NotFound(new { Message = $"Game record with ID {id} could not be resolved." });
            }
            return Ok(GameDetailsDto.FromModel(game));
        }

        [HttpGet("franchise/{franchiseId:long}")]
        public async Task<IActionResult> GetSameFranchiseGames([FromRoute] long franchiseId)
        {
            var games = await _gameDetailsService.GetSameFranchiseGames(franchiseId);

            if (games == null)
            {
                return Ok(Enumerable.Empty<GameCardDto>());
            }
            return Ok(games.Select(g => GameCardDto.FromModel(g)));
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchGames([FromQuery] string query, [FromQuery] string? fields = null, [FromQuery] int? limitResults = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { Message = "Search query cannot be empty." });
            }

            try
            {
                var igdbClient = await _settingsService.GetIgdbClient();

                // Use the custom provided fields parameter if available; otherwise, fall back to the default fields
                string selectedFields = !string.IsNullOrWhiteSpace(fields)
                    ? fields.Trim()
                    : StashGame.GameSearchQueryFields;

                var queryString = $"fields {selectedFields}; search \"{query}\";";

                if (limitResults != null)
                    queryString += $"limit {limitResults};";

                var games = await igdbClient.QueryAsync<Game>(Endpoints.Games, query: queryString, requestPriority: RequestPriority.High);

                var dtos = (games ?? Array.Empty<Game>())
                    .Select(GameSearchResultDto.FromModel)
                    .ToArray();

                return Ok(dtos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to query games repository.", Details = ex.Message });
            }
        }

        [HttpGet("steam/{appId:long}")]
        public async Task<IActionResult> GetGameDetailsBySteamAppId([FromRoute] long appId)
        {
            var game = await _gameDetailsService.GetGameBySteamAppIdAsync<Game>(appId);

            if (game == null)
            {
                return NotFound(new { Message = $"Could not resolve IGDB metadata for Steam AppID {appId}." });
            }

            return Ok(GameDetailsDto.FromModel(game));
        }
    }
}