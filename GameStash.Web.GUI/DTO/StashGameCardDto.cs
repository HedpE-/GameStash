using GameStash.Core.Extensions;
using GameStash.Core.Models;
using GameStash.Web.GUI.Extensions;
using GameStash.Web.GUI.Helpers;
using IgdbSharp.Enumerators;
using System;
using System.IO;
using System.Net.Http;

namespace GameStash.Web.GUI.DTO
{
    public class StashGameCardDto : GameCardDto
    {
        private static readonly HttpClient _headClient = new();

        public string SortName { get; set; } = string.Empty;
        public string ReleaseDateFormatted { get; set; } = string.Empty;
        public string ReleaseYear { get; set; } = string.Empty;
        public DateTimeOffset? FirstReleaseDate { get; set; }
        public DateTime? DateAdded { get; set; }
        public string DateAddedFormatted { get; set; } = string.Empty;
        public long FolderDiskSpaceBytes { get; set; }
        public string FolderSizeFormatted { get; set; } = string.Empty;
        public bool IsFavorite { get; set; }
        public bool IsInstalled { get; set; }
        public long? ExternalAppId { get; set; }
        public string? ExternalSystem { get; set; }
        public bool IsFolderValid { get; set; }
        public bool IsInPlaylist { get; set; }
        public string Folder { get; set; } = string.Empty;

        protected StashGameCardDto(StashGame game) : base(game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));

            SortName = !string.IsNullOrEmpty(game.SortName) ? game.SortName : game.Name ?? string.Empty;
            FirstReleaseDate = game.FirstReleaseDate;
            ReleaseDateFormatted = game.FirstReleaseDate?.ToString("dd/MM/yyyy") ?? "No date";
            ReleaseYear = game.FirstReleaseDate?.ToString("yyyy") ?? "TBD";
            IsFavorite = game.IsFavorite;
            IsInstalled = game.IsInstalled;
            IsInPlaylist = game.IsInPlaylist;

            // Pattern matching: extract local disk properties only for LocalStashGame
            if (game is LocalStashGame local)
            {
                DateAdded = local.DateAdded;
                DateAddedFormatted = local.DateAdded?.ToString("dd/MM/yyyy") ?? "Unknown";
                Folder = local.Folder ?? string.Empty;
                IsFolderValid = local.IsFolderValid;

                if (!string.IsNullOrWhiteSpace(local.Folder) && Directory.Exists(local.Folder))
                {
                    try
                    {
                        FolderDiskSpaceBytes = new DirectoryInfo(local.Folder).GetDirectorySize(false);
                        FolderSizeFormatted = Utils.FormatBytes(FolderDiskSpaceBytes);
                    }
                    catch
                    {
                        FolderDiskSpaceBytes = 0;
                        FolderSizeFormatted = string.Empty;
                    }
                }
                else
                {
                    FolderDiskSpaceBytes = local.FolderDiskSpaceBytes;
                    FolderSizeFormatted = FolderDiskSpaceBytes > 0 ? Utils.FormatBytes(FolderDiskSpaceBytes) : string.Empty;
                }
            }
            // Pattern matching: extract Steam-specific properties for SteamStashGame
            else if (game is SteamStashGame steam)
            {
                ExternalAppId = steam.ExtId;
                ExternalSystem = "Steam";
                PlaytimeMinutes = steam.PlaytimeMinutes;

                // Fallback cover via Steam CDN if IGDB cover is missing
                if (game.Cover?.Value?.ImageId == null && steam.ExtId.HasValue)
                {
                    var cdnUrl = $"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{steam.ExtId.Value}/library_600x900.jpg";
                    try
                    {
                        var request = new HttpRequestMessage(HttpMethod.Head, cdnUrl);
                        var response = _headClient.Send(request, HttpCompletionOption.ResponseHeadersRead); // Thread-safe!

                        if (response.IsSuccessStatusCode)
                            CoverUrl = cdnUrl;
                    }
                    catch { }
                }
            }
            // Pattern matching: extract Epic-specific properties for EpicStashGame
            else if (game is EpicStashGame epic)
            {
                ExternalSystem = "Epic";
                PlaytimeMinutes = epic.PlaytimeMinutes;

                // Fallback cover via Epic Store KeyImages if IGDB cover is missing
                if (game.Cover?.Value?.ImageId == null && !string.IsNullOrWhiteSpace(epic.FallbackCoverUrl))
                {
                    CoverUrl = epic.FallbackCoverUrl;
                }
            }
            else if (game is GogStashGame gog)
            {
                ExternalAppId = gog.ExtId;
                ExternalSystem = "GOG";
                PlaytimeMinutes = gog.PlaytimeMinutes;

                if (game.Cover?.Value?.ImageId == null && !string.IsNullOrWhiteSpace(gog.FallbackCoverUrl))
                {
                    CoverUrl = gog.FallbackCoverUrl;
                }
            }
            else if (game is AmazonStashGame amazon)
            {
                ExternalSystem = "Amazon";

                if (game.Cover?.Value?.ImageId == null && !string.IsNullOrWhiteSpace(amazon.FallbackCoverUrl))
                {
                    CoverUrl = amazon.FallbackCoverUrl;
                }
            }
        }

        /// <summary>
        /// Factory method for StashGame (binds automatically in .Select(StashGameCardDto.FromModel))
        /// </summary>
        public static StashGameCardDto FromModel(StashGame game)
        {
            return new StashGameCardDto(game);
        }

        public static StashGameCardDto FromModel(LocalStashGame game)
        {
            return new StashGameCardDto(game);
        }

        public static StashGameCardDto FromModel(SteamStashGame game)
        {
            return new StashGameCardDto(game);
        }

        public static StashGameCardDto FromModel(EpicStashGame game)
        {
            return new StashGameCardDto(game);
        }
    }
}