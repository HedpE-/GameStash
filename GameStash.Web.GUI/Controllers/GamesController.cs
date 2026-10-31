using GameStash.Web.GUI.DTO;
using GameStash.Web.GUI.Interfaces;
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

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetGameDetails(long id)
        {
            var game = await _gameDetailsService.GetGameDetailsAsync<Game>(id);

            if (game == null)
            {
                return NotFound(new { Message = $"Game record with ID {id} could not be resolved." });
            }
            return Ok(GameDetaislDto.FromModel(game));
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchGames([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { Message = "Search query cannot be empty." });
            }

            try
            {
                var igdbClient = _settingsService.GetIgdbClient();
                // Query IGDB matching WPF's BrowseGamesViewModel
                var queryString = $"fields {GameSearchResultDto.GameSearchQueryFields}; search \"{query}\"; limit 100;";
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
    }
}