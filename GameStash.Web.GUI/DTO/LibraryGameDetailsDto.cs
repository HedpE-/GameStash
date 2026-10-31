using GameStash.Core.Models;
using IgdbSharp.Helpers;
using System;
using System.Linq;

namespace GameStash.Web.GUI.DTO
{
    public class LibraryGameDetailsDto : GameDetaislDto
    {
        public bool IsFavorite { get; set; }
        public bool IsInstalled { get; set; }
        public bool IsFolderValid { get; set; }
        public string Folder { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string DateAddedFormatted { get; set; } = string.Empty;
        public long FolderDiskSpaceBytes { get; set; }
        public string FolderSizeFormatted { get; set; } = string.Empty;

        private LibraryGameDetailsDto() : base() { }

        private LibraryGameDetailsDto(Game game) : base(game)
        {
            // Local Library specific properties
            IsFavorite = game.IsFavorite;
            IsInstalled = game.IsInstalled;
            IsFolderValid = game.IsFolderValid;
            Folder = game.Folder ?? string.Empty;
            Type = game.Type ?? "PC";
            DateAddedFormatted = game.DateAdded?.ToString("dd/MM/yyyy") ?? "TBD";
            FolderDiskSpaceBytes = game.FolderDiskSpaceBytes;
            FolderSizeFormatted = FormatBytes(game.FolderDiskSpaceBytes);
        }

        public static LibraryGameDetailsDto FromModel(Game game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));

            var dto = new LibraryGameDetailsDto(game);

            return dto;
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