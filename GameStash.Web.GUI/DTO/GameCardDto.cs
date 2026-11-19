using GameStash.Core.Extensions;
using GameStash.Core.Models.Steam;
using IgdbSharp.Enumerators;
using IgdbSharp.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;

namespace GameStash.Web.GUI.DTO
{
    public class GameCardDto
    {
        public long? Id { get; set; }
        public string Name { get; set; }
        public string ReleaseDate { get; set; }
        public double Rating { get; set; }
        public int RatingCount { get; set; }
        public int PlaytimeMinutes { get; set; }

        public string CoverUrl { get; set; } = string.Empty;
        public IEnumerable<Platform> Platforms { get; set; }

        [JsonIgnore]
        public Game IgdbGame { get; set; }

        protected GameCardDto() { }

        protected GameCardDto(Game game)
        {
            IgdbGame = game ?? throw new ArgumentNullException(nameof(game));
            Id = game.Id ?? 0;
            Name = game.Name ?? "Unknown Game";
            ReleaseDate = game.FirstReleaseDate?.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture) ?? "TBD";
            Rating = game.Rating ?? 0;
            RatingCount = game.RatingCount ?? 0;

            CoverUrl = game.GetCoverUrl(ImageSize.CoverBig);
            Platforms = game.Platforms?.Values;
        }

        public static GameCardDto FromModel(Game game)
        {
            return new GameCardDto(game);
        }

        public void UpdateFromModel(Game game)
        {
            var newModel = FromModel(game ?? throw new ArgumentNullException(nameof(game)));
            IgdbGame = newModel.IgdbGame;
            Id = newModel.Id;
            Name = newModel.Name;
            ReleaseDate = newModel.ReleaseDate;
            Rating = newModel.Rating;
            RatingCount = newModel.RatingCount;
        }
    }
}