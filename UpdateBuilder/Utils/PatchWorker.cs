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
using RsyncNet.Delta;
using RsyncNet.Hash;
using RsyncNet.Libraries;
using UpdateBuilder.Models;
using UpdateBuilder.ViewModels.Items;
using UpdateBuilder.ZIPLib;
using UpdateBuilder.ZIPLib.Zip;

namespace UpdateBuilder.Utils
{
    public class PatchWorker
    {
        public event EventHandler ProgressChanged;
        public const int BLOCK_SIZE = 1024;

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
                var hash = GetMD5HashFromFile(file.FullName);
                folder.Files.Add(new FileModel() { Name = file.Name, Hash = hash,
                    Size = file.Length, FullPath = file.FullName, Path = folder.Path});
                Logger.Instance.Add($"Добавили {file} в {folder.Name}");
            }
            Logger.Instance.Add($"Поднимаемся из {rootDir}");
            return folder;
        }


        public async Task<bool> BuildUpdateAsync(UpdateInfoModel updateInfoAll, UpdateInfoModel updateInfo, string outPath, CancellationToken token)
        {
            return await Task.Run(() => {
                try
                {
                    token.ThrowIfCancellationRequested();
                    Logger.Instance.Add("Создаем патч лист");

                    BuildUpdateInfo(updateInfo, outPath);

                    Logger.Instance.Add("Патч лист создан");

                    Logger.Instance.Add("Начинаем паковать");

                    ProceedUpdateRecurse(updateInfoAll.Folder, $"{outPath}\\{updateInfoAll.Folder.Name}", token);

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

        private void ProceedUpdateRecurse(FolderModel rootFolder, string outPath, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
           
            foreach (var folder in rootFolder.Folders)
            {
                var folderPath = Path.Combine(outPath, folder.Name);

                if (Directory.Exists(folderPath) && folder.ModifyType == ModifyType.Deleted)
                {
                    Directory.Delete(folderPath, true);
                }
                else
                {
                    Directory.CreateDirectory(folderPath);
                }

                ProceedUpdateRecurse(folder, folderPath, token);
            }

            foreach (var file in rootFolder.Files.Where(c => c.ModifyType == ModifyType.Deleted))
            {
                var filePath = Path.Combine(outPath, file.Name + ".file");

                Logger.Instance.Add($"Удаляем {filePath}");

                if (Directory.Exists(filePath))
                    Directory.Delete(filePath, true);
            }

            foreach (var file in rootFolder.Files.Where(c => c.ModifyType == ModifyType.NotModified))
            {
                Logger.Instance.Add($"Ничего не делаем с {file.FullPath}");
            }

            foreach (var file in rootFolder.Files.Where(c => c.ModifyType == ModifyType.New))
            { 
                try
                {
                    var fileFolder = Path.Combine(outPath, file.Name + ".file");
                    var filePath = Path.Combine(fileFolder, file.Name + ".origin");

                    Logger.Instance.Add($"Проверяем {file.FullPath}");

                    if(!File.Exists(file.FullPath))
                        throw new Exception("Файл отсутствует");

                    Logger.Instance.Add($"{file.FullPath} на месте");

                    if (!Directory.Exists(fileFolder))
                        Directory.CreateDirectory(fileFolder);

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

            foreach (var file in rootFolder.Files.Where(c => c.ModifyType == ModifyType.Modified))
            {
                try
                {
                  
                    var fileFolder = Path.Combine(outPath, file.Name + ".file");
                    var tempFileFolder = Path.Combine(fileFolder, "Temp");
                  

                    Logger.Instance.Add($"Проверяем {file.FullPath}");

                    if (!File.Exists(file.FullPath))
                        throw new Exception("Файл отсутствует");


                    Logger.Instance.Add($"{file.FullPath} на месте");

                    CreateTempFolder(tempFileFolder);

                    var lastPatch = file.FilePatches.Last();
                    var patchFullPath = Path.Combine(fileFolder, lastPatch.Name);

                    Logger.Instance.Add($"Создаем патч {lastPatch.Name} для {file.Name}");

                    Logger.Instance.Add($"Собираем последнюю версию {file.Name} во временную папку");

                    CreateTempFile(tempFileFolder, fileFolder, file, token);

                    var patch = CreatePatchFor(Path.Combine(tempFileFolder, file.Name), file.FullPath);

                    using (var zip = new ZipFile { CompressionLevel = CompressionLevel.BestCompression }) // Создаем объект для работы с архивом
                    {
                        zip.AddEntry(patchFullPath, "", patch);
                        zip.Save(patchFullPath + ".zip");
                    }

                    Logger.Instance.Add($"Патч запакован {file.Name}");

                    Logger.Instance.Add($"Подчищаем временные файлы {tempFileFolder}");
                    if (Directory.Exists(Path.Combine(tempFileFolder)))
                        Directory.Delete(tempFileFolder, true);
                }
                catch (Exception e)
                {
                    Logger.Instance.Add($"Не удалось запаковать {file.Name}, причина [{e.Message}]");
                }
                OnProgressChanged();
            }
        }

        private static void CreateTempFolder(string tempFileFolder)
        {
            Logger.Instance.Add($"Создаем временную папку {tempFileFolder}");

            if (Directory.Exists(Path.Combine(tempFileFolder)))
                Directory.Delete(tempFileFolder, true);

            Directory.CreateDirectory(tempFileFolder);
        }

        private void CreateTempFile(string tempFileFolder, string fileFolder, FileModel file, CancellationToken token)
        {
            var originFilePath = Path.Combine(fileFolder, file.Name + ".origin");
            var tempFilePath = Path.Combine(tempFileFolder, file.Name);

            Unpacking(tempFileFolder, originFilePath, token);

            foreach (var filePatch in file.FilePatches.Where(c=>c.ModifyType != ModifyType.New))
            {
                var zipPatchPath = Path.Combine(fileFolder, filePatch.Name);
                var tempPatchPath = Path.Combine(tempFileFolder, filePatch.Name);
               
                Unpacking(tempFileFolder, zipPatchPath, token);

                var resultPath = Path.Combine(tempFileFolder, "PatchingResult");

                using (FileStream deltaStream = File.Open(tempPatchPath, FileMode.Open))
                using (FileStream sourceStream = File.Open(tempFilePath, FileMode.Open))
                using (FileStream outStream = File.Open(resultPath, FileMode.Create))
                {
                            DeltaStreamer streamer = new DeltaStreamer();
                            streamer.Receive(deltaStream, sourceStream, outStream);
                }

                var md5 = GetMD5HashFromFile(tempFilePath);
                var md5Result = GetMD5HashFromFile(resultPath);
                File.Copy(resultPath, tempFilePath, true);
                File.Delete(resultPath);
            }
        }

        public string GetMD5HashFromFile(string fileName)
        {
            FileStream file = new FileStream(fileName, FileMode.Open);
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] retVal = md5.ComputeHash(file);
            file.Close();

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < retVal.Length; i++)
            {
                sb.Append(retVal[i].ToString("x2"));
            }
            return sb.ToString();
        }

        public byte[] CreatePatchFor(string filename1, string filename2)
        {
            // Compute hashes
            HashBlock[] hashBlocksFromReceiver;
            using (FileStream sourceStream = File.Open(filename1, FileMode.Open))
            {
                hashBlocksFromReceiver = new HashBlockGenerator(new RollingChecksum(),
                    new HashAlgorithmWrapper<MD5>(MD5.Create()),
                    BLOCK_SIZE).ProcessStream(sourceStream).ToArray();
            }

            // Compute deltas
            MemoryStream deltaStream = new MemoryStream();
            using (FileStream fileStream = File.Open(filename2, FileMode.Open))
            {
                DeltaGenerator deltaGen = new DeltaGenerator(new RollingChecksum(), new HashAlgorithmWrapper<MD5>(MD5.Create()));
                deltaGen.Initialize(BLOCK_SIZE, hashBlocksFromReceiver);
                IEnumerable<IDelta> deltas = deltaGen.GetDeltas(fileStream);
                deltaGen.Statistics.Dump();
                fileStream.Seek(0, SeekOrigin.Begin);
                DeltaStreamer streamer = new DeltaStreamer();
                streamer.Send(deltas, fileStream, deltaStream);
            }

            return deltaStream.ToArray();
        }

        private void Unpacking(string fileFolderPath, string filePath, CancellationToken token)
        {
            using (ZipFile zip = ZipFile.Read(filePath + ".zip"))
            {
                foreach (ZipEntry ef in zip)
                {
                    token.ThrowIfCancellationRequested();
                    ef.Extract(fileFolderPath, ExtractExistingFileAction.OverwriteSilently);
                }
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

                var syncFolder = SyncFolder(patchInfo.Folder, mainFolder, token);
                if (syncFolder == null)
                {
                    return mainFolder;
                }

                Logger.Instance.Add("Патч синхронизирован");
                return syncFolder;

            }, token);
        }

        private FolderModel SyncFolder(FolderModel patchInfoFolder, FolderModel mainFolder, CancellationToken token)
        {
            FolderModel syncFolder;
            if (patchInfoFolder.Name.Equals(mainFolder.Name))
            {
                syncFolder = new FolderModel { Name = mainFolder.Name, ModifyType = mainFolder.ModifyType, Path = mainFolder.Path };
                Logger.Instance.Add($"Синхронизация собранного ранее патча с новым...");
                SyncFolderRecurse(patchInfoFolder, mainFolder, syncFolder, false);
                token.ThrowIfCancellationRequested();
                Logger.Instance.Add($"Синхронизировано");

                Logger.Instance.Add($"Синхронизация нового патча с собраным ранее...");
                SyncFolderRecurse(mainFolder, patchInfoFolder, syncFolder, true);
                token.ThrowIfCancellationRequested();
                Logger.Instance.Add($"Синхронизировано");

                SyncFiles(patchInfoFolder, mainFolder, syncFolder, false);
                token.ThrowIfCancellationRequested();

                SyncFiles(mainFolder, patchInfoFolder, syncFolder, true);
                token.ThrowIfCancellationRequested();

                return syncFolder;
            }
            else
            {
                Logger.Instance.Add($"Папки для синхронизации не совпадают");
                return null;
                //syncFolder = new FolderModel { Name = "UpdateDiff" };
                //var syncPatchFolder = new FolderModel {Name = patchInfoFolder.Name, ModifyType = ModifyType.Deleted, Path = patchInfoFolder.Path};
                //var syncMainFolder = new FolderModel {Name = mainFolder.Name, ModifyType = mainFolder.ModifyType, Path = mainFolder.Path};
                //syncFolder.Folders.Add(syncPatchFolder);
                //syncFolder.Folders.Add(syncMainFolder);
                //SyncFolderRecurse(patchInfoFolder, null, syncPatchFolder,true);
                //SyncFolderRecurse(mainFolder, null, syncMainFolder, false);



                //SyncFiles(patchInfoFolder, mainFolder, syncPatchFolder, false);
                //SyncFiles(mainFolder, patchInfoFolder, syncPatchFolder, true);

                //SyncFiles(patchInfoFolder, mainFolder, syncMainFolder, false);
                //SyncFiles(mainFolder, patchInfoFolder, syncMainFolder, true);
            }

     

            return syncFolder;
        }

        private void SyncFolderRecurse(FolderModel masterFolders, FolderModel slaveFolders, FolderModel syncFolderModel, bool reverse)
        {
            foreach (var masterFolder in masterFolders.Folders)
            {
                Logger.Instance.Add($"Синхронизация папки {masterFolder.Name}");

                if (slaveFolders != null)
                {

                    Logger.Instance.Add($"Поиск зависимой папки {masterFolder.Name}");
                    var sameSlaveFolder = slaveFolders.Folders.FirstOrDefault(c => c.Name.Equals(masterFolder.Name));

                    var syncFolder = syncFolderModel.Folders.FirstOrDefault(c => c.Name.Equals(masterFolder.Name));
                    if (syncFolder == null)
                    {
                        Logger.Instance.Add($"Создаем папку синхронизации {masterFolder.Name}");
                        syncFolder = CreateSyncFolder(masterFolder, sameSlaveFolder);
                        syncFolderModel.Folders.Add(syncFolder);
                    }
                    else
                    {
                        Logger.Instance.Add($"Папка синхронизации присутствует {masterFolder.Name}");
                    }

                    Logger.Instance.Add($"Синхронизации файлов для {masterFolder.Name}");
                    SyncFiles(masterFolder, sameSlaveFolder, syncFolder, reverse);

                    SyncFolderRecurse(masterFolder, sameSlaveFolder, syncFolder, reverse);
                }
                else
                {
                    Logger.Instance.Add($"Зависимой папки нет {masterFolder.Name}");
                    var syncFolder = syncFolderModel.Folders.FirstOrDefault(c => c.Name.Equals(masterFolder.Name));
                    if (syncFolder == null)
                    {
                        syncFolder = new FolderModel { Name = masterFolder.Name, ModifyType = masterFolder.ModifyType, Path = masterFolder.Path };
                        syncFolderModel.Folders.Add(syncFolder);
                    }

                    foreach (var masterFile in masterFolder.Files)
                    {
                        var modifyType = reverse ? ModifyType.New : ModifyType.Deleted;
                        Logger.Instance.Add($"Устанавливаем тип {modifyType} для {masterFile.Name}");
                        syncFolder.Files.Add(new FileModel(){Name = masterFile.Name, ModifyType = modifyType, Path = masterFile.Path, Hash = masterFile.Hash, Size = masterFile.Size});
                    }

                    SyncFolderRecurse(masterFolder, null, syncFolder, reverse);
                }
            }
        }

        private void SyncFiles(FolderModel masterFolder, FolderModel sameSlaveFolder, FolderModel syncFolder, bool reverse)
        {
            foreach (var masterFile in masterFolder.Files)
            {
                Logger.Instance.Add($"Синхронизируем файл {masterFile.Name}");
                var sameMainFile = sameSlaveFolder?.Files.FirstOrDefault(c => c.Name.Equals(masterFile.Name));
                var syncFile = syncFolder.Files.FirstOrDefault(c => c.Name.Equals(masterFile.Name));
                if (syncFile == null)
                {
                    var syncFileInfo = CreateSyncFile(masterFile, sameMainFile, reverse);
                    syncFolder.Files.Add(syncFileInfo);
                    Logger.Instance.Add($"Создаем файл синхронизации {masterFile.Name}");
                }
                else
                {
                    Logger.Instance.Add($"Файл синхронизации присутствует {masterFile.Name}");
                }
            }
        }

        private FolderModel CreateSyncFolder(FolderModel masterFolder, FolderModel slaveFolder)
        {
            if (slaveFolder != null)
            {
                Logger.Instance.Add($"Зависимая папка найдена {masterFolder.Name}");
                return new FolderModel{Name = slaveFolder.Name, ModifyType = slaveFolder.ModifyType, Path = slaveFolder.Path};
            }
            else
            {
                Logger.Instance.Add($"Зависимой папки нет {masterFolder.Name}");
                return new FolderModel { Name = masterFolder.Name, ModifyType = masterFolder.ModifyType, Path = masterFolder.Path };
            }
        }

        private FileModel CreateSyncFile(FileModel masterFile, FileModel slaveFile, bool reverse)
        {
            if (slaveFile != null)
            {
                Logger.Instance.Add($"Зависимый файл найден {slaveFile.Name}");
               
                var syncFile = new FileModel {
                    Name = slaveFile.Name, 
                    Path = slaveFile.Path,
                    CheckHash = masterFile.CheckHash, 
                    QuickUpdate = masterFile.QuickUpdate, 
                    Hash = masterFile.Hash, 
                    Size = masterFile.Size,
                    FullPath = slaveFile.FullPath,
                    FilePatches = masterFile.FilePatches
                };

                if (slaveFile.Hash == masterFile.Hash || syncFile.FilePatches.Any(c => c.Hash == slaveFile.Hash))
                {
                    syncFile.ModifyType = ModifyType.NotModified;
                }
                if (slaveFile.Hash != masterFile.Hash && syncFile.FilePatches.All(c => c.Hash != slaveFile.Hash))
                {
                    syncFile.ModifyType = ModifyType.Modified;
                    syncFile.Hash = masterFile.Hash;
                    syncFile.Size = masterFile.Size;
                    var newFilePatch = new FilePatchModel()
                    {
                        Hash = slaveFile.Hash,
                        Size = slaveFile.Size,
                        ModifyType = ModifyType.New,
                        Version = syncFile.FilePatches.Count + 1
                    };
                    newFilePatch.Name = slaveFile.Name + ".patch_" + newFilePatch.Version;
                    syncFile.FilePatches.Add(newFilePatch);
                }
                return syncFile;
            }
            else
            {
                Logger.Instance.Add($"Зависимого файла нет {masterFile.Name}");
                var type = reverse ? ModifyType.New : ModifyType.Deleted;
                Logger.Instance.Add($"Устанавливаем тип {type} для {masterFile.Name}");
                return new FileModel
                {
                    Name = masterFile.Name, 
                    ModifyType = type, 
                    Path = masterFile.Path, 
                    FullPath = masterFile.FullPath,
                    Hash = masterFile.Hash, 
                    Size = masterFile.Size,
                    CheckHash = true,
                    QuickUpdate = true,
                };
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
