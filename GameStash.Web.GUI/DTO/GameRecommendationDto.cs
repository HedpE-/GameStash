using GameStash.Core.Models;
using System.Collections.Generic;

namespace GameStash.Web.GUI.DTO
{
    public class GameRecommendationDto
    {
        public StashGameCardDto Game { get; set; }

        /// <summary>
        /// Normalized affinity percentage (e.g., 87.5%)
        /// </summary>
        public double MatchPercentage { get; set; }

        /// <summary>
        /// Raw cosine score with domain boosts applied
        /// </summary>
        public double MatchScore { get; set; }

        /// <summary>
        /// Key matching tags that caused this recommendation (e.g., "Developer: FromSoftware", "Theme: Dark Fantasy")
        /// </summary>
        public List<string> MatchReasons { get; set; } = new();
    }
}