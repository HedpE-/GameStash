namespace GameStash.Core.Models
{
    public class GogStashGame : StashGame
    {
        public long? ExtId { get; set; }
        public int PlaytimeMinutes { get; set; } = 0;
        public string? FallbackCoverUrl { get; set; }
    }
}
