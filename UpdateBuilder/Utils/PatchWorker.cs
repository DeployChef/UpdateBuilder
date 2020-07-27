using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using UpdateBuilder.Models;
using UpdateBuilder.ViewModels.Items;
using UpdateBuilder.ZIPLib;
using UpdateBuilder.ZIPLib.Zip;

namespace UpdateBuilder.Utils
{
    public class PatchWorker
    {
        private readonly Crc32 _hashCalc = new Crc32();
        public event EventHandler ProgressChanged;

        public async Task<FolderModel> GetFolderInfoAsync(string path, CancellationToken token)
        {
            return await Task.Run(() => {
                try
                {
                    Logger.Instance.Add("Проверяем корневую папку");
                    if (!Directory.Exists(path))
                    {
                        Logger.Instance.Add("Папка отсутствует");
                        return null;
                    }
                    Logger.Instance.Add("Все на месте, начинаем поиск файлов и папок");
                    var rootDir = new DirectoryInfo(path);
                    var tree = GetTreeRecurse(rootDir, token);
                    Logger.Instance.Add("Все успешно загружено");
                    return tree;
                }
                catch (Exception e)
                {
                    Logger.Instance.Add($"Произошла ошибка во время чтения файлов [{e.Message}]");
                    return null;
                }
            });
        }


        private FolderModel GetTreeRecurse(DirectoryInfo rootDir, CancellationToken token, string path = "")
        {
            token.ThrowIfCancellationRequested();
            Logger.Instance.Add($"Погружаемся в {rootDir}");

            var folder = new FolderModel
            {
                Name = rootDir.Name,
                Path = Path.Combine(path, rootDir.Name)
            };

            Logger.Instance.Add($"Проверяем исть ли подпапки");
            foreach (var dir in rootDir.EnumerateDirectories())
            {
                folder.Folders.Add(GetTreeRecurse(dir, token, folder.Path));
                Logger.Instance.Add($"Добавили {dir} в {folder.Name}");
            }

            Logger.Instance.Add($"Проверяем исть ли файлы");
            foreach (var file in rootDir.EnumerateFiles())
            {
                token.ThrowIfCancellationRequested();
                var hash = _hashCalc.Get(file.FullName);
                folder.Files.Add(new FileModel() { Name = file.Name, Hash = hash,
                    Size = file.Length, FullPath = file.FullName, Path = folder.Path});
                Logger.Instance.Add($"Добавили {file} в {folder.Name}");
            }
            Logger.Instance.Add($"Поднимаемся из {rootDir}");
            return folder;
        }


        public async Task<bool> BuildUpdateAsync(UpdateInfoModel updateInfo, string outPath, CancellationToken token)
        {
            return await Task.Run(() => {
                try
                {
                    token.ThrowIfCancellationRequested();
                    Logger.Instance.Add("Создаем патч лист");

                    BuildUpdateInfo(updateInfo, outPath);

                    Logger.Instance.Add("Патч лист создан");

                    Logger.Instance.Add("Начинаем паковать");

                    PuckingRecurse(updateInfo.Folder, $"{outPath}\\{updateInfo.Folder.Name}", token);

                    Logger.Instance.Add("Все запаковано");
                    return true;
                }
                catch (Exception e)
                {
                    Logger.Instance.Add($"Произошла ошибка во время создания апдейта [{e.Message}]");
                    return false;
                }
            });
        }

        private void PuckingRecurse(FolderModel rootFolder, string outPath, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
           
            foreach (var folder in rootFolder.Folders)
            {
                var folderPath = $"{outPath}\\{folder.Name}";

                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                PuckingRecurse(folder, folderPath, token);
            }

            foreach (var file in rootFolder.Files)
            { 
                try
                {
                    var filePath = $"{outPath}\\{file.Name}";

                    Logger.Instance.Add($"Проверяем {file.FullPath}");

                    if(!File.Exists(file.FullPath))
                        throw new Exception("Файл отсутствует");

                    Logger.Instance.Add($"{file.FullPath} на месте");

                    Logger.Instance.Add($"Пакуем {file.Name}");
                   
                    using (var zip = new ZipFile{ CompressionLevel = CompressionLevel.BestCompression}) // Создаем объект для работы с архивом
                    {
                        zip.AddFile(file.FullPath,"");
                        zip.Save(filePath + ".zip");                           
                    }

                    Logger.Instance.Add($"Запаковано {file.Name}");
                }
                catch (Exception e)
                {
                    Logger.Instance.Add($"Не удалось запаковать {file.Name}, причина [{e.Message}]");
                }
                OnProgressChanged();
            }
        }

        private static void BuildUpdateInfo(UpdateInfoModel updateInfo, string outPath)
        {
            var serializer = new XmlSerializer(typeof(UpdateInfoModel));
            var settings = new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 };

            using (var w = XmlWriter.Create($"{outPath}\\UpdateInfo.xml", settings))
                serializer.Serialize(w, updateInfo);
        }

        protected virtual void OnProgressChanged()
        {
            ProgressChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task<FolderModel> SyncUpdateInfoAsync(FolderModel mainFolder, string patchInfoPath, CancellationToken token)
        {
            return await Task.Run(() => {

                var patchInfo = DeserializeUpdateInfo(patchInfoPath);
                Logger.Instance.Add($"Данные о прошлом патче получены");

                var syncFolder = SyncFolder(patchInfo.Folder, mainFolder);


                return syncFolder;

            }, token);
        }

        private FolderModel SyncFolder(FolderModel patchInfoFolder, FolderModel mainFolder)
        {
            FolderModel syncFolder;
            if (patchInfoFolder.Name.Equals(mainFolder.Name))
            {
                syncFolder = new FolderModel { Name = mainFolder.Name, ModifyType = mainFolder.ModifyType, Path = mainFolder.Path };
                SyncFolderRecurse(patchInfoFolder, mainFolder, syncFolder);
                SyncFolderRecurse(mainFolder, patchInfoFolder, syncFolder);
            }
            else
            {
                syncFolder = new FolderModel { Name = "UpdateDiff" };
                var syncPatchFolder = new FolderModel {Name = patchInfoFolder.Name, ModifyType = ModifyType.Deleted, Path = patchInfoFolder.Path};
                var syncMainFolder = new FolderModel {Name = mainFolder.Name, ModifyType = mainFolder.ModifyType, Path = mainFolder.Path};
                syncFolder.Folders.Add(syncPatchFolder);
                syncFolder.Folders.Add(syncMainFolder);
                SyncFolderRecurse(patchInfoFolder, null, syncPatchFolder);
                SyncFolderRecurse(mainFolder, patchInfoFolder, syncMainFolder);
            }
            return syncFolder;
        }

        private void SyncFolderRecurse(FolderModel masterFolders, FolderModel slaveFolders, FolderModel syncFolderModel)
        {
            foreach (var masterFolder in masterFolders.Folders)
            {
                if (slaveFolders != null)
                {
                    var sameSlaveFolder = slaveFolders.Folders.FirstOrDefault(c => c.Name.Equals(masterFolder.Name));

                    var syncFolder = syncFolderModel.Folders.FirstOrDefault(c => c.Name.Equals(masterFolder.Name));
                    if (syncFolder == null)
                    {
                        syncFolder = CreateSyncFolder(masterFolder, sameSlaveFolder);
                        syncFolderModel.Folders.Add(syncFolder);
                    }

                    foreach (var patchFolderFile in masterFolder.Files)
                    {
                        var sameMainFile = slaveFolders.Files.FirstOrDefault(c => c.Name.Equals(masterFolder.Name));
                        var syncFileInfo = CreateSyncFile(patchFolderFile, sameMainFile);
                        syncFolder.Files.Add(syncFileInfo);
                    }

                    SyncFolderRecurse(masterFolder, sameSlaveFolder, syncFolder);
                }
                else
                {
                    masterFolder.ModifyType = ModifyType.Deleted;
                    var syncFolder = syncFolderModel.Folders.FirstOrDefault(c => c.Name.Equals(masterFolder.Name));
                    if (syncFolder == null)
                    {
                        syncFolder = new FolderModel { Name = masterFolder.Name, ModifyType = masterFolder.ModifyType, Path = masterFolder.Path };
                        syncFolderModel.Folders.Add(syncFolder);
                    }

                    foreach (var patchFolderFile in masterFolder.Files)
                    {
                        syncFolder.Files.Add(new FileModel(){Name = patchFolderFile.Name, ModifyType = ModifyType.Deleted});
                    }

                    SyncFolderRecurse(masterFolder, null, syncFolder);
                }
            }
        }

        private FolderModel CreateSyncFolder(FolderModel masterFolder, FolderModel slaveFolder)
        {
            if (slaveFolder != null)
            {
                return new FolderModel{Name = slaveFolder.Name, ModifyType = slaveFolder.ModifyType, Path = slaveFolder.Path};
            }
            else
            {
                masterFolder.ModifyType = ModifyType.Deleted;
                return new FolderModel { Name = masterFolder.Name, ModifyType = masterFolder.ModifyType, Path = masterFolder.Path };
            }
        }

        private FileModel CreateSyncFile(FileModel masterFile, FileModel slaveFile)
        {
            if (slaveFile != null)
            {
                slaveFile.Sync = true;
                return new FileModel { Name = slaveFile.Name, ModifyType = slaveFile.ModifyType, Path = slaveFile.Path };
            }
            else
            {
                masterFile.ModifyType = ModifyType.Deleted;
                return new FileModel { Name = masterFile.Name, ModifyType = masterFile.ModifyType, Path = masterFile.Path };
            }
        }

        private UpdateInfoModel DeserializeUpdateInfo(string patchInfoPath)
        {
            var serializer = new XmlSerializer(typeof(UpdateInfoModel));
            using (var reader = new StreamReader(File.OpenRead(patchInfoPath)))
            {
                return (UpdateInfoModel)serializer.Deserialize(reader);
            }
        }
    }
}
