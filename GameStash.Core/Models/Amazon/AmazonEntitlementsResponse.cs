using System.Text.Json.Serialization;

namespace GameStash.Core.Models.Amazon
{
    public class AmazonEntitlementsResponse
    {
        [JsonPropertyName("entitlements")]
        public List<AmazonEntitlement> Entitlements { get; set; } = new();

        [JsonPropertyName("nextToken")]
        public string? NextToken { get; set; }
    }
}
