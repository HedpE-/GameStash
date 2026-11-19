using GameStash.Core.Data;
using GameStash.Core.Models;
using GameStash.Web.GUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace GameStash.Core.Services
{
    public abstract class StashServiceBase<TEntity> : IStashServiceBase<TEntity> where TEntity : StashGame
    {
        protected readonly object SyncLock = new();
        protected List<TEntity>? CachedGames = [];

        private Task? _initializationTask;

        /// <summary>
        /// Subclasses implement the actual synchronization logic here.
        /// </summary>
        protected abstract Task ExecuteInitializeAsync();

        /// <summary>
        /// Public entry point (used by Program.cs and GetStashAsync) that prevents duplicate runs
        /// and awaits the active initialization task if one is already in progress.
        /// </summary>
        public Task InitializeAsync()
        {
            lock (SyncLock)
            {
                if (_initializationTask == null || _initializationTask.IsCompleted)
                {
                    _initializationTask = RunInitializationWrapperAsync();
                }
                return _initializationTask;
            }
        }

        private async Task RunInitializationWrapperAsync()
        {
            try
            {
                await ExecuteInitializeAsync();
            }
            finally
            {
                lock (SyncLock)
                {
                    _initializationTask = null;
                }
            }
        }

        public async Task<IReadOnlyList<TEntity>> GetStashAsync(bool forceRefresh = false)
        {
            bool isRunning;
            lock (SyncLock)
            {
                isRunning = _initializationTask != null && !_initializationTask.IsCompleted;
            }

            if (isRunning || forceRefresh || CachedGames == null || !CachedGames.Any())
            {
                await InitializeAsync();
            }

            lock (SyncLock)
            {
                return CachedGames?.AsReadOnly() ?? Enumerable.Empty<TEntity>().ToList().AsReadOnly();
            }
        }

        public async Task<bool> UpdateGamePropertyAsync(long id, string propertyName, object? value)
        {
            var prop = typeof(TEntity).GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (prop == null || !prop.CanWrite)
                return false;

            TEntity? game;

            lock (SyncLock)
            {
                game = CachedGames?.FirstOrDefault(g => IsMatchingId(g, id));
                if (game == null) return false;

                object? targetValue = UnwrapJsonValue(value);

                if (targetValue != null && !prop.PropertyType.IsInstanceOfType(targetValue))
                {
                    var underlyingType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                    targetValue = Convert.ChangeType(targetValue, underlyingType);
                }

                prop.SetValue(game, targetValue);
            }

            await using var context = GameStashDbContext.CreateDbContext();
            context.Set<TEntity>().Update(game);
            await context.SaveChangesAsync();

            return true;
        }

        protected abstract bool IsMatchingId(TEntity game, long id);

        private static object? UnwrapJsonValue(object? value)
        {
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                return jsonElement.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.True => true,
                    System.Text.Json.JsonValueKind.False => false,
                    System.Text.Json.JsonValueKind.String => jsonElement.GetString(),
                    System.Text.Json.JsonValueKind.Number => jsonElement.TryGetInt64(out var l) ? l : jsonElement.GetDouble(),
                    _ => jsonElement.GetRawText()
                };
            }
            return value;
        }

        public Task<TEntity?> GetByIdAsync(long id)
        {
            lock (SyncLock)
            {
                var match = CachedGames?.FirstOrDefault(g => IsMatchingId(g, id));
                return Task.FromResult(match);
            }
        }
    }
}
