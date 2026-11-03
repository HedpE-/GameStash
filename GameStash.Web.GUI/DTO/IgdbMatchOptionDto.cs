using IgdbSharp.Models;
using System;
using System.Linq;

namespace GameStash.Web.GUI.DTO
{
    public class IgdbMatchOptionDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Year { get; set; } = "TBD";
        public string Platforms { get; set; } = string.Empty;
        public string ReleaseDate { get; set; } = string.Empty;
        public double? Rating { get; set; } = null;
        private string _dateFormat = "dd MMM, yyyy";
        private readonly DateTimeOffset? firstReleaseDate;

        private IgdbMatchOptionDto(Game game, string dateFormat = null)
        {
            Id = game.Id ?? 0;
            Name = game.Name ?? "Unknown";
            Platforms = game.Platforms?.Values != null
                ? string.Join(", ", game.Platforms.Values.Select(p => p.Name))
                : string.Empty;
            Rating = game.Rating;

            firstReleaseDate = game.FirstReleaseDate;
            Year = firstReleaseDate?.ToString("yyyy") ?? "TBD";

            ChangeDateFormat(dateFormat);
        }

        public static IgdbMatchOptionDto FromModel(Game game, string dateFormat = null)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));

            return new IgdbMatchOptionDto(game, dateFormat);
        }

        public void ChangeDateFormat(string newFormat)
        {
            if (!string.IsNullOrEmpty(newFormat))
                _dateFormat = newFormat;
            ReleaseDate = firstReleaseDate?.ToString(_dateFormat) ?? "TBD";
        }
    }
}