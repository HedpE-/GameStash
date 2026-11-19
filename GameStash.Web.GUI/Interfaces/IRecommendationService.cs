using System.Collections.Generic;
using System.Threading.Tasks;
using GameStash.Core.Models;
using GameStash.Web.GUI.DTO;

namespace GameStash.Core.Services
{
    public interface IRecommendationService
    {
        /// <summary>
        /// Recommends unplayed stash games matching the profile of the current Playlist queue.
        /// </summary>
        Task<List<GameRecommendationDto>> GetPlaylistSuggestionsAsync(string libraryType, int limit = 10);

        /// <summary>
        /// Recommends backlog games matching the user's all-time favorites, currently playing, and highest rated games.
        /// </summary>
        Task<List<GameRecommendationDto>> GetStashSuggestionsAsync(string libraryType, int limit = 10);

        /// <summary>
        /// Core mathematical engine: compares candidate games against a set of seed reference games.
        /// </summary>
        List<GameRecommendationDto> CalculateRecommendations(
            IEnumerable<LocalStashGame> seedGames,
            IEnumerable<LocalStashGame> candidatePool,
            int limit = 10);
    }
}