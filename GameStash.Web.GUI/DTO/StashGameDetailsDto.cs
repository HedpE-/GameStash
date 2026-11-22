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
            else if (game is SteamStashGame steam)
            {
                PlaytimeMinutes = steam.PlaytimeMinutes;

                if (game.Cover?.Value?.ImageId == null && steam.ExtId.HasValue)
                {
                    var cdnUrl = $"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{steam.ExtId.Value}/library_600x900.jpg";
                    using var headClient = new HttpClient();
                    using var request = new HttpRequestMessage(HttpMethod.Head, cdnUrl);
                    using var response = headClient.Send(request, HttpCompletionOption.ResponseHeadersRead);

                    if (response.IsSuccessStatusCode)
                        CoverUrl = cdnUrl;
                }
            }
            else if (game is EpicStashGame epic)
            {
                PlaytimeMinutes = epic.PlaytimeMinutes;
                if (game.Cover?.Value?.ImageId == null && !string.IsNullOrWhiteSpace(epic.FallbackCoverUrl))
                    CoverUrl = epic.FallbackCoverUrl;
            }
            else if (game is GogStashGame gog)
            {
                PlaytimeMinutes = gog.PlaytimeMinutes;
                if (game.Cover?.Value?.ImageId == null && !string.IsNullOrWhiteSpace(gog.FallbackCoverUrl))
                    CoverUrl = gog.FallbackCoverUrl;
            }
            else if (game is AmazonStashGame amazon)
            {
                if (game.Cover?.Value?.ImageId == null && !string.IsNullOrWhiteSpace(amazon.FallbackCoverUrl))
                    CoverUrl = amazon.FallbackCoverUrl;
            }

            ChangeDateFormat(dateFormat);
        }

        public static StashGameDetailsDto FromModel(StashGame game, string? dateFormat = null)
        {
            ArgumentNullException.ThrowIfNull(game);
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