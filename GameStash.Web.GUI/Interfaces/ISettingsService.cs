using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using GameStash.Core.Models;
using IgdbSharp;

namespace GameStash.Web.GUI.Interfaces
{
    public interface ISettingsService
    {
        // Event to notify the background worker whenever root folders are mutated
        event Action? RootFoldersChanged;

        string IgdbClientId { get; }
        string IgdbSecret { get; }
        ConcurrentBag<RootFolder> RootFolders { get; }
        ConcurrentBag<Game> FoundFolders { get; }
        ConcurrentQueue<string> MessageQueue { get; }

        Task InitializeAsync();
        IgdbClient GetIgdbClient();
        Task UpdateIgdbCredentialsAsync(string clientId, string secret);
        void AddRootFolder(string path);
        void RemoveRootFolder(RootFolder folder);
        Task RefreshAllRootFolders();
    }
}
