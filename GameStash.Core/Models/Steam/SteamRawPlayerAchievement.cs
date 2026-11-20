using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Steam
{
    internal class SteamRawPlayerAchievement
    {
        [JsonPropertyName("apiname")]
        public string ApiName { get; set; } = string.Empty;

        [JsonPropertyName("achieved")]
        public int Achieved { get; set; }

        [JsonPropertyName("unlocktime")]
        public long UnlockTime { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
}
