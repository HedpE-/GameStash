using GameStash.Core.Extensions;
using IgdbSharp.Enumerators;
using IgdbSharp.Models;
using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace GameStash.Web.GUI.DTO
{
    public class GameCardDto
    {
        public static readonly string GameCardQueryFields = "id, name, cover.*";
        public static readonly string GameCardReleaseDateQueryFields = GameCardQueryFields + ", first_release_date";
        public static readonly string GameCardRatingQueryFields = GameCardQueryFields + ", rating, rating_count";
        public long? Id { get; set; }
        public string Name { get; set; } 
        public string ReleaseDate { get; set; }
        public double Rating { get; set; }
        public int RatingCount { get; set; }

        // This property will capture the output of your extension method perfectly
        public string CoverUrl { get; set; } = string.Empty;
        [JsonIgnore]
        public Game IgdbGame { get; set; }

        public GameCardDto(Game game)
        {
            IgdbGame = game ?? throw new ArgumentNullException(nameof(game));
            Id = game.Id ?? 0;
            Name = game.Name ?? "Unknown Game";
            ReleaseDate = game.FirstReleaseDate?.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture) ?? "TBD";
            Rating = game.Rating ?? 0;
            RatingCount = game.RatingCount ?? 0;

            // CRITICAL BINDING: Executes your C# cross-assembly extension method straight into the JSON payload
            CoverUrl = game.GetCoverUrl(ImageSize.CoverBig);
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