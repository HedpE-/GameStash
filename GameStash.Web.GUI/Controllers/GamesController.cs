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
    public class GamesController(IGameDetailsService gameDetailsService,
        ISettingsService settingsService,
        ISteamStashService steamStashService,
        IEpicStashService epicStashService,
        IGogStashService gogStashService,
        IAmazonStashService amazonStashService) : ControllerBase
    {
        private readonly IGameDetailsService _gameDetailsService = gameDetailsService;
        private readonly ISettingsService _settingsService = settingsService;
        private readonly ISteamStashService _steamStashService = steamStashService;
        private readonly IEpicStashService _epicStashService = epicStashService;
        private readonly IGogStashService _gogStashService = gogStashService;
        private readonly IAmazonStashService _amazonStashService = amazonStashService;

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

        [HttpGet("{store}/{appId:long}")]
        public async Task<IActionResult> GetGameDetailsBySteamAppId([FromRoute] string store, [FromRoute] long appId)
        {
            if (store != "steam")
                return BadRequest(new { Message = $"Store '{store}' is not supported for this endpoint." });

            var game = await _gameDetailsService.GetGameBySteamAppIdAsync<Game>(appId);

            if (game == null)
            {
                return NotFound(new { Message = $"Could not resolve IGDB metadata for Steam AppID {appId}." });
            }

            return Ok(GameDetailsDto.FromModel(game));
        }

        [HttpGet("{store}/{appId:long}/achievements")]
        public async Task<IActionResult> GetStoreAchievements([FromRoute] string store, [FromRoute] long appId)
        {
            try
            {
                return store.ToLowerInvariant() switch
                {
                    "steam" => await GetSteamAchievementsAsync(appId),
                    "epic" => await GetEpicAchievementsAsync(appId),
                    "gog" => await GetGogAchievementsAsync(appId),
                    "amazon" => await GetAmazonAchievementsAsync(appId),
                    _ => BadRequest(new { Message = $"Achievements are not supported for store '{store}'." })
                };
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Failed to fetch achievements from {store}.", Details = ex.Message });
            }
        }

        private async Task<IActionResult> GetSteamAchievementsAsync(long appId)
        {
            var steamGame = await _steamStashService.GetByIdAsync(appId);
            long targetSteamAppId = steamGame?.ExtId ?? appId;

            if (targetSteamAppId <= 0)
                return NotFound(new { Message = $"Could not resolve a valid Steam AppID for {appId}." });

            var steamClient = _settingsService.GetSteamClient();
            if (steamClient == null)
                return BadRequest(new { Message = "Steam client is not configured." });

            var result = await steamClient.FetchGameAchievementsAsync(targetSteamAppId);
            if (result == null)
                return Ok(new GameAchievementsDto { Store = "steam" });

            return Ok(new GameAchievementsDto
            {
                Store = "steam",
                TotalAchievements = result.TotalAchievements,
                UnlockedAchievements = result.UnlockedAchievements,
                Achievements = result.Achievements.Select(a => new AchievementDto
                {
                    Id = a.Id,
                    Name = a.Name,
                    Description = a.Description,
                    IconUrl = a.UnlockedIconUrl ?? string.Empty,
                    LockedIconUrl = a.LockedIconUrl ?? a.UnlockedIconUrl ?? string.Empty,
                    IsUnlocked = a.Unlocked,
                    UnlockedAt = a.UnlockDate,
                    RarityPercent = a.RarityPercent,
                    IsHidden = a.IsHidden
                }).ToList()
            });
        }

        private async Task<IActionResult> GetEpicAchievementsAsync(long appId)
        {
            var epicGame = await _epicStashService.GetByIdAsync(appId);
            string? sandboxId = epicGame?.Namespace;

            if (string.IsNullOrWhiteSpace(sandboxId))
                return NotFound(new { Message = $"Could not resolve an Epic Namespace/SandboxId for {appId}." });

            var epicClient = _settingsService.GetEpicClient();
            if (epicClient == null)
                return BadRequest(new { Message = "Epic client is not configured." });

            // Ensure _accountId is populated on EpicClient before calling FetchGameAchievementsAsync
            await epicClient.VerifySessionAsync();

            var result = await epicClient.FetchGameAchievementsAsync(sandboxId);
            if (result == null)
                return Ok(new GameAchievementsDto { Store = "epic" });

            return Ok(new GameAchievementsDto
            {
                Store = "epic",
                TotalAchievements = result.TotalAchievements,
                UnlockedAchievements = result.UnlockedAchievements,
                TotalXp = result.TotalXp,
                EarnedXp = result.EarnedXp,
                Achievements = result.Achievements.Select(a => new AchievementDto
                {
                    Id = a.Id,
                    Name = a.Name,
                    Description = a.Description,
                    IconUrl = a.UnlockedIconUrl ?? string.Empty,
                    LockedIconUrl = a.LockedIconUrl ?? a.UnlockedIconUrl ?? string.Empty,
                    IsUnlocked = a.Unlocked,
                    UnlockedAt = a.UnlockDate,
                    RarityPercent = a.RarityPercent,
                    Progress = a.Progress,
                    Xp = a.Xp,
                    IsHidden = a.IsHidden
                }).ToList()
            });
        }

        private async Task<IActionResult> GetGogAchievementsAsync(long appId)
        {
            var gogGame = await _gogStashService.GetByIdAsync(appId);
            long targetGogId = gogGame?.ExtId ?? appId;

            if (targetGogId <= 0)
                return NotFound(new { Message = $"Could not resolve a valid GOG Product ID for {appId}." });

            var gogClient = _settingsService.GetGogClient();
            if (gogClient == null)
                return BadRequest(new { Message = "GOG client is not configured." });

            var result = await gogClient.FetchGameAchievementsAsync(targetGogId);
            if (result == null)
                return Ok(new GameAchievementsDto { Store = "gog" });

            return Ok(new GameAchievementsDto
            {
                Store = "gog",
                TotalAchievements = result.TotalAchievements,
                UnlockedAchievements = result.UnlockedAchievements,
                Achievements = result.Achievements.Select(a => new AchievementDto
                {
                    Id = a.Key,
                    Name = a.Name,
                    Description = a.Description,
                    IconUrl = a.UnlockedIconUrl ?? string.Empty,
                    LockedIconUrl = a.LockedIconUrl ?? a.UnlockedIconUrl ?? string.Empty,
                    IsUnlocked = a.Unlocked,
                    UnlockedAt = a.UnlockDate,
                    RarityPercent = a.RarityPercent,
                    IsHidden = a.IsHidden
                }).ToList()
            });
        }

        private async Task<IActionResult> GetAmazonAchievementsAsync(long appId)
        {
            var amazonGame = await _amazonStashService.GetByIdAsync(appId);
            string? productId = amazonGame?.ExtId;

            if (string.IsNullOrWhiteSpace(productId))
                return NotFound(new { Message = $"Could not resolve an Amazon Product ID for {appId}." });

            var amazonClient = _settingsService.GetAmazonClient();
            if (amazonClient == null)
                return BadRequest(new { Message = "Amazon client is not configured." });

            var result = await amazonClient.FetchGameAchievementsAsync(productId);
            if (result == null)
                return Ok(new GameAchievementsDto { Store = "amazon" });

            return Ok(new GameAchievementsDto
            {
                Store = "amazon",
                TotalAchievements = result.TotalAchievements,
                UnlockedAchievements = result.UnlockedAchievements,
                Achievements = result.Achievements.Select(a => new AchievementDto
                {
                    Id = a.Id,
                    Name = a.Name,
                    Description = a.Description,
                    IconUrl = a.UnlockedIconUrl ?? string.Empty,
                    LockedIconUrl = a.LockedIconUrl ?? a.UnlockedIconUrl ?? string.Empty,
                    IsUnlocked = a.Unlocked,
                    UnlockedAt = a.UnlockDate,
                    RarityPercent = a.RarityPercent,
                    IsHidden = a.IsHidden
                }).ToList()
            });
        }
    }
}