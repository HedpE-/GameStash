using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Common.Lib.Managers; // Ensure this maps to where FolderMonitorWithThrottle lives
using GameStash.Web.GUI.Interfaces;

namespace GameStash.Web.GUI.Services
{
    public class RootFolderMonitorService(IRootFoldersService rootFoldersService) : BackgroundService
    {
        private readonly IRootFoldersService _rootFoldersService = rootFoldersService;
        private readonly object _watcherLock = new();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Log.ForContext<RootFolderMonitorService>().Information("Synchronizing file system watchers engine...");

            // Hook into the central database settings notification pipeline
            _rootFoldersService.RootFoldersChanged += OnRootFoldersChanged;

            // Trigger the initial setup synchronization on system boot
            UpdateWatchers();

            // FIX: Keep the background worker thread alive and polling until the host shuts down
            while (!stoppingToken.IsCancellationRequested)
            {
                // Keeps the task active without consuming CPU cycles
                await Task.Delay(1000, stoppingToken);
            }
        }

        private void OnRootFoldersChanged()
        {
            Log.ForContext<RootFolderMonitorService>().Information("Change detected in settings. Refreshing physical handlers...");
            UpdateWatchers();
        }

        private void UpdateWatchers()
        {
            lock (_watcherLock)
            {
                // 1. Safe teardown of existing watchers to prevent system memory leaks and locked file handles
                //foreach (var oldWatcher in _activeWatchers)
                //{
                //    try
                //    {
                //        oldWatcher.StopMonitoring();
                //        oldWatcher.FolderCreated -= Watcher_FolderCreated;
                //        oldWatcher.FolderRenamed -= Watcher_FolderRenamed;
                //        oldWatcher.FolderDeleted -= Watcher_FolderDeleted;
                //    }
                //    catch (Exception ex)
                //    {
                //        Log.ForContext<RootFolderMonitorService>().Error(ex, "Error tearing down watcher for path: {Path}", oldWatcher.MonitoredPath);
                //    }
                //}
                //_activeWatchers.Clear();

                //// 2. Rebuild the watcher tree based on the live source of truth inside SettingsService
                //foreach (var folder in _rootFoldersService.RootFolders)
                //{
                //    if (!Directory.Exists(folder.Path))
                //    {
                //        Log.ForContext<RootFolderMonitorService>().Warning("Target directory path no longer exists: {Path}", folder.Path);
                //        continue;
                //    }

                //    try
                //    {
                //        var watcher = new FolderMonitorWithThrottle(folder.Path);
                //        watcher.FolderCreated += Watcher_FolderCreated;
                //        watcher.FolderRenamed += Watcher_FolderRenamed;
                //        watcher.FolderDeleted += Watcher_FolderDeleted;

                //        _activeWatchers.Add(watcher);
                //        Log.ForContext<RootFolderMonitorService>().Information("Monitoring successfully attached to: {Path}", folder.Path);
                //    }
                //    catch (Exception ex)
                //    {
                //        Log.ForContext<RootFolderMonitorService>().Error(ex, "Failed to initialize watcher for path: {Path}", folder.Path);
                //    }
                //}
            }
        }

        // Event hooks matching your original system notifications logic
        private void Watcher_FolderCreated(object? sender, FileSystemEventArgs e)
        {
            Log.ForContext<RootFolderMonitorService>().Information("New folder directory entry detected: {Path}", e.FullPath);
            // Optional TODO: Trigger your automatic game identification matching here
        }

        private void Watcher_FolderRenamed(object? sender, RenamedEventArgs e)
        {
            Log.ForContext<RootFolderMonitorService>().Information("Folder layout renamed: {Old} -> {New}", e.OldFullPath, e.FullPath);
        }

        private void Watcher_FolderDeleted(object? sender, FileSystemEventArgs e)
        {
            Log.ForContext<RootFolderMonitorService>().Warning("Folder directory entry detached: {Path}", e.FullPath);
        }

        public override void Dispose()
        {
            // Clean up global shared memory event bindings during hosting engine shutdown
            _rootFoldersService.RootFoldersChanged -= OnRootFoldersChanged;

            lock (_watcherLock)
            {
                //foreach (var watcher in _activeWatchers)
                //{
                //    watcher.StopMonitoring();
                //}
            }

            base.Dispose();
        }
    }
}
