using GameStash.Core.Models;
using IgdbSharp.Models;
using System.Collections.Generic;
using System.Linq;

namespace GameStash.Web.GUI.DTO
{

    public class FoundFolderDto
    {
        public string Folder { get; set; } = string.Empty;
        public string RootFolder { get; set; } = string.Empty;
        public string RelativeFolderPath { get => Folder?.Replace(RootFolder ?? "", "", System.StringComparison.CurrentCultureIgnoreCase); }
        public string MatchName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public bool IsLoadingIgdbMatches { get; set; }
        public long? SelectedMatchId { get; set; }
        public List<IgdbMatchOptionDto> IgdbMatches { get; set; } = new();

        public static FoundFolderDto FromModel(StashGame game)
        {
            return new FoundFolderDto
            {
                Folder = game.Folder ?? string.Empty,
                RootFolder = game.RootFolder ?? string.Empty,
                MatchName = game.MatchName ?? game.SortName ?? game.Name ?? string.Empty,
                Type = game.PackageType ?? "PC",
                IsSelected = game.IsSelected,
                IsLoadingIgdbMatches = game.IsLoadingIgdbMatches,
                SelectedMatchId = game.IgdbSelectedMatch?.Id,
                IgdbMatches = (game.IgdbMatches ?? Enumerable.Empty<Game>())
                    .Where(m => m.Id.HasValue)
                    .Select(m => IgdbMatchOptionDto.FromModel(m))
                    .ToList()
            };
        }
    }
}