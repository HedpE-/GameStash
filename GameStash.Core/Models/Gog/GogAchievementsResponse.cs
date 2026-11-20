using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Gog
{
    // ---------------------------------------------------------
    // INTERNAL GOG GAMEPLAY API WRAPPERS
    // ---------------------------------------------------------

    internal class GogAchievementsResponse
    {
        [JsonPropertyName("total_count")]
        public int TotalCount { get; set; }

        [JsonPropertyName("items")]
        public List<GogAchievementItem> Items { get; set; } = new();
    }
}
