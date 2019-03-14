﻿using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Input;
using UpdateBuilder.Models;
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
        private string _totalSize;
        private int _totalCount;
        private int _progressValue;
        private ObservableCollection<FolderItemViewModel> _mainFolder = new ObservableCollection<FolderItemViewModel>();
        private readonly PatchWorker _patchWorker;
        private CancellationTokenSource _cts;

        public RelayCommand SetPatchPathCommand { get; set; } 
        public RelayCommand SetOutPathCommand { get; set; }
        public RelayCommand GoToSiteCommand { get; set; }
        public RelayCommand ClearLogCommand { get; set; }
        public RelayCommand BuildUpdateCommand { get; set; }

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
                SetProperty(ref _pathPath, value);
                LoadInfoAsync();
            }
        }
        public string OutPath
        {
            get => _outPath;
            set => SetProperty(ref _outPath, value);
        }

        public ObservableCollection<FolderItemViewModel> MainFolder
        {
            get => _mainFolder;
            set => SetProperty(ref _mainFolder, value);
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


        public MainWindowViewModel()
        {
            _patchWorker = new PatchWorker();
            _patchWorker.ProgressChanged += (sender, args) => ProgressValue++; 
            SetPatchPathCommand = new RelayCommand(o => { PatchPath = GetPath();}, can => !InBuilding);
            SetOutPathCommand = new RelayCommand(o => { OutPath = GetPath(); }, can => !InBuilding);
            GoToSiteCommand = new RelayCommand(o => { Process.Start(@"http:\\upnova.ru"); });
            ClearLogCommand = new RelayCommand(o => { Logger.Instance.Clear(); });
            BuildUpdateCommand = new RelayCommand(o => BuildUpdateAsync(), can => !IsBusy && !string.IsNullOrWhiteSpace(PatchPath) && !string.IsNullOrWhiteSpace(OutPath));
           
            Logger.Instance.Add("Ready to work");
            //PatchPath = @"D:\BuilderTest\UpdateFiles";
            //OutPath = @"D:\BuilderTest\Out";
        }
        private async void LoadInfoAsync()
        {
            IsBusy = true;

            _cts?.Cancel(true);
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            MainFolder.Clear();
            TotalCount = 0;
            TotalSize = "0";
            ProgressValue = 0;
            Logger.Instance.Clear();

            var rootF = await _patchWorker.GetFolderInfoAsync(PatchPath, token);

            if (rootF != null)
            {
                var rootFolder = new FolderItemViewModel(rootF);
                TotalCount = rootFolder.GetCount();
                TotalSize = rootFolder.GetSize().BytesToString();
                MainFolder.Add(rootFolder);
                ProgressValue = TotalCount;
            }

            IsBusy = false;
            _cts = null;

            CommandManager.InvalidateRequerySuggested();
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

            var rootFolder = MainFolder.FirstOrDefault();
            if (rootFolder != null)
            {
                var updateInfo = new UpdateInfoModel()
                {
                    Folder = rootFolder.ToModel(),
                    Version = 1
                };

                var result = await _patchWorker.BuildUpdateAsync(updateInfo, OutPath, token);

                if (!token.IsCancellationRequested || result)
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