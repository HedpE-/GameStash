using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Amazon
{
    internal class AmazonGraphQlAchievementsData
    {
        [JsonPropertyName("gameAchievements")]
        public AmazonGameAchievementsPayload? GameAchievements { get; set; }
    }
}
