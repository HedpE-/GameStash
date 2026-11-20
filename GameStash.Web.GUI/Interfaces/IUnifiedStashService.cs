using GameStash.Web.GUI.DTO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Interfaces
{
    public interface IUnifiedStashService
    {
        Task<List<UnifiedStashCardDto>> GetAllUnifiedGamesAsync(bool forceRefresh = false);
    }
}
