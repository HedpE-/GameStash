using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Amazon
{
    public class AmazonUserProfile
    {
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
    }
}
