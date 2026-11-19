using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Epic
{
    // ---------------------------------------------------------
    // INTERNAL GRAPHQL RESPONSE WRAPPERS
    // ---------------------------------------------------------

    internal class EpicGraphQlAchievementResponse
    {
        [JsonPropertyName("data")]
        public EpicGraphQlAchievementData? Data { get; set; }
    }
}
