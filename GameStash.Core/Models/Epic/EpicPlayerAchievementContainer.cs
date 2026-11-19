using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Epic
{
    internal class EpicPlayerAchievementContainer
    {
        [JsonPropertyName("playerAchievementGameRecordsBySandboxIds")]
        public List<EpicSandboxAchievementRecord>? Records { get; set; }
    }
}
