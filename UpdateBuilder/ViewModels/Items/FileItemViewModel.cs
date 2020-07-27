using UpdateBuilder.Models;
using UpdateBuilder.ViewModels.Base;

namespace UpdateBuilder.ViewModels.Items
{
    public class FileItemViewModel : ItemViewModel
    { 
        private long _size;
        private string _hash;
        private bool _quickUpdate;
        private bool _checkHash;

        public string FullPath { get; set; }

        public string Path { get; set; }

        public long Size
        {
            get => _size;
            set => SetProperty(ref _size, value);
        }

        public string Hash
        {
            get => _hash;
            set => SetProperty(ref _hash, value);
        }

        public bool QuickUpdate
        {
            get => _quickUpdate;
            set => SetProperty(ref _quickUpdate, value);
        }

        public bool CheckHash
        {
            get => _checkHash;
            set => SetProperty(ref _checkHash, value);
        }

        public FileItemViewModel(FileModel model)
        {
            Name = model.Name;
            Size = model.Size;
            Hash = model.Hash;
            QuickUpdate = true;
            CheckHash = true;
            FullPath = model.FullPath;
            Path = model.Path;
        }

        public FileModel ToModel()
        {
            return new FileModel()
            {
                Name = Name,
                Size = Size,
                Hash = Hash,
                QuickUpdate = QuickUpdate,
                CheckHash = CheckHash,
                FullPath = FullPath,
                Path = Path
            };
        }
    }
}
