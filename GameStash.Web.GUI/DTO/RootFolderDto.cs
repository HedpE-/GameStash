using GameStash.Core.Models;

namespace GameStash.Web.GUI.DTO
{
    public class RootFolderDto
    {
        public int Id { get; set; }
        public string Path { get; set; } = string.Empty;
        public long UsedSpace { get; set; }
        public long FreeSpace { get; set; }
        public long TotalSpace => UsedSpace + FreeSpace;
        public long FoundGamesCount { get; set; }
        public bool IsImportingFolders { get; set; }

        public static RootFolderDto FromModel(RootFolder model)
        {
            return new RootFolderDto
            {
                Id = model.Id,
                Path = model.Path ?? string.Empty,
                UsedSpace = model.UsedSpace,
                FreeSpace = model.FreeSpace,
                FoundGamesCount = model.FoundGamesCount,
                IsImportingFolders = model.IsImportingFolders
            };
        }
    }
}