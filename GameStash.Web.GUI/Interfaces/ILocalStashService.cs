using GameStash.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Interfaces
{
    public interface ILocalStashService : IStashServiceBase<LocalStashGame>
    {
        IReadOnlyList<LocalStashGame> GetPlaylist();
        Task AddGameToLibrary(LocalStashGame game);
        Task RemoveGameFromStash(string id);
        Task RemoveGameFromLibrary(LocalStashGame game);
        Task AddGameToPlaylist(LocalStashGame game);
        Task RemoveGameFromPlaylist(LocalStashGame game);
        Task RemoveByRootFolder(string path);
        Task<LocalStashGame> RefreshGameAsync(long id);
        Task Flush();
    }
}
