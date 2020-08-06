using System;
using System.Collections.ObjectModel;
using UpdateBuilder.Models;
using UpdateBuilder.ViewModels.Base;

namespace UpdateBuilder.ViewModels.Items
{
    public class FileUpdateItemViewModel : ItemViewModel
    { 
        private long _size;
        private string _hash;

        private ModifyType _modifyType;

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

        public ModifyType ModifyType
        {
            get => _modifyType;
            set => SetProperty(ref _modifyType, value);
        }

        public int Version { get; set; }

        public FileUpdateItemViewModel(FileUpdateModel model)
        {
            Name = model.Name;
            Size = model.Size;
            Hash = model.Hash;
            ModifyType = model.ModifyType;
            Version = model.Version;
        }

        public FileUpdateModel ToModel()
        {
            return new FileUpdateModel
            {
                Name = Name,
                Size = Size,
                Hash = Hash,
                ModifyType = ModifyType,
                Version = Version
            };
        }
    }
}
