namespace GameStash.Web.GUI.DTO
{
    public class VideoDto
    {
        private static readonly string VideThumbnailUrlTemplate = "https://i.ytimg.com/vi/_videoId_/hqdefault.jpg";
        public const string YouTubeEmbedUrlTemplate = "https://www.youtube.com/embed/{0}?autoplay=1&rel=0";
        public const string YouTubeWatchUrlTemplate = "https://www.youtube.com/watch?v={0}";
        public const string YouTubeThumbnailUrlTemplate = "https://img.youtube.com/vi/{0}/hqdefault.jpg";
        public string VideoId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public string ThumbnailUrl => $"{YouTubeThumbnailUrlTemplate.Replace("{0}", VideoId)}";
        public string YouTubeEmbedUrl => $"{YouTubeEmbedUrlTemplate.Replace("{0}", VideoId)}";
        public string YouTubeWatchUrl => $"{YouTubeWatchUrlTemplate.Replace("{0}", VideoId)}";

    }
}