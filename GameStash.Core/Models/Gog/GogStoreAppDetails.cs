using System.Collections.Generic;

namespace GameStash.Core.Models.Gog
{
    public class GogStoreAppDetails
    {
        public long GameId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public string DetailedDescription { get; set; } = string.Empty;
        public string CoverUrl { get; set; } = string.Empty;
        public string ReleaseDateText { get; set; } = string.Empty;
        public List<string> Developers { get; set; } = new();
        public List<string> Publishers { get; set; } = new();
        public List<string> Genres { get; set; } = new();
        public List<string> Platforms { get; set; } = new();
        public List<string> Screenshots { get; set; } = new();
    }
}
