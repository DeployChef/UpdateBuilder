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

        public async Task<FolderModel> CreateSyncFolderInfo(string patchPath, CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}
