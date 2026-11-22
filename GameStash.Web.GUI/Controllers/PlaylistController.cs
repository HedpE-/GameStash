using System.Collections.Generic;
using System.Threading.Tasks;
using GameStash.Core.Models;
using GameStash.Core.Services;
using GameStash.Web.GUI.DTO;
using GameStash.Web.GUI.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GameStash.Web.GUI.Controllers
{
    [ApiController]
    [Route("api/playlist")]
    public class PlaylistController(
        ILocalStashService localStashService,
        ISteamStashService steamStashService,
        IEpicStashService epicStashService,
        IGogStashService gogStashService,
        IAmazonStashService amazonStashService,
        IRecommendationService recommendationService) : ControllerBase
    {
        private readonly ILocalStashService _localStashService = localStashService;
        private readonly ISteamStashService _steamStashService = steamStashService;
        private readonly IEpicStashService _epicStashService = epicStashService;
        private readonly IGogStashService _gogStashService = gogStashService;
        private readonly IAmazonStashService _amazonStashService = amazonStashService;
        private readonly IRecommendationService _recommendationService = recommendationService;

        [HttpGet]
        public IActionResult GetPlaylist()
        {
            var combined = new List<StashGameCardDto>();

            void AppendFrom<TEntity>(IStashServiceBase<TEntity> service, string sourceName) where TEntity : StashGame
            {
                foreach (var g in service.GetPlaylistItems())
                {
                    var dto = StashGameCardDto.FromModel(g);
                    dto.ExternalSystem = sourceName;
                    combined.Add(dto);
                }
            }

            AppendFrom(_localStashService, "Local");
            AppendFrom(_steamStashService, "Steam");
            AppendFrom(_epicStashService, "Epic");
            AppendFrom(_gogStashService, "GOG");
            AppendFrom(_amazonStashService, "Amazon");

            return Ok(combined);
        }

        [HttpPut("{type}/{gameId:long}")]
        public Task<IActionResult> AddToPlaylist([FromRoute] string type, [FromRoute] long gameId) =>
            type.ToLowerInvariant() switch
            {
                "local" => TogglePlaylistMembershipAsync(_localStashService, gameId, true, "Local"),
                "steam" => TogglePlaylistMembershipAsync(_steamStashService, gameId, true, "Steam"),
                "epic" => TogglePlaylistMembershipAsync(_epicStashService, gameId, true, "Epic"),
                "gog" => TogglePlaylistMembershipAsync(_gogStashService, gameId, true, "GOG"),
                "amazon" => TogglePlaylistMembershipAsync(_amazonStashService, gameId, true, "Amazon"),
                _ => Task.FromResult<IActionResult>(BadRequest(new { Message = $"Playlist is not supported for stash type: {type}" }))
            };

        [HttpDelete("{type}/{gameId:long}")]
        public Task<IActionResult> RemoveFromPlaylist([FromRoute] string type, [FromRoute] long gameId) =>
            type.ToLowerInvariant() switch
            {
                "local" => TogglePlaylistMembershipAsync(_localStashService, gameId, false, "Local"),
                "steam" => TogglePlaylistMembershipAsync(_steamStashService, gameId, false, "Steam"),
                "epic" => TogglePlaylistMembershipAsync(_epicStashService, gameId, false, "Epic"),
                "gog" => TogglePlaylistMembershipAsync(_gogStashService, gameId, false, "GOG"),
                "amazon" => TogglePlaylistMembershipAsync(_amazonStashService, gameId, false, "Amazon"),
                _ => Task.FromResult<IActionResult>(BadRequest(new { Message = $"Playlist is not supported for stash type: {type}" }))
            };

        [HttpGet("recommendations/playlist")]
        public async Task<IActionResult> GetPlaylistRecommendations([FromQuery] int limit = 10)
        {
            var recommendations = await _recommendationService.GetPlaylistSuggestionsAsync(limit);
            return Ok(recommendations);
        }

        [HttpGet("recommendations/stash")]
        public async Task<IActionResult> GetStashRecommendations([FromQuery] int limit = 10)
        {
            var recommendations = await _recommendationService.GetStashSuggestionsAsync(limit);
            return Ok(recommendations);
        }

        private async Task<IActionResult> TogglePlaylistMembershipAsync<TEntity>(
            IStashServiceBase<TEntity> service, long gameId, bool add, string sourceName) where TEntity : StashGame
        {
            var updated = await service.UpdateGamePropertyAsync(gameId, nameof(StashGame.IsInPlaylist), add);
            if (!updated) return NotFound();

            var game = await service.GetByIdAsync(gameId);
            if (game == null) return NotFound();

            var dto = StashGameCardDto.FromModel(game);
            dto.ExternalSystem = sourceName;
            return Ok(dto);
        }

    }
}
