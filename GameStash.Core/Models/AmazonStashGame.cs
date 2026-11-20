namespace GameStash.Core.Models
{
    public class AmazonStashGame : StashGame
    {
        public string? ExtId { get; set; }
        public string? FallbackCoverUrl { get; set; }
    }
}
