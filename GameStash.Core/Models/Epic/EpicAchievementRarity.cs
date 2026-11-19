using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Epic
{
    internal class EpicAchievementRarity
    {
        [JsonPropertyName("percent")]
        public double Percent { get; set; }
    }
}
