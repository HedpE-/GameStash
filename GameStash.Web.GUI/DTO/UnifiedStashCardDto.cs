using GameStash.Core.Models;
using System.Collections.Generic;

namespace GameStash.Web.GUI.DTO
{
    public class UnifiedStashCardDto : StashGameCardDto
    {
        public List<string> OwnedSources { get; set; } = new();
        public bool IsPlayed { get; set; }
        public bool IsPlaying { get; set; }
        public bool IsFinished { get; set; }
        public bool PlayedToEnd { get; set; }

        public UnifiedStashCardDto(StashGame game, string initialSource) : base(game)
        {
            ExternalSystem = initialSource;
            OwnedSources = new List<string> { initialSource };
            IsPlayed = game.IsPlayed;
            IsPlaying = game.IsPlaying;
            IsFinished = game.IsFinished;
            PlayedToEnd = game.PlayedToEnd;
        }
    }
}
