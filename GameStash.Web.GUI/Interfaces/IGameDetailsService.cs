using IgdbSharp.Models;
using System.Threading.Tasks;

namespace GameStash.Web.GUI.Interfaces
{
    /// <summary>
    /// Handles granular entity isolation queries for a specific metadata entry bypassing landing state buffers.
    /// </summary>
    public interface IGameDetailsService
    {
        /// <summary>
        /// Looks up full relation graphs for a target record matching its isolated tracking reference.
        /// </summary>
        // Adiciona <T> aqui também
        Task<T?> GetGameDetailsAsync<T>(long gameId) where T : Game;
    }
}