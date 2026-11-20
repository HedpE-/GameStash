using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Ubisoft
{
    internal class UbisoftRawChallenge
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("isCompleted")]
        public bool IsCompleted { get; set; }

        [JsonPropertyName("completionDate")]
        public DateTimeOffset? CompletionDate { get; set; }

        [JsonPropertyName("xpReward")]
        public int XpReward { get; set; }

        [JsonPropertyName("completionPercentage")]
        public double CompletionPercentage { get; set; }

        [JsonPropertyName("imageUrl")]
        public string? ImageUrl { get; set; }

        [JsonPropertyName("isSecret")]
        public bool IsSecret { get; set; }
    }
}
