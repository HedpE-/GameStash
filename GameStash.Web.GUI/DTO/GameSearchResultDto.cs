using GameStash.Core.Extensions;
using IgdbSharp.Enumerators;
using IgdbSharp.Helpers;
using IgdbSharp.Models;
using System;
using System.Linq;

namespace GameStash.Web.GUI.DTO
{
    public class GameSearchResultDto
    {
        public static readonly string GameSearchQueryFields = "id, name, cover.*, first_release_date, rating, rating_count, platforms.*,platforms.platform_logo.*, game_type," +
            " involved_companies.*, involved_companies.company.*,involved_companies.company.logo.*,involved_companies.company.websites.*,involved_companies.game.*";
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CoverUrl { get; set; } = string.Empty;
        public string ReleaseDate { get; set; } = "No release date available";
        public DateTimeOffset? RawReleaseDate { get; set; }
        public string DeveloperName { get; set; } = "No developer company available";
        public string DeveloperLogoUrl { get; set; } = string.Empty;
        public string Platforms { get; set; } = "No platforms available";
        public double Rating { get; set; }
        public string GameType { get; set; } = "Main Game";

        public static GameSearchResultDto FromModel(Game game)
        {
            var devCompany = game.InvolvedCompanies?.Values?
                .FirstOrDefault(c => c.Developer == true)?.Company?.Value;

            var devLogoImageId = devCompany?.Logo?.Value?.ImageId;

            return new GameSearchResultDto
            {
                Id = game.Id ?? 0,
                Name = game.Name ?? "Unknown Title",
                CoverUrl = game.GetCoverUrl(ImageSize.CoverSmall) ?? "/css/placeholder.png",
                RawReleaseDate = game.FirstReleaseDate,
                ReleaseDate = game.FirstReleaseDate?.ToString("dd/MM/yyyy") ?? "No release date available",
                DeveloperName = devCompany?.Name ?? "No developer company available",
                DeveloperLogoUrl = !string.IsNullOrEmpty(devLogoImageId)
                    ? ImageHelper.GetImageUrl(devLogoImageId, ImageSize.LogoMed)
                    : string.Empty,
                Platforms = game.Platforms?.Values != null
                    ? string.Join(", ", game.Platforms.Values.Select(p => p.Name))
                    : "No platforms available",
                Rating = game.TotalRating ?? game.Rating ?? 0,
                GameType = FormatGameType(game.GameType?.ToString())
            };
        }

        private static string FormatGameType(string? rawGameType)
        {
            return rawGameType switch
            {
                "MainGame" => "Main Game",
                "DlcAddon" => "DLC / Addon",
                "Expansion" => "Expansion",
                "Bundle" => "Bundle",
                "StandaloneExpansion" => "Standalone",
                "Mod" => "Mod",
                "Episode" => "Episode",
                "Season" => "Season",
                "Remake" => "Remake",
                "Remaster" => "Remaster",
                "ExpandedGame" => "Expanded",
                "Port" => "Port",
                "Fork" => "Fork",
                "Pack" => "Pack",
                "Update" => "Update",
                _ => rawGameType ?? "Game"
            };
        }
    }
}