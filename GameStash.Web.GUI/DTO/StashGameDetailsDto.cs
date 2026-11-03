using GameStash.Core.Models;
using IgdbSharp.Helpers;
using IgdbSharp.Models;
using System;
using System.Linq;

namespace GameStash.Web.GUI.DTO
{
    public class StashGameDetailsDto : GameDetaislDto
    {
        public bool IsFavorite { get; set; }
        public bool IsPlayed { get; set; }
        public bool IsFinished { get; set; }
        public bool IsInstalled { get; set; }
        public bool IsFolderValid { get; set; }
        public string Folder { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string DateAddedFormatted { get; set; } = string.Empty;
        public long FolderDiskSpaceBytes { get; set; }
        public string FolderSizeFormatted { get; set; } = string.Empty;
        private string _dateFormat = "dd MMM, yyyy";
        private readonly DateTime? dateAdded;

        private StashGameDetailsDto() : base() { }

        private StashGameDetailsDto(StashGame game, string dateFormat = null) : base(game)
        {
            // Local Library specific properties
            IsFavorite = game.IsFavorite;
            IsPlayed = game.IsPlayed;
            IsFinished = game.IsFinished;
            IsInstalled = game.IsInstalled;
            IsFolderValid = game.IsFolderValid;
            Folder = game.Folder ?? string.Empty;
            Type = game.PackageType ?? "PC";
            FolderDiskSpaceBytes = game.FolderDiskSpaceBytes;
            FolderSizeFormatted = FormatBytes(game.FolderDiskSpaceBytes);
            dateAdded = game.DateAdded;

            ChangeDateFormat(dateFormat);
        }

        public static StashGameDetailsDto FromModel(StashGame game, string dateFormat = null)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));

            var dto = new StashGameDetailsDto(game, dateFormat);

            return dto;
        }

        public void ChangeDateFormat(string newFormat)
        {
            if(!string.IsNullOrEmpty(newFormat))
                _dateFormat = newFormat;
            DateAddedFormatted = dateAdded?.ToString(_dateFormat) ?? "TBD";
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.#} {sizes[order]}";
        }
    }
}