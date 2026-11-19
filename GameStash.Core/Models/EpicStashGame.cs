namespace GameStash.Core.Models
{
    public class EpicStashGame : StashGame
    {
        public string? Uid { get; set; }
        public string? CatalogItemId { get; set; }
        public string? Namespace { get; set; }
        public string? AppName { get; set; }
        public string? FallbackCoverUrl { get; set; }
        public int PlaytimeMinutes { get; set; } = 0;
    }
}
