using GameStash.Web.GUI.DTO;
using IgdbSharp.Models;
using System;

namespace GameStash.Web.GUI.DTO
{
    public class HomeDashboardResponseDto
    {
        public GameCardDto[] LatestReleases { get; set; } = Array.Empty<GameCardDto>();
        public GameCardDto[] Top10RatedGames { get; set; } = Array.Empty<GameCardDto>();
        public GameCardDto[] MostAnticipated { get; set; } = Array.Empty<GameCardDto>();
        public DateTime? LastUpdated;
    }
}
