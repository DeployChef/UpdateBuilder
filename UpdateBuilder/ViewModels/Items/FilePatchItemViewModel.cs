using System;
using System.Collections.ObjectModel;
using UpdateBuilder.Models;
using UpdateBuilder.ViewModels.Base;

namespace UpdateBuilder.ViewModels.Items
{
    public class FilePatchItemViewModel : ItemViewModel
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

        public FilePatchItemViewModel(FilePatchModel model)
        {
            Name = model.Name;
            Size = model.Size;
            Hash = model.Hash;
            ModifyType = model.ModifyType;
            Version = model.Version;
        }

        public FilePatchModel ToModel()
        {
            return new FilePatchModel
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
