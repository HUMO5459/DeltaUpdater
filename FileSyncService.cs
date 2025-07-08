using DeltaUpdater.Enums;
using DeltaUpdater.Models;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FileInfo = DeltaUpdater.Models.FileInfo;

namespace DeltaUpdater
{
    public class FileSyncService
    {
        private readonly HttpClient httpClient;
        private readonly string githubRawUrl;
        private readonly string localAppPath;

        public FileSyncService(string githubUser, string repoName, string localPath)
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "AppUpdater/1.0");

            // GitHub raw files URL
            githubRawUrl = $"https://raw.githubusercontent.com/{githubUser}/{repoName}/main";
            localAppPath = localPath;
        }

        // GitHub dan file manifest ni olish
        public async Task<FileManifest> GetRemoteManifestAsync()
        {
            try
            {
                string manifestUrl = $"{githubRawUrl}/manifest.json";
                var response = await httpClient.GetStringAsync(manifestUrl);

                var manifest = JsonSerializer.Deserialize<FileManifest>(response, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return manifest;
            }
            catch (Exception ex)
            {
                throw new Exception($"Remote manifest olishda xatolik: {ex.Message}");
            }
        }

        // Local fayllarning manifest ini yaratish
        public FileManifest CreateLocalManifest()
        {
            var manifest = new FileManifest
            {
                Version = GetLocalVersion(),
                GeneratedAt = DateTime.Now,
                Files = new List<FileInfo>()
            };

            if (!Directory.Exists(localAppPath))
            {
                // Dastur umuman o'rnatilmagan
                return manifest;
            }

            // Barcha fayllarni scan qilish
            ScanDirectory(localAppPath, localAppPath, manifest.Files);

            return manifest;
        }

        private void ScanDirectory(string directoryPath, string basePath, List<FileInfo> fileList)
        {
            try
            {
                // Fayllarni scan qilish
                foreach (string filePath in Directory.GetFiles(directoryPath))
                {
                    string relativePath = Path.GetRelativePath(basePath, filePath);

                    // Temp va updater fayllarini skip qilish
                    if (ShouldSkipFile(relativePath))
                        continue;

                    var fileInfo = new FileInfo
                    {
                        RelativePath = relativePath.Replace('\\', '/'), // Unix style path
                        Size = new System.IO.FileInfo(filePath).Length,
                        Checksum = CalculateFileChecksum(filePath),
                        LastModified = File.GetLastWriteTime(filePath)
                    };

                    fileList.Add(fileInfo);
                }

                // Subdirectorylarni scan qilish
                foreach (string subDir in Directory.GetDirectories(directoryPath))
                {
                    ScanDirectory(subDir, basePath, fileList);
                }
            }
            catch (Exception ex)
            {
                // Directory access xatoliklarini ignore qilish
                Console.WriteLine($"Directory scan xatolik: {ex.Message}");
            }
        }

        private bool ShouldSkipFile(string relativePath)
        {
            string fileName = Path.GetFileName(relativePath).ToLower();
            string[] skipFiles = {
                "updater.exe", "updater.pdb",
                "manifest.json", "temp.txt",
                ".log", ".tmp"
            };

            return skipFiles.Any(skip => fileName.Contains(skip));
        }

        // Fayl checksum ni hisoblash (SHA256)
        public string CalculateFileChecksum(string filePath)
        {
            try
            {
                using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = sha256.ComputeHash(stream);
                    return Convert.ToHexString(hash);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Checksum hisoblashda xatolik {filePath}: {ex.Message}");
            }
        }

        // Fayllarni solishtirish va farqlarni topish
        public List<FileDifference> CompareManifests(FileManifest local, FileManifest remote)
        {
            var differences = new List<FileDifference>();

            // Remote da bor, local da yo'q (yangi fayllar)
            foreach (var remoteFile in remote.Files)
            {
                var localFile = local.Files.FirstOrDefault(f => f.RelativePath == remoteFile.RelativePath);

                if (localFile == null)
                {
                    differences.Add(new FileDifference
                    {
                        RelativePath = remoteFile.RelativePath,
                        ChangeType = FileChangeType.Added,
                        RemoteFile = remoteFile
                    });
                }
                else if (localFile.Checksum != remoteFile.Checksum)
                {
                    differences.Add(new FileDifference
                    {
                        RelativePath = remoteFile.RelativePath,
                        ChangeType = FileChangeType.Modified,
                        LocalFile = localFile,
                        RemoteFile = remoteFile
                    });
                }
            }

            // Local da bor, remote da yo'q (o'chirilgan fayllar)
            foreach (var localFile in local.Files)
            {
                var remoteFile = remote.Files.FirstOrDefault(f => f.RelativePath == localFile.RelativePath);

                if (remoteFile == null)
                {
                    differences.Add(new FileDifference
                    {
                        RelativePath = localFile.RelativePath,
                        ChangeType = FileChangeType.Deleted,
                        LocalFile = localFile
                    });
                }
            }

            return differences;
        }

        // GitHub dan faylni download qilish
        public async Task<byte[]> DownloadFileAsync(string relativePath, IProgress<long> progress = null)
        {
            try
            {
                string fileUrl = $"{githubRawUrl}/{relativePath}";

                using (var response = await httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    var downloadedBytes = 0L;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var memoryStream = new MemoryStream())
                    {
                        var buffer = new byte[8192];
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await memoryStream.WriteAsync(buffer, 0, bytesRead);
                            downloadedBytes += bytesRead;

                            progress?.Report(downloadedBytes);
                        }

                        return memoryStream.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Fayl download xatolik {relativePath}: {ex.Message}");
            }
        }

        // Faylni local ga saqlash
        public async Task SaveFileAsync(string relativePath, byte[] fileData)
        {
            try
            {
                string fullPath = Path.Combine(localAppPath, relativePath);
                string directory = Path.GetDirectoryName(fullPath);

                // Directory yaratish
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Fayl mavjud bo'lsa, backup yaratish
                if (File.Exists(fullPath))
                {
                    string backupPath = fullPath + ".backup";
                    File.Copy(fullPath, backupPath, true);
                }

                // Yangi faylni saqlash
                await File.WriteAllBytesAsync(fullPath, fileData);
            }
            catch (Exception ex)
            {
                throw new Exception($"Fayl saqlashda xatolik {relativePath}: {ex.Message}");
            }
        }

        // Faylni o'chirish
        public void DeleteFile(string relativePath)
        {
            try
            {
                string fullPath = Path.Combine(localAppPath, relativePath);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Fayl o'chirishda xatolik {relativePath}: {ex.Message}");
            }
        }

        private string GetLocalVersion()
        {
            try
            {
                // Version.txt faylidan yoki assembly dan version olish
                string versionFile = Path.Combine(localAppPath, "version.txt");
                if (File.Exists(versionFile))
                {
                    return File.ReadAllText(versionFile).Trim();
                }
                return "1.0.0";
            }
            catch
            {
                return "1.0.0";
            }
        }

        public void Dispose()
        {
            httpClient?.Dispose();
        }
    }
}