using GameStash.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Interfaces
{
    public interface ILibraryService
    {
        IReadOnlyList<Game> GetLocalLibrary(string type);
        Task InitializeAsync();
        Task<Game?> GetByIdAsync(long id, string libraryType);
        Task<bool> ToggleFavoriteAsync(long id, bool isFavorite, string libraryType);
        Task<bool> ToggleInstalledAsync(long id, bool isInstalled, string libraryType);
        Task<bool> UpdateSortNameAsync(long id, string sortName, string libraryType);
        Task<bool> RemoveAsync(long id, string libraryType);
        Task<Game?> RefreshGameAsync(long id, string libraryType);
    }
}