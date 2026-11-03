using Common.Lib.HelperClasses;
using GameStash.Core.Services;

namespace GameStash.Core.Models
{
    public class RootFolder : ObservableObject
    {
        private int _id;
        public int Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
                    RaisePropertyChanged();
                }
            }
        }

        private string _path;
        public string Path
        {
            get => _path;
            set
            {
                if (_path != value)
                {
                    _path = value;
                    RaisePropertyChanged();
                }
            }
        }

        private long _usedSpace =0;
        public long UsedSpace
        {
            get => _usedSpace;
            set
            {
                if (_usedSpace != value)
                {
                    _usedSpace = value;
                    RaisePropertyChanged();
                }
            }
        }

        private long _freeSpace;
        public long FreeSpace
        {
            get => _freeSpace;
            set
            {
                if (_freeSpace != value)
                {
                    _freeSpace = value;
                    RaisePropertyChanged();
                }
            }
        }


        public IEnumerable<StashGame> _foundGames;
        public IEnumerable<StashGame> FoundGames
        {
            get => _foundGames;
            set
            {
                if (_foundGames != value)
                {
                    _foundGames = value;
                    RaisePropertyChanged();
                }
            }
        }

        private long _foundGamesCount = 0;
        public long FoundGamesCount
        {
            get => _foundGamesCount;
            set
            {
                if (_foundGamesCount != value)
                {
                    _foundGamesCount = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool _isImportingFolders;
        public bool IsImportingFolders
        {
            get => _isImportingFolders;
            set
            {
                if (_isImportingFolders != value)
                {
                    _isImportingFolders = value;
                    RaisePropertyChanged();
                }
            }
        }

        public RootFolder(string path)
        {
            Path = path;
            _ = UpdateSpace();
        }

        public async Task UpdateSpace()
        {
            if (Directory.Exists(Path))
            {
                await Task.Run(() =>
                {
                    var drive = new DriveInfo(Path);

                    if (drive.IsReady)
                    {
                        FreeSpace = drive.AvailableFreeSpace;  // bytes
                        UsedSpace = drive.TotalSize - FreeSpace; // bytes
                    }
                });                
            }
        }

        public async Task FindGames()
        {
            FoundGames = null;
            IsImportingFolders = true;
            try
            {
                FoundGamesCount = 0;
                var result = await new GameScanner().ScanFolderAsync(Path);
                FoundGames = await new GameIdentifier().IdentifyGamesAsync(result);
                FoundGamesCount = FoundGames.Count();
                Parallel.ForEach(FoundGames, game => game.RootFolder = Path);
            }
            finally
            {
                IsImportingFolders = false;
            }
        }
    }
}
