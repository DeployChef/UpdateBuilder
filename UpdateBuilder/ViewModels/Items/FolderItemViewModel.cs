using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls.Primitives;
using UpdateBuilder.Models;
using UpdateBuilder.Utils;
using UpdateBuilder.ViewModels.Base;

namespace UpdateBuilder.ViewModels.Items
{
    public class FolderItemViewModel : ItemViewModel
    {
        private bool _quickUpdate;
        private bool _checkHash;
        private ModifyType _modifyType;
        private ObservableCollection<FileItemViewModel> _files;
        private ObservableCollection<FolderItemViewModel> _folders;
        private ObservableCollection<ItemViewModel> _childrens;

        public bool Updating { get; set; }

        public ModifyType ModifyType
        {
            get => _modifyType;
            set => SetProperty(ref _modifyType, value);
        }

        public ObservableCollection<FileItemViewModel> Files
        {
            get => _files;
            set => SetProperty(ref _files, value);
        }

        public ObservableCollection<FolderItemViewModel> Folders
        {
            get => _folders;
            set => SetProperty(ref _folders, value);
        }

        public ObservableCollection<ItemViewModel> Childrens
        {
            get => _childrens;
            set => SetProperty(ref _childrens, value);
        }

        public bool QuickUpdate
        { 
            get => _quickUpdate;
            set
            {
                SetProperty(ref _quickUpdate, value);
                if(!Updating)
                    SetRecurceQuickUpdate(this, value);
            }
        }

        public bool CheckHash
        {
            get => _checkHash;
            set
            {
                SetProperty(ref _checkHash, value);
                SetRecurseCheckHash(this, value);
            }
        }

        private void SetRecurceQuickUpdate(FolderItemViewModel rootFolder, bool value)
        {
            foreach (var folder in rootFolder.Folders)
            {
                folder.QuickUpdate = value;
                SetRecurceQuickUpdate(folder, value);
            }

            foreach (var file in rootFolder.Files)
            {
                file.QuickUpdate = value;
            }
        }

        private void SetRecurseCheckHash(FolderItemViewModel rootFolder, bool value)
        {
            foreach (var folder in rootFolder.Folders)
            {
                folder.CheckHash = value;
                SetRecurseCheckHash(folder, value);
            }

            foreach (var file in rootFolder.Files)
            {
                file.CheckHash = value;
            }
        }


        public FolderItemViewModel(FolderModel model)
        {
            Name = model.Name;
            Files = new ObservableCollection<FileItemViewModel>(model.Files.Select(c => new FileItemViewModel(c)));
            Folders = new ObservableCollection<FolderItemViewModel>(model.Folders.Select(c => new FolderItemViewModel(c)));
            Childrens = new ObservableCollection<ItemViewModel>();
            Childrens.AddRange(Folders);
            Childrens.AddRange(Files);
            _checkHash = GetCheckHashRecurse(this);
            RaisePropertyChanged(nameof(CheckHash));
            _quickUpdate = GetQuickUpdateRecurse(this);
            RaisePropertyChanged(nameof(QuickUpdate));
            ModifyType = GetModifyType(this);
        }

        public int GetCount()
        {
            return GetCountRecurce(this);
        }

        public long GetSize()
        {
           return GetSizeRecurce(this);
        }

        private ModifyType GetModifyType(FolderItemViewModel rootFolder)
        {
            var modifyTypes = GetModifyTypeRecurse(rootFolder);
            var modyfyGroups = modifyTypes.GroupBy(c => c).ToArray();
            return modyfyGroups.Length == 1 ? modyfyGroups.First().Key : ModifyType.Modified;
        }
        private List<ModifyType> GetModifyTypeRecurse(FolderItemViewModel rootFolder)
        {
            var modifyTypes = rootFolder.Folders.SelectMany(GetModifyTypeRecurse).ToList();
            modifyTypes.AddRange(rootFolder.Files.Select(c => c.ModifyType));
            return modifyTypes;
        }

        private bool GetCheckHashRecurse(FolderItemViewModel rootFolder)
        {
            var checkHash = rootFolder.Folders.All(GetCheckHashRecurse);
            return checkHash && rootFolder.Files.All(c => c.CheckHash);
        }

        private bool GetQuickUpdateRecurse(FolderItemViewModel rootFolder)
        {
            var quickUpdate = rootFolder.Folders.All(GetQuickUpdateRecurse);
            return quickUpdate && rootFolder.Files.All(c => c.QuickUpdate);
        }

        private int GetCountRecurce(FolderItemViewModel rootFolder)
        {
            var count = rootFolder.Folders.Sum(GetCountRecurce);
            count += rootFolder.Files.Count;
            return count;
        }

        private long GetSizeRecurce(FolderItemViewModel rootFolder)
        {
            var count = rootFolder.Folders.Sum(folder => GetSizeRecurce(folder));
            count += rootFolder.Files.Sum(c=>c.Size);
            return count;
        }

        private FolderModel GetFolderRecurce(FolderItemViewModel rootFolder)
        {
            var folderModel = new FolderModel{ Name = rootFolder.Name };
            foreach (var folder in rootFolder.Folders)
            {
                folderModel.Folders.Add(GetFolderRecurce(folder));
            }
            foreach (var file in rootFolder.Files)
            {
                folderModel.Files.Add(file.ToModel());
            }
            return folderModel;
        }

        private FolderModel GetFolderUnDeletedRecurce(FolderItemViewModel rootFolder)
        {
            var folderModel = new FolderModel { Name = rootFolder.Name };
            foreach (var folder in rootFolder.Folders.Where(c => c.ModifyType != ModifyType.Deleted))
            {
                folderModel.Folders.Add(GetFolderUnDeletedRecurce(folder));
            }
            foreach (var file in rootFolder.Files.Where(c => c.ModifyType != ModifyType.Deleted))
            {
                folderModel.Files.Add(file.ToModel());
            }
            return folderModel;
        }

        public FolderModel ToModel()
        {
            return GetFolderRecurce(this);
        }

        public FolderModel ToUnDeletedModel()
        {
            return GetFolderUnDeletedRecurce(this);
        }
    }
}
