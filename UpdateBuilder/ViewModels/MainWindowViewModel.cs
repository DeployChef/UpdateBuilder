using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Input;
using UpdateBuilder.Models;
using UpdateBuilder.Properties;
using UpdateBuilder.Utils;
using UpdateBuilder.ViewModels.Base;
using UpdateBuilder.ViewModels.Items;

namespace UpdateBuilder.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private bool _isRoot;
        private bool _inBuilding;
        private string _pathPath;
        private string _outPath;
        private string _totalSize = ((long)0).BytesToString();
        private int _totalCount;
        private int _progressValue;
        private FolderModel _mainFolder;
        private ObservableCollection<FolderItemViewModel> _syncFolder = new ObservableCollection<FolderItemViewModel>();
        private readonly PatchWorker _patchWorker;
        private CancellationTokenSource _cts;

        public ICommand SetPatchPathCommand { get; set; } 
        public ICommand SetOutPathCommand { get; set; }
        public ICommand GoToSiteCommand { get; set; }
        public ICommand ClearLogCommand { get; set; }
        public ICommand BuildUpdateCommand { get; set; }

        public ICommand SyncCommand { get; set; }

        public bool IsRoot
        {
            get => _isRoot;
            set => SetProperty(ref _isRoot, value);
        }

        public string PatchPath
        {
            get => _pathPath;
            set
            {
                if (SetProperty(ref _pathPath, value))
                {
                    Settings.Default.PatchPath = value;
                    Settings.Default.Save();
                    LoadInfoAsync();
                }
                   
            }
        }
        public string OutPath
        {
            get => _outPath;
            set
            {
                if (SetProperty(ref _outPath, value))
                {
                    Settings.Default.OutPath = value;
                    Settings.Default.Save();
                    SyncInfoAsync();
                }
                 
            }
        }

        public ObservableCollection<FolderItemViewModel> SyncFolder
        {
            get => _syncFolder;
            set => SetProperty(ref _syncFolder, value);
        }

        public string TotalSize
        {
            get => _totalSize;
            set => SetProperty(ref _totalSize, value);
        }

        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        public int ProgressValue
        {
            get => _progressValue;
            set
            {
                SetProperty(ref _progressValue, value);
                RaisePropertyChanged(()=> ProgressProcent);
            }
        }

        public string ProgressProcent
        {
            get
            {
                if (TotalCount == 0)
                    return "100%";
                return ProgressValue * 100 / TotalCount + "%";
            }
        }

        public bool InBuilding
        {
            get => _inBuilding;
            set => SetProperty(ref _inBuilding, value);
        }


        public bool CanSync => !string.IsNullOrEmpty(PatchPath) && !string.IsNullOrEmpty(OutPath) && _mainFolder != null;

        public MainWindowViewModel()
        {
            _patchWorker = new PatchWorker();
            _patchWorker.ProgressChanged += (sender, args) => ProgressValue++; 
            SetPatchPathCommand = new RelayCommand(o => { PatchPath = GetPath();}, can => !InBuilding);
            SetOutPathCommand = new RelayCommand(o => { OutPath = GetPath(); }, can => !InBuilding);
            GoToSiteCommand = new RelayCommand(o => { Process.Start(@"http:\\upnova.ru"); });
            ClearLogCommand = new RelayCommand(o => { Logger.Instance.Clear(); });
            SyncCommand = new RelayCommand(o => { LoadInfoAsync(); }, can => !IsBusy && CanSync);
            BuildUpdateCommand = new RelayCommand(o => BuildUpdateAsync(), can => !IsBusy && !string.IsNullOrWhiteSpace(PatchPath) && !string.IsNullOrWhiteSpace(OutPath));
           
            Logger.Instance.Add("Ready to work");
            PatchPath = Settings.Default.PatchPath;
            OutPath = Settings.Default.OutPath;
        }
        private async void LoadInfoAsync()
        {
            IsBusy = true;

            _cts?.Cancel(true);
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _mainFolder = null;
            TotalCount = 0;
            TotalSize = "0";
            ProgressValue = 0;
            Logger.Instance.Clear();

            _mainFolder = await _patchWorker.GetFolderInfoAsync(PatchPath, token);

            if (_mainFolder != null)
            {
                CreateSyncFolder(_mainFolder);
            }
          

            IsBusy = false;
            var cancel = _cts.IsCancellationRequested;
            _cts = null;

            CommandManager.InvalidateRequerySuggested();

            if (!cancel)
                SyncInfoAsync();
        }


        private async void SyncInfoAsync()
        {
            if(!CanSync || IsBusy)
                return;

            IsBusy = true;

            _cts?.Cancel(true);
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                Logger.Instance.Add("Начинаем синхронизацию...");

                var patchInfoPath = Path.Combine(OutPath, "UpdateInfo.xml");
                if (File.Exists(patchInfoPath))
                {
                    var syncFolder = await _patchWorker.SyncUpdateInfoAsync(_mainFolder, patchInfoPath, token);

                    CreateSyncFolder(syncFolder);
                }
                else
                {
                    Logger.Instance.Add("Файлов предыдущего патча не найдено");
                }
            }
            catch (Exception e)
            {
                Logger.Instance.Add("Во время синхронизации произошла ошибка");
                Logger.Instance.Add(e.Message);
            }

            Logger.Instance.Add("Конец синхронизации");
            IsBusy = false;
            _cts = null;

            CommandManager.InvalidateRequerySuggested();
        }

        private void CreateSyncFolder(FolderModel syncF)
        {
            SyncFolder.Clear();
            var syncFolder = new FolderItemViewModel(syncF);
            TotalCount = syncFolder.GetCount();
            TotalSize = syncFolder.GetSize().BytesToString();
            SyncFolder.Add(syncFolder);
            ProgressValue = TotalCount;
        }


        private async void BuildUpdateAsync()
        {
            IsBusy = true;
            InBuilding = true;

            _cts?.Cancel(true);
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Logger.Instance.Clear();
            ProgressValue = 0;

            var rootFolder = SyncFolder.FirstOrDefault();
            if (rootFolder != null)
            {
                var updateInfoAll = new UpdateInfoModel()
                {
                    Folder = rootFolder.ToModel(),
                };

                var updateInfo = new UpdateInfoModel()
                {
                    Folder = rootFolder.ToUnDeletedModel(),
                };

                var result = await _patchWorker.BuildUpdateAsync(updateInfoAll, updateInfo, OutPath, token);

                if (!token.IsCancellationRequested && result)
                {
                    Logger.Instance.Add("--------------------------------------------");
                    Logger.Instance.Add("--------------ПАТЧ-ГОТОВ!------------");
                    Logger.Instance.Add("--------------------------------------------");
                    Process.Start("explorer", OutPath);
                }
            }
            else
            {
                Logger.Instance.Add("КОРНЕВОЙ ПАПКИ НЕТ!");
            }
            ProgressValue = TotalCount;

            InBuilding = false;
            IsBusy = false;
            _cts = null;

            CommandManager.InvalidateRequerySuggested();
        }

        private string GetPath()
        {
            var dialog = new FolderBrowserDialog();
            return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : string.Empty;
        }
    }
}