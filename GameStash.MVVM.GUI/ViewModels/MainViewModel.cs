using Common.Lib.Collections;
using Common.Lib.Comparers;
using Common.Lib.Extensions;
using Common.Lib.HelperClasses;
using Common.Lib.Managers;
using MaterialDesignThemes.Wpf;
using GameStash.Core.Models;
using GameStash.Core.Services;
using GameStash.MVVM.GUI.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using IgdbSharp.Enumerators;

namespace GameStash.MVVM.GUI.ViewModels
{
    internal class MainViewModel : ObservableObject
    {
        private readonly string _databasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GameStash", "gamestash.db");
        private readonly List<FolderMonitorWithThrottle> _rootFolderWatchers;

        // Global Snackbar MessageQueue
        private ISnackbarMessageQueue _messageQueue = new SnackbarMessageQueue(TimeSpan.FromSeconds(5));
        public ISnackbarMessageQueue MessageQueue
        {
            get => _messageQueue;
            set
            {
                _messageQueue = value;
                RaisePropertyChanged();
            }
        }

        private FastObservableCollection<RootFolder> _rootFolders;
        public FastObservableCollection<RootFolder> RootFolders
        {
            get => _rootFolders;
            private set
            {
                if (_rootFolders != value)
                {
                    _rootFolders = value;
                    RaisePropertyChanged();
                }
            }
        }

        private ObservableCollection<StashGame> _localGamesLibrary;
        public ObservableCollection<StashGame> LocalGamesLibrary
        {
            get => _localGamesLibrary;
            private set
            {
                if (_localGamesLibrary != value)
                {
                    _localGamesLibrary = value;
                    RaisePropertyChanged();
                }
            }
        }

        public ICollectionView SortedImportedGames { get; set; }

        private string _selectedSortOption;
        public string SelectedSortOption
        {
            get => _selectedSortOption;
            set
            {
                if (_selectedSortOption != value)
                {
                    _selectedSortOption = value;
                    RaisePropertyChanged();
                    ApplySorting();
                }
            }
        }

        private ListSortDirection _selectedSortDirection;
        public ListSortDirection SelectedSortDirection
        {
            get => _selectedSortDirection;
            set
            {
                if (_selectedSortDirection != value)
                {
                    _selectedSortDirection = value;
                    RaisePropertyChanged();
                    ApplySorting();
                }
            }
        }

        private string _selectedFilterOption;
        public string SelectedFilterOption
        {
            get => _selectedFilterOption;
            set
            {
                if (_selectedFilterOption != value)
                {
                    _selectedFilterOption = value;
                    RaisePropertyChanged();
                    ApplyFiltering();
                }
            }
        }

        public ObservableCollection<char> AvailableLetters { get; } = new();

        private ObservableCollection<StashGame> _foundFolders;
        public ObservableCollection<StashGame> FoundFolders
        {
            get => _foundFolders;
            private set
            {
                if (_foundFolders != value)
                {
                    _foundFolders = value;
                    RaisePropertyChanged();
                }
            }
        }

        // Current folder path entered by the user
        private string _folderPath = "F:\\Jogos\\PC";
        public string FolderPath
        {
            get => _folderPath;
            set
            {
                _folderPath = value;
                RaisePropertyChanged();
            }
        }

        private string _igdb_client_id;
        public string Igdb_Client_Id
        {
            get => _igdb_client_id;
            set
            {
                _igdb_client_id = value;
                RaisePropertyChanged();
                SqliteHelper.UpdateSettingsEntry("IGDB_Client_Id", _igdb_client_id);
            }
        }

        private string _igdb_secret;
        public string Igdb_Secret
        {
            get => _igdb_secret;
            set
            {
                _igdb_secret = value;
                RaisePropertyChanged();
                SqliteHelper.UpdateSettingsEntry("IGDB_Secret", _igdb_secret);
            }
        }

        private bool _isInitiallizingDatabase;
        public bool IsInitiallizingDatabase
        {
            get => _isInitiallizingDatabase;
            set
            {
                if (_isInitiallizingDatabase != value)
                {
                    _isInitiallizingDatabase = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool _isLoadingLibrary;
        public bool IsLoadingLibrary
        {
            get => _isLoadingLibrary;
            set
            {
                if (_isLoadingLibrary != value)
                {
                    _isLoadingLibrary = value;
                    RaisePropertyChanged();
                }
            }
        }

        private string _loadingLibraryText;
        public string LoadingLibraryText
        {
            get => _loadingLibraryText;
            set
            {
                if (_loadingLibraryText != value)
                {
                    _loadingLibraryText = value;
                    RaisePropertyChanged();
                }
            }
        }

        private double _loadingLibraryProgress;
        public double LoadingLibraryProgress
        {
            get => _loadingLibraryProgress;
            set
            {
                if (_loadingLibraryProgress != value)
                {
                    _loadingLibraryProgress = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool _isImportAllItemsSelected;
        public bool IsImportAllItemsSelected
        {
            get => _isImportAllItemsSelected;
            set
            {
                if (_isImportAllItemsSelected != value)
                {
                    _isImportAllItemsSelected = value;
                    RaisePropertyChanged();
                    Parallel.ForEach(FoundFolders, x => x.IsSelected = value);
                }
            }
        }

        private bool _isCheckAllWithMatchBoxSelected;
        public bool IsCheckAllWithMatchBoxSelected
        {
            get => _isCheckAllWithMatchBoxSelected;
            set
            {
                if (_isCheckAllWithMatchBoxSelected != value)
                {
                    _isCheckAllWithMatchBoxSelected = value;
                    RaisePropertyChanged();
                    Parallel.ForEach(FoundFolders.Where(f => f.IgdbSelectedMatch != null), f => f.IsSelected = value);
                }
            }
        }

        public Action<char>? ScrollToLetterAction { get; set; }

        private ICommand _scrollToLetterCommand;
        public ICommand ScrollToLetterCommand
        {
            get
            {
                return _scrollToLetterCommand ??= new SimpleCommand
                {
                    CanExecuteDelegate = x => x is char,
                    ExecuteDelegate = x => ScrollToLetter((char)x)
                };
            }
        }

        private ICommand _refreshAllRootFoldersCommand;
        public ICommand RefreshAllRootFoldersCommand
        {
            get
            {
                return _refreshAllRootFoldersCommand ??= new SimpleCommand
                {
                    CanExecuteDelegate = x => RootFolders.Any(),
                    ExecuteDelegate = async x =>
                    {
                        await RefreshAllRootFolders();
                    }
                };
            }
        }

        private ICommand _refreshRootFolderCommand;
        public ICommand RefreshRootFolderCommand
        {
            get
            {
                return _refreshRootFolderCommand ??= new SimpleCommand
                {
                    CanExecuteDelegate = x => x is RootFolder f && RootFolders.Contains(f),
                    ExecuteDelegate = async x =>
                    {
                        var rootFolder = x as RootFolder;
                        var folders = FoundFolders.Where(f => f.RootFolder == rootFolder.Path);
                        foreach (var folder in folders)
                        {
                            FoundFolders.Remove(folder);
                        }
                        await ImportGamesFromFolderAsync(rootFolder);
                    }
                };
            }
        }

        private ICommand _igdbMatchSelectedFoldersCommand;
        public ICommand IgdbMatchSelectedFoldersCommand
        {
            get
            {
                return _igdbMatchSelectedFoldersCommand ??= new SimpleCommand
                {
                    CanExecuteDelegate = x => FoundFolders.Any(f => f.IsSelected),
                    ExecuteDelegate = async x =>
                    {
                        var selectedFolders = FoundFolders.Where(f => f.IsSelected).ToList();
                        IsImportAllItemsSelected = false;
                        Parallel.ForEach(FoundFolders, x => x.IsSelected = false);
                        Parallel.ForEach(selectedFolders, async game => await game.MatchIgdbGame(Igdb_Client_Id, Igdb_Secret));
                    }
                };
            }
        }

        private ICommand _importSelectedFoldersCommand;
        public ICommand ImportSelectedFoldersCommand
        {
            get
            {
                return _importSelectedFoldersCommand ??= new SimpleCommand
                {
                    CanExecuteDelegate = x => FoundFolders.Any(x => x.IsSelected) && FoundFolders.Where(x => x.IsSelected).All(x => x.IgdbSelectedMatch != null),
                    ExecuteDelegate = x =>
                    {
                        var games = FoundFolders.Where(x => x.IsSelected).ToList();

                        for (int c = 0; c < games.Count; c++)
                        {
                            var game = games[c];
                            try
                            {
                                game.MatchIgdbGame(game.IgdbSelectedMatch);

                                if (!game.DateAdded.HasValue)
                                    game.DateAdded = DateTime.Now;

                                LocalGamesLibrary.Add(game);
                                FoundFolders.Remove(game);
                                SqliteHelper.InsertOrUpdateLibraryEntry(game);
                            }
                            catch (Exception e)
                            {
                                var m = e.Message;
                            }
                        }
                    }
                };
            }
        }

        private ICommand _addFolderCommand;
        public ICommand AddFolderCommand
        {
            get
            {
                return _addFolderCommand ??= new SimpleCommand
                {
                    CanExecuteDelegate = x => Directory.Exists(FolderPath) && !RootFolders.Any(f => f.Path == FolderPath),
                    ExecuteDelegate = x => AddRootFolder(FolderPath)
                };
            }
        }

        private ICommand _removeRootFolderCommand;
        public ICommand RemoveRootFolderCommand
        {
            get
            {
                return _removeRootFolderCommand ??= new SimpleCommand
                {
                    CanExecuteDelegate = x => x is RootFolder && RootFolders.Contains((RootFolder)x),
                    ExecuteDelegate = x =>
                    {
                        var folder = x as RootFolder;
                        RemoveRootFolder(folder);
                    }
                };
            }
        }

        private ICommand _removeLibraryItemCommand;

        public ICommand RemoveLibraryItemCommand
        {
            get
            {
                return _removeLibraryItemCommand ??= new SimpleCommand
                {
                    CanExecuteDelegate = x => x is StashGame,
                    ExecuteDelegate = x =>
                    {
                        var game = x as StashGame;
                        LocalGamesLibrary.Remove(game);
                        if (game.IsFolderValid)
                        {
                            FoundFolders.Add(new StashGame
                            {
                                PackageType = game.PackageType,
                                Folder = game.Folder,
                                RootFolder = game.RootFolder,
                                Name = game.Name,
                                MatchName = !string.IsNullOrEmpty(game.Name) ? game.Name : game.MatchName,
                                IgdbMatches = new[] { game },
                                IgdbSelectedMatch = game
                            });
                        }
                        SqliteHelper.RemoveEntryFromLibrary(game);
                    }
                };
            }
        }

        private ICommand _openGameFolderCommand;

        public ICommand OpenGameFolderCommand
        {
            get
            {
                return _openGameFolderCommand ??= new SimpleCommand
                {
                    CanExecuteDelegate = x => x is StashGame,
                    ExecuteDelegate = x =>
                    {
                        try
                        {
                            // Open Windows Explorer at the specified folder
                            Process.Start("explorer.exe", $"\"{((StashGame)x).Folder}\"");
                        }
                        catch (System.Exception ex)
                        {
                            // Handle error appropriately, e.g., show a message or log the error
                            // Here we can log or throw depending on your app design
                            // For now, a simple console message
                            MessageQueue.Enqueue($"Error opening folder: {ex.Message}");
                        }
                    }
                };
            }
        }

        private ICommand _refreshCommand;
        public ICommand RefreshCommand
        {
            get
            {
                return _refreshCommand ??= new SimpleCommand
                {
                    CanExecuteDelegate = x => x is StashGame,
                    ExecuteDelegate = async x =>
                    {
                        var game = x as StashGame;
                        game.Cover = null;
                        MessageQueue.Enqueue($"Refreshing {game.Name} {(game.FirstReleaseDate.HasValue ? "(" + game.FirstReleaseDate.Value.Year + ")" : "")}");
                        game.CheckIsFolderValid();
                        await game.Refresh(Igdb_Client_Id, Igdb_Secret);
                    }
                };
            }
        }

        private ICommand _refreshLibraryCommand;
        public ICommand RefreshLibraryCommand
        {
            get
            {
                return _refreshLibraryCommand ??= new SimpleCommand
                {
                    CanExecuteDelegate = x => true,
                    ExecuteDelegate = async x =>
                    {
                        IsLoadingLibrary = true;

                        LocalGamesLibrary.Clear();
                        await LoadLibrary();
                        //await RefreshAllRootFolders();
                    }
                };
            }
        }

        private ICommand _copyGamesListToClipboardCommand;
        public ICommand CopyGamesListToClipboardCommand
        {
            get
            {
                return _copyGamesListToClipboardCommand ??= new SimpleCommand
                {
                    CanExecuteDelegate = x => true,
                    ExecuteDelegate = x =>
                    {
                        if (IsLoadingLibrary)
                        {
                            MessageQueue.Enqueue("Please wait for Local Library to finish loading");
                            return;
                        }
                        var list = LocalGamesLibrary.Select(g => $"{g.Name} ({(g.IsFavorite ? "Favorite | " : "")}{(g.IsInstalled ? "Installed" : "Not Installed")})");
                        var success = Tools.TryCopyToClipboard(string.Join(Environment.NewLine, list));
                        MessageQueue.Enqueue(success ? "Games List copied to clipboard" : "Failed to copy Games List to the clipboard");
                    }
                };
            }
        }

        private ICommand _sortCommand;
        public ICommand SortCommand => _sortCommand ??= new SimpleCommand
        {
            CanExecuteDelegate = x => x is string && !string.IsNullOrEmpty(x.ToString()),
            ExecuteDelegate = x =>
            {
                string sortOption = x.ToString();
                if (sortOption != SelectedSortOption)
                {
                    SelectedSortOption = sortOption;
                    SelectedSortDirection = ListSortDirection.Ascending;
                }
                else
                    SelectedSortDirection = SelectedSortDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
            }
        };

        private ICommand _filterCommand;
        public ICommand FilterCommand => _filterCommand ??= new SimpleCommand
        {
            CanExecuteDelegate = x => x is string,
            ExecuteDelegate = x => SelectedFilterOption = x.ToString()
        };

        public MainViewModel()
        {
            _rootFolderWatchers = new List<FolderMonitorWithThrottle>();
            RootFolders = new FastObservableCollection<RootFolder>();
            FoundFolders = new ObservableCollection<StashGame>();
            LocalGamesLibrary = new ObservableCollection<StashGame>();
            //LocalGamesLibrary.ItemChanged += ImportedGames_ItemChanged;
            InitializeDatabase();
        }

        private void ImportedGames_ItemChanged(object? sender, ItemChangedEventArgs<StashGame> e)
        {
            if (e.PropertyName == nameof(StashGame.IsFavorite) || e.PropertyName == nameof(StashGame.IsInstalled) || e.PropertyName == nameof(StashGame.SortName))
            {
                SqliteHelper.InsertOrUpdateLibraryEntry(e.Item);
                if ((e.PropertyName == nameof(StashGame.IsFavorite) && (SelectedFilterOption == "Favorites" || SelectedFilterOption == "Not Favorites"))
                    || (e.PropertyName == nameof(StashGame.IsInstalled) && (SelectedFilterOption == "Installed" || SelectedFilterOption == "Not Installed"))
                    || (e.PropertyName == nameof(StashGame.SortName) && SelectedSortOption == "Name"))
                {
                    SortedImportedGames.Refresh();
                    if (e.PropertyName == nameof(StashGame.SortName) && SelectedSortOption == "Name")
                        LoadAvailableLetters();
                }
            }
        }

        private async Task InitializeDatabase()
        {
            IsInitiallizingDatabase = true;

            MessageQueue.Enqueue("Initializing Database");

            await Task.Run(() => SqliteHelper.InitializeDatabase(_databasePath));

            // Load settings and populate Folders and IGDB credentials
            await LoadSettings();

            // Load library data and populate the ImportedGames collection
            await LoadLibrary();

            MessageQueue.Enqueue("Database initialized successfully!");

            IsInitiallizingDatabase = false;
        }

        private async Task LoadSettings()
        {
            // Get the settings from the database (only the first row of Settings table)
            var settings = await SqliteHelper.GetSettingsAsync();
            if (settings != null)
            {
                // Set IGDB credentials
                Igdb_Client_Id = settings.IGDB_Client_ID;
                Igdb_Secret = settings.IGDB_Secret;

                // Parse RootFolders into individual folder paths and create Folder objects
                var rootFolders = settings.RootFolders?.Split(';') ?? new string[0];
                foreach (var folderPath in rootFolders)
                {
                    if (!string.IsNullOrEmpty(folderPath))
                        AddRootFolder(folderPath);
                }
            }
        }

        private async Task LoadLibrary()
        {
            try
            {
                IsLoadingLibrary = true;
                MessageQueue.Enqueue("Loading Library items");
                LoadingLibraryText = "Loading Library items";

                SortedImportedGames = CollectionViewSource.GetDefaultView(LocalGamesLibrary);
                SortedImportedGames.Filter = FilterPredicate;

                SelectedSortOption = "Name";
                SelectedSortDirection = ListSortDirection.Ascending;
                SelectedFilterOption = "All";

                // Load all rows from the Library table
                var libraryEntries = await SqliteHelper.GetLibraryAsync();
                foreach (var entry in libraryEntries)
                {
                    // Refresh the game using IGDB credentials
                    LocalGamesLibrary.Add(entry);
                    // Add the game to the ImportedGames collection
                    if (FoundFolders.Any(x => x.Folder == entry.Folder))
                    {
                        IEnumerable<StashGame> foldersToRemove = FoundFolders.Where(x => x.Folder == entry.Folder);
                        foreach (var folder in foldersToRemove)
                        {
                            FoundFolders.Remove(folder);
                        }
                    }
                }

                LoadAvailableLetters();

                LoadingLibraryText = "Loading Library items   0/" + libraryEntries.Count;
                LoadingLibraryProgress = 0;
                int completedGames = 0;
                var tasks = libraryEntries.Select(async entry =>
                {
                    try
                    {
                        entry.IsIgdbMatched = true;
                        await entry.Refresh(Igdb_Client_Id, Igdb_Secret, requestPriority: RequestPriority.Low);

                        // Increment the counter atomically
                        int completed = Interlocked.Increment(ref completedGames);

                        // Update loading message
                        LoadingLibraryText = $"Loading Library items {completed}/{libraryEntries.Count}";
                        LoadingLibraryProgress = (double)completed / libraryEntries.Count;
                        // MessageQueue.Enqueue($"Loading Library items... {completed}/{libraryEntries.Count}");
                    }
                    catch
                    {
                        // Log or handle the error appropriately
                        throw;
                    }
                });

                // Await the completion of all tasks
                await Task.WhenAll(tasks);

                MessageQueue.Enqueue("Library items successfully lodaded!");
            }
            catch (Exception e)
            {
                var m = e.Message;
                MessageQueue.Enqueue(m);
            }
            finally
            {
                IsLoadingLibrary = false;
                LoadingLibraryText = string.Empty;
                LoadingLibraryProgress = 0;
            }
        }

        private void LoadAvailableLetters()
        {
            var letters = SortedImportedGames.Cast<StashGame>()
                // Select the first letter from SortName or Name, as a string
                .Select(game => (string.IsNullOrEmpty(game.SortName) ? game.Name : game.SortName).GetFirstLetter<char>())
                .Distinct()
                .OrderBy(c => c, new NaturalStringPropertyComparer<char>());

            Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableLetters.Clear();
                foreach (var l in letters)
                    AvailableLetters.Add(l);
            });
        }

        private void ApplySorting()
        {
            if (SortedImportedGames is ListCollectionView collectionView)
            {
                SortedImportedGames.SortDescriptions.Clear();
                collectionView.CustomSort = null;

                switch (SelectedSortOption)
                {
                    case "Name":
                        //SortedImportedGames.SortDescriptions.Add(new SortDescription(nameof(Game.SortName), SelectedSortDirection));
                        collectionView.CustomSort = new NaturalStringPropertyComparer<StashGame>("SortName");
                        break;
                    case "Release Date":
                        collectionView.SortDescriptions.Add(new SortDescription(nameof(StashGame.FirstReleaseDate), SelectedSortDirection));
                        break;
                    case "Rating":
                        collectionView.SortDescriptions.Add(new SortDescription(nameof(StashGame.Rating), SelectedSortDirection));
                        break;
                    case "Date Added":
                        collectionView.SortDescriptions.Add(new SortDescription(nameof(StashGame.DateAdded), SelectedSortDirection));
                        break;
                    case "Folder Size":
                        collectionView.SortDescriptions.Add(new SortDescription(nameof(StashGame.FolderDiskSpaceBytes), SelectedSortDirection));
                        break;
                    default:
                        collectionView.CustomSort = new NaturalStringPropertyComparer<StashGame>("SortName");
                        break;
                }

                collectionView.Refresh();
                if (SelectedSortOption == "Name")
                    LoadAvailableLetters();
            }
        }

        private void ApplyFiltering()
        {
            SortedImportedGames.Refresh();
            if (SelectedSortOption == "Name")
                LoadAvailableLetters();
        }

        private bool FilterPredicate(object obj)
        {
            if (obj is not StashGame game) return false;

            return SelectedFilterOption switch
            {
                "Favorites" => game.IsFavorite,
                "Not Favorites" => !game.IsFavorite,
                "Installed" => game.IsInstalled,
                "Not Installed" => !game.IsInstalled,
                "Missing" => !game.IsFolderValid,
                "All" or null => true,
                _ => true
            };
        }

        private void ScrollToLetter(char letter)
        {
            ScrollToLetterAction?.Invoke(letter);
        }

        private async Task ImportGamesFromFolderAsync(RootFolder folder)
        {
            try
            {
                MessageQueue.Enqueue($"Importing game folders from {folder.Path}");
                await folder.FindGames();
                var identifiedGames = folder.FoundGames.Where(x => !LocalGamesLibrary.Any(g => g.Folder == x.Folder)).OrderBy(g => g.MatchName);
                foreach (var game in identifiedGames)
                {
                    FoundFolders.Add(game);
                }
            }
            finally
            {
                MessageQueue.Enqueue($"Finished importing game folders from {folder.Path}");
            }
        }

        private void AddRootFolder(string path)
        {
            var rootFolder = new RootFolder(path);
            RootFolders.Add(rootFolder);
            var watcher = new FolderMonitorWithThrottle(path);
            watcher.FolderCreated += Watcher_FolderCreated;
            watcher.FolderRenamed += Watcher_FolderRenamed;
            watcher.FolderDeleted += Watcher_FolderDeleted;
            _rootFolderWatchers.Add(watcher);
            ImportGamesFromFolderAsync(rootFolder);
            UpdateRootFolderEntry();
        }

        private void RemoveRootFolder(RootFolder folder)
        {
            RootFolders.Remove(folder);
            IEnumerable<StashGame> gamesToRemove = LocalGamesLibrary.Where(g => g.RootFolder == folder.Path);
            foreach (var game in gamesToRemove)
            {
                LocalGamesLibrary.Remove(game);
            }
            IEnumerable<StashGame> foldersToRemove = FoundFolders.Where(g => g.RootFolder == folder.Path);
            foreach (var folderToRemove in foldersToRemove)
            {
                FoundFolders.Remove(folderToRemove);
            }
            var watcher = _rootFolderWatchers.First(w => w.MonitoredPath == folder.Path);
            watcher.StopMonitoring();
            _rootFolderWatchers.Remove(watcher);
            UpdateRootFolderEntry();
        }

        private void UpdateRootFolderEntry()
        {
            string combinedPaths = string.Join(";", RootFolders.Select(f => f.Path));
            SqliteHelper.UpdateSettingsEntry("RootFolders", combinedPaths);
        }

        private async Task RefreshAllRootFolders()
        {
            await Task.Run(() => Parallel.ForEach(RootFolders,
                async rootFolder =>
                {
                    var folders = FoundFolders.Where(f => f.RootFolder == rootFolder.Path).ToArray();
                    foreach (var folder in folders)
                    {
                        FoundFolders.Remove(folder);
                    }
                    await ImportGamesFromFolderAsync(rootFolder);
                })
            );
        }

        private void Watcher_FolderDeleted(object? sender, FileSystemEventArgs e)
        {
        }

        private void Watcher_FolderRenamed(object? sender, RenamedEventArgs e)
        {
        }

        private void Watcher_FolderCreated(object? sender, FileSystemEventArgs e)
        {
        }
    }
}
