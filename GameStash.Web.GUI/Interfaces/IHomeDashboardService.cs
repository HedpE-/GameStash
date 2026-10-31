using System.Threading.Tasks;
using GameStash.Web.GUI.DTO;

namespace GameStash.Web.GUI.Interfaces
{
    /// <summary>
    /// Defines contracts for retrieving globally cached landing page statistics from the IGDB pipeline.
    /// </summary>
    public interface IHomeDashboardService
    {
        /// <summary>
        /// Asynchronously fetches or returns day-cached rows for releases, ratings, and hyped queries.
        /// </summary>
        Task<HomeDashboardResponseDto> GetDashboardDataAsync();
    }
}
