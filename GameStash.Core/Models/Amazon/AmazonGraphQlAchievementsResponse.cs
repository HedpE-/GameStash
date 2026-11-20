using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Amazon
{
    internal class AmazonGraphQlAchievementsResponse
    {
        [JsonPropertyName("data")]
        public AmazonGraphQlAchievementsData? Data { get; set; }
    }
}
