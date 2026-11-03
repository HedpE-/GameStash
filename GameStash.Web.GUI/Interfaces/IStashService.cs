using GameStash.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Interfaces
{
    public interface IStashService
    {
        IReadOnlyList<StashGame> GetLibrary(string libraryType);
        Task InitializeAsync();
        Task AddGameToLibrary(StashGame game);
        Task RemoveGameFromLibrary(string id);
        Task RemoveGameFromLibrary(StashGame game);
        Task RemoveByRootFolder(string path);
        Task<StashGame?> GetByIdAsync(long id, string libraryType);
        Task<bool> ToggleFavoriteAsync(long id, bool isFavorite, string libraryType);
        Task<bool> ToggleInstalledAsync(long id, bool isInstalled, string libraryType);
        Task<bool> UpdateSortNameAsync(long id, string sortName, string libraryType);
        Task<StashGame?> RefreshGameAsync(long id, string libraryType);
        Task Flush();
    }
}
