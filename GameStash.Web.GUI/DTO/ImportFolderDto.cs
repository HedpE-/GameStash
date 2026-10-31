using GameStash.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace GameStash.Web.GUI.DTO
{

    public class ImportFolderDto
    {
        public string Folder { get; set; } = string.Empty;
        public string MatchName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public bool IsLoadingIgdbMatches { get; set; }
        public long? SelectedMatchId { get; set; }
        public List<IgdbMatchOptionDto> IgdbMatches { get; set; } = new();

        public static ImportFolderDto FromModel(Game game)
        {
            return new ImportFolderDto
            {
                Folder = game.Folder ?? string.Empty,
                MatchName = !string.IsNullOrEmpty(game.MatchName) ? game.MatchName : game.Name ?? string.Empty,
                Type = game.Type ?? "PC",
                IsSelected = game.IsSelected,
                IsLoadingIgdbMatches = game.IsLoadingIgdbMatches,
                SelectedMatchId = game.IgdbSelectedMatch?.Id,
                IgdbMatches = (game.IgdbMatches ?? Enumerable.Empty<IgdbSharp.Models.Game>())
                    .Where(m => m.Id.HasValue)
                    .Select(m => new IgdbMatchOptionDto
                    {
                        Id = m.Id!.Value,
                        Name = m.Name ?? "Unknown",
                        Year = m.FirstReleaseDate?.ToString("yyyy") ?? "TBD",
                        Platforms = m.Platforms?.Values != null
                            ? string.Join(", ", m.Platforms.Values.Select(p => p.Name))
                            : string.Empty
                    })
                    .ToList()
            };
        }
    }
}