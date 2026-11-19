using GameStash.Core.Extensions;
using GameStash.Core.Models;
using GameStash.Web.GUI.Extensions;
using GameStash.Web.GUI.Helpers;
using IgdbSharp.Helpers;
using System;
using System.IO;
using System.Net.Http;

namespace GameStash.Web.GUI.DTO
{
    public class StashGameDetailsDto : GameDetailsDto
    {
        public bool IsFavorite { get; set; }
        public bool IsPlayed { get; set; }
        public bool IsPlaying { get; set; }
        public bool IsFinished { get; set; }
        public bool PlayedToEnd { get; set; }
        public bool IsInstalled { get; set; }
        public bool IsFolderValid { get; set; }
        public string SortName { get; set; } = string.Empty;
        public string Folder { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string DateAddedFormatted { get; set; } = string.Empty;
        public long FolderDiskSpaceBytes { get; set; }
        public string FolderSizeFormatted { get; set; } = string.Empty;
        public int PlaytimeMinutes { get; set; }

        private string _dateFormat = "dd MMM, yyyy";
        private readonly DateTime? _dateAdded;

        private StashGameDetailsDto() : base() { }

        // Primary constructor handling polymorphic StashGame entities (LocalStashGame or SteamStashGame)
        private StashGameDetailsDto(StashGame game, string? dateFormat = null) : base(game.Clone())
        {
            IsFavorite = game.IsFavorite;
            IsPlayed = game.IsPlayed;
            IsPlaying = game.IsPlaying;
            IsFinished = game.IsFinished;
            PlayedToEnd = game.PlayedToEnd;
            IsInstalled = game.IsInstalled;
            SortName = !string.IsNullOrEmpty(game.SortName) ? game.SortName : game.Name ?? string.Empty;

            // Handle Local Stash specific properties safely
            if (game is LocalStashGame local)
            {
                IsFolderValid = local.IsFolderValid;
                Folder = local.Folder ?? string.Empty;
                Type = local.PackageType ?? string.Empty;
                _dateAdded = local.DateAdded;

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
            }
            // Handle Steam Stash specific properties safely
            else if (game is SteamStashGame steam)
            {
                PlaytimeMinutes = steam.PlaytimeMinutes;

                // Fallback cover via Steam CDN if IGDB cover is missing
                if (game.Cover?.Value?.ImageId == null && steam.ExtId.HasValue)
                {
                    var cdnUrl = $"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{steam.ExtId.Value}/library_600x900.jpg";
                    HttpClient _headClient = new HttpClient();
                    var request = new HttpRequestMessage(HttpMethod.Head, cdnUrl);
                    var response = _headClient.Send(request, HttpCompletionOption.ResponseHeadersRead);

                    if (response.IsSuccessStatusCode)
                        CoverUrl = cdnUrl;
                }
            }

            ChangeDateFormat(dateFormat);
        }

        public static StashGameDetailsDto FromModel(StashGame game, string? dateFormat = null)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            return new StashGameDetailsDto(game, dateFormat);
        }

        public static StashGameDetailsDto FromModel(LocalStashGame game, string? dateFormat = null)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            return new StashGameDetailsDto(game, dateFormat);
        }

        public static StashGameDetailsDto FromModel(SteamStashGame game, string? dateFormat = null)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            return new StashGameDetailsDto(game, dateFormat);
        }

        public void ChangeDateFormat(string newFormat)
        {
            if (!string.IsNullOrEmpty(newFormat))
                _dateFormat = newFormat;

            DateAddedFormatted = _dateAdded?.ToString(_dateFormat) ?? "TBD";
        }
    }
}