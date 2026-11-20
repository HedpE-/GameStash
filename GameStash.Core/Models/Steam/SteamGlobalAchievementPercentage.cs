using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Steam
{
    internal class SteamGlobalAchievementPercentage
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        // Steam occasionally returns "percent" as a JSON string or number depending on the endpoint version
        [JsonPropertyName("percent")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double Percent { get; set; }
    }
}
