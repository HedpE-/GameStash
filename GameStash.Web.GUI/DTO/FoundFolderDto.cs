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
        //private long? _selectedMatchId;
        public long? SelectedMatchId { get; set; }
        //{
        //    get => _selectedMatchId;
        //    set
        //    {
        //        if (_selectedMatchId != value)
        //        {
        //            _selectedMatchId = value;
        //            if (value.HasValue && value > 0)
        //            {
        //                _game.IgdbSelectedMatch = _game.IgdbMatches.FirstOrDefault(m => m.Id == value);
        //                _game.IsIgdbMatched = _game.IgdbSelectedMatch != null;
        //                _game.IsSelected = _game.IgdbSelectedMatch != null;
        //                IsSelected = _game.IgdbSelectedMatch != null;
        //            }
        //        }
        //    }
        //}
        public List<IgdbMatchOptionDto> IgdbMatches { get; set; } = new();
        private StashGame _game;

        private FoundFolderDto(LocalStashGame game)
        {
            _game = game;
            Folder = game.Folder ?? string.Empty;
            RootFolder = game.RootFolder ?? string.Empty;
            MatchName = game.MatchName ?? game.SortName ?? game.Name ?? string.Empty;
            Type = game.PackageType ?? "PC";
            IsSelected = game.IsSelected || game.IsIgdbMatched;
            IsLoadingIgdbMatches = game.IsLoadingIgdbMatches;
            SelectedMatchId = game.IgdbSelectedMatch?.Id;
            IgdbMatches = (game.IgdbMatches ?? Enumerable.Empty<Game>())
                .Where(m => m.Id.HasValue)
                .Select(m => IgdbMatchOptionDto.FromModel(m))
                .ToList();
        }

        public static FoundFolderDto FromModel(LocalStashGame game)
        {
            return new FoundFolderDto(game);
        }
    }
}