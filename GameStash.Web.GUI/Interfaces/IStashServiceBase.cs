using System.Collections.Generic;
using System.Threading.Tasks;
using GameStash.Core.Models;

namespace GameStash.Web.GUI.Interfaces
{
    public interface IStashServiceBase<TEntity> where TEntity : StashGame
    {
        /// <summary>
        /// Synchronizes the external library with the local database and memory cache.
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Retrieves the stash records from the cache or triggers initialization if empty/forced.
        /// </summary>
        Task<IReadOnlyList<TEntity>> GetStashAsync(bool forceRefresh = false);

        IReadOnlyList<TEntity> GetPlaylistItems();

        /// <summary>
        /// Retrieves a single game from the cached stash by its identifier.
        /// </summary>
        Task<TEntity?> GetByIdAsync(long id);

        /// <summary>
        /// Updates a specific property of a stash entity and persists it to the database.
        /// </summary>
        Task<bool> UpdateGamePropertyAsync(long id, string propertyName, object? value);
    }
}
