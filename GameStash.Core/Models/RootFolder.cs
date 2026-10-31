using Common.Lib.HelperClasses;
using GameStash.Core.Services;

namespace GameStash.Core.Models
{
    public class RootFolder : ObservableObject
    {
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

        private long _usedSpace;
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


        public IEnumerable<Game> _foundGames;
        public IEnumerable<Game> FoundGames
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

        private long _foundGamesCount;
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
            UpdateSpace();
        }

        // Método para atualizar o espaço da pasta (caso necessário)
        public async Task UpdateSpace()
        {
            if (Directory.Exists(Path))
            {
                await Task.Run(() =>
                {
                    // Obter o diretório da pasta (unidade) para usar com DriveInfo
                    var drive = new DriveInfo(Path);

                    // Verificar se a unidade é acessível
                    if (drive.IsReady)
                    {
                        FreeSpace = drive.AvailableFreeSpace;  // Espaço livre em bytes
                        UsedSpace = drive.TotalSize - FreeSpace; // Espaço utilizado em bytes
                    }
                });                
            }
        }

        public async Task FindGames()
        {
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
