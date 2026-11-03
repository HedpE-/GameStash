using GameStash.Core.Extensions;
using GameStash.Core.Models;
using IgdbSharp.Enumerators;
using System;

namespace GameStash.Web.GUI.DTO
{
    public class StashGameCardDto : GameCardDto
    {
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
        public bool IsFolderValid { get; set; }
        public string Folder { get; set; } = string.Empty;

        public StashGameCardDto(StashGame game) : base(game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));

            SortName = !string.IsNullOrEmpty(game.SortName) ? game.SortName : game.Name ?? string.Empty;
            FirstReleaseDate = game.FirstReleaseDate;
            ReleaseDateFormatted = game.FirstReleaseDate?.ToString("dd/MM/yyyy") ?? "No date";
            ReleaseYear = game.FirstReleaseDate?.ToString("yyyy") ?? "TBD";
            DateAdded = game.DateAdded;
            DateAddedFormatted = game.DateAdded?.ToString("dd/MM/yyyy") ?? "Unknown";
            FolderDiskSpaceBytes = game.FolderDiskSpaceBytes;
            FolderSizeFormatted = FormatBytes(game.FolderDiskSpaceBytes);
            IsFavorite = game.IsFavorite;
            IsInstalled = game.IsInstalled;
            IsFolderValid = game.IsFolderValid;
            Folder = game.Folder ?? string.Empty;
        }

        public static new StashGameCardDto FromModel(StashGame game)
        {
            return new StashGameCardDto(game);
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