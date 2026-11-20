using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Steam
{
    internal class SteamSchemaAchievement
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("hidden")]
        public int Hidden { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("icon")]
        public string? Icon { get; set; }

        [JsonPropertyName("icongray")]
        public string? IconGray { get; set; }
    }
}
