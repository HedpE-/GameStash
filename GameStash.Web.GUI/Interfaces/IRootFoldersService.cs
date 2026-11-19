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
        IReadOnlyList<LocalStashGame> FoundFolders { get; }

        Task InitializeAsync();
        Task AddRootFolderAsync(string path);
        Task RemoveRootFolderAsync(string path);
        Task RefreshRootFolderAsync(string path);
        Task RefreshAllRootFoldersAsync();

        // Methods to encapsulate mutations on FoundFolders
        void AddFoundFolder(LocalStashGame game);
        void AddFoundFolders(IEnumerable<LocalStashGame> games);
        void RemoveFoundFolder(string folderPath);
        void RemoveFoundFolders(IEnumerable<string> folderPaths);
        void ClearFoundFolders();
        void IgnoreFoundFolder(string folderPath);
    }
}