using Common.Lib.Extensions;
using GameStash.Core.Extensions;

namespace GameStash.Core.Models
{
    public class LocalStashGame : StashGame
    {
        private string _packageType;
        public string PackageType
        {
            get => _packageType;
            set => _packageType = value;
        }

        private string _rootFolder;
        public string RootFolder
        {
            get => _rootFolder;
            set => _rootFolder = value;
        }

        private string _folder;
        public string Folder
        {
            get => _folder;
            set
            {
                if (_folder != value)
                {
                    _folder = value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        CheckIsFolderValid();
                        if (IsFolderValid)
                        {
                            try
                            {
                                FolderDiskSpaceBytes = new DirectoryInfo(value).GetDirectorySize(false);
                            }
                            catch
                            {
                                FolderDiskSpaceBytes = 0;
                            }
                        }
                    }
                }
            }
        }

        private long _folderDiskSpaceBytes;
        public long FolderDiskSpaceBytes
        {
            get => _folderDiskSpaceBytes;
            set => _folderDiskSpaceBytes = value;
        }

        private DateTime? _dateAdded;
        public DateTime? DateAdded
        {
            get => _dateAdded;
            set => _dateAdded = value;
        }

        private bool _isFolderValid;
        public bool IsFolderValid
        {
            get
            {
                CheckIsFolderValid();
                return _isFolderValid;
            }
            set => _isFolderValid = value;
        }

        public void CheckIsFolderValid()
        {
            IsFolderValid = !string.IsNullOrEmpty(Folder) && Directory.Exists(Folder);
        }
    }
}
