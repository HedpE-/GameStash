using GameStash.Core.Extensions;
using IgdbSharp.Enumerators;
using IgdbSharp.Helpers;
using IgdbSharp.Models;
using System;
using System.Linq;

namespace GameStash.Web.GUI.DTO
{
    public class GameDetaislDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Storyline { get; set; } = string.Empty;
        public string ReleaseDate { get; set; } = string.Empty;
        public string CoverUrl { get; set; } = string.Empty;

        // Brand new analytical metadata layers extracted from the XAML layout fields
        public Company? DeveloperCompany { get; set; }
        public string DeveloperLogoUrl { get; set; } = string.Empty;
        public string Platforms { get; set; } = "No platforms available";
        public string Genres { get; set; } = "Not specified";
        public string Themes { get; set; } = "Not specified";
        public string Perspectives { get; set; } = "Not specified";
        public string Modes { get; set; } = "Not specified";
        public double Rating { get; set; }
        public int RatingCount { get; set; }

        public ScreenshotDto[] Screenshots { get; set; } = Array.Empty<ScreenshotDto>();
        public VideoDto[] Videos { get; set; } = Array.Empty<VideoDto>();
        public GameCardDto[] SimilarGames { get; set; } = Array.Empty<GameCardDto>();

        protected GameDetaislDto() { }
        protected GameDetaislDto(Game game)
        {
            Id = game.Id ?? 0;
            Name = game.Name ?? "Unknown Game";

            var devCompany = game.InvolvedCompanies?.Values?
                .FirstOrDefault(c => c.Developer == true)?.Company?.Value;

            var devLogoImageId = devCompany?.Logo?.Value?.ImageId;

            Summary = game.Summary ?? "No summary available.";
            Storyline = game.Storyline ?? "No storyline registered.";
                ReleaseDate = game.FirstReleaseDate?.ToString("MMMM dd, yyyy", System.Globalization.CultureInfo.InvariantCulture) ?? "TBD";
            CoverUrl = game.GetCoverUrl(ImageSize.CoverBig);
            Rating = game.TotalRating ?? game.Rating ?? 0;
            RatingCount = game.TotalRatingCount ?? game.RatingCount ?? 0;

            // Resolves nested relation graphs with structural string flattening
            DeveloperCompany = devCompany;
            DeveloperLogoUrl = !string.IsNullOrEmpty(devLogoImageId)
                ? ImageHelper.GetImageUrl(devLogoImageId, ImageSize.LogoMed)
                : string.Empty;

            Platforms = game.Platforms?.Values != null
                ? string.Join(", ", game.Platforms.Values.Select(p => p.Name))
                : "No platforms available";

            Genres = game.Genres?.Values != null
                ? string.Join(", ", game.Genres.Values.Select(g => g.Name))
                : "Not specified";

            Themes = game.Themes?.Values != null
                ? string.Join(", ", game.Themes.Values.Select(t => t.Name))
                : "Not specified";
            Perspectives = game.PlayerPerspectives?.Values != null
                ? string.Join(", ", game.PlayerPerspectives.Values.Select(p => p.Name))
                : "Not specified";

            Modes = game.GameModes?.Values != null
                ? string.Join(", ", game.GameModes.Values.Select(m => m.Name))
                : "Not specified";

            if (game.Screenshots?.Values != null)
            {
                Screenshots = game.Screenshots.Values
                    .Where(s => !string.IsNullOrEmpty(s.ImageId))
                    .Select(s => new ScreenshotDto
                    {
                        ImageId = s.ImageId ?? "Untitled Screenshot",
                        MediumUrl = ImageHelper.GetImageUrl(s.ImageId, ImageSize.ScreenshotMed),
                        BigUrl = ImageHelper.GetImageUrl(s.ImageId, ImageSize.ScreenshotMed),
                        HugeUrl = ImageHelper.GetImageUrl(s.ImageId, ImageSize.ScreenshotMed),
                        HugeRetinaUrl = ImageHelper.GetImageUrl(s.ImageId, ImageSize.ScreenshotHuge, true)
                    })
                    .ToArray();
            }

            if (game.Videos?.Values != null)
            {
                Videos = game.Videos.Values
                    .Where(s => !string.IsNullOrEmpty(s.VideoId))
                    .Select(s => new VideoDto
                    {
                        VideoId = s.VideoId,
                        Name = s.Name ?? "Untitled Video"
                    })
                    .ToArray();
            }

            if (game.SimilarGames?.Values != null)
            {
                SimilarGames = game.SimilarGames.Values
                    .Select(sim => GameCardDto.FromModel(sim))
                    .ToArray();
            }
        }

        public static GameDetaislDto FromModel(Game game)
        {
            var devCompany = game.InvolvedCompanies?.Values?
                .FirstOrDefault(c => c.Developer == true)?.Company?.Value;

            var devLogoImageId = devCompany?.Logo?.Value?.ImageId;

            var dto = new GameDetaislDto(game);

            return dto;
        }
    }
}
