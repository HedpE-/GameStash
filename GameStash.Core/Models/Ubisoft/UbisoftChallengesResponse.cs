using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Ubisoft
{
    internal class UbisoftChallengesResponse
    {
        [JsonPropertyName("challenges")]
        public List<UbisoftRawChallenge> Challenges { get; set; } = new();
    }
}
