using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameStash.Core.Models;
using IgdbSharp;

namespace GameStash.Web.GUI.Interfaces
{
    public interface IRootFoldersService
    {
        event Action? RootFoldersChanged;

        IReadOnlyList<RootFolder> RootFolders { get; }
        IReadOnlyList<StashGame> FoundFolders { get; }

        Task InitializeAsync();
        Task AddRootFolderAsync(string path);
        Task RemoveRootFolderAsync(string path);
        Task RefreshRootFolderAsync(string path);
        Task RefreshAllRootFoldersAsync();

        // Methods to encapsulate mutations on FoundFolders
        void AddFoundFolder(StashGame game);
        void AddFoundFolders(IEnumerable<StashGame> games);
        void RemoveFoundFolder(string folderPath);
        void RemoveFoundFolders(IEnumerable<string> folderPaths);
        void ClearFoundFolders();
    }
}