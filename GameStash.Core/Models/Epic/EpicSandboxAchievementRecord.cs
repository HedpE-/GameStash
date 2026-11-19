using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Epic
{
    internal class EpicSandboxAchievementRecord
    {
        [JsonPropertyName("sandboxId")]
        public string? SandboxId { get; set; }

        [JsonPropertyName("totalAchievements")]
        public int TotalAchievements { get; set; }

        [JsonPropertyName("totalXP")]
        public int TotalXp { get; set; }

        [JsonPropertyName("playerAwards")]
        public List<EpicPlayerAward>? PlayerAwards { get; set; }

        [JsonPropertyName("playerAchievements")]
        public List<EpicPlayerAchievementNode>? PlayerAchievements { get; set; }
    }
}
