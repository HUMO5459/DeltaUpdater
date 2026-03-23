using DeltaUpdater.Enums;
using DeltaUpdater.Models;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using FileInfoModel = DeltaUpdater.Models.FileInfoModel;

namespace DeltaUpdater
{
    public class FileSyncService : IDisposable
    {
        private readonly HttpClient httpClient;
        private readonly string githubRawUrl;
        private readonly string localAppPath;

        public FileSyncService(string githubUser, string repoName, string localPath)
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "AppUpdater/1.0");
            githubRawUrl = $"https://raw.githubusercontent.com/{githubUser}/{repoName}/main";
            localAppPath = localPath;
        }

        public async Task<FileManifest> GetRemoteManifestAsync()
        {
            try
            {
                string manifestUrl = $"{githubRawUrl}/manifest.json";
                var response = await httpClient.GetStringAsync(manifestUrl);
                return JsonSerializer.Deserialize<FileManifest>(response, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"Remote manifest olishda xatolik: {ex.Message}");
            }
        }

        public FileManifest CreateLocalManifest()
        {
            var manifest = new FileManifest
            {
                Version = GetLocalVersion(),
                GeneratedAt = DateTime.Now,
                Files = new List<FileInfoModel>()
            };

            if (Directory.Exists(localAppPath))
                ScanDirectory(localAppPath, localAppPath, manifest.Files);

            return manifest;
        }

        private void ScanDirectory(string directoryPath, string basePath, List<FileInfoModel> fileList)
        {
            try
            {
                foreach (string filePath in Directory.GetFiles(directoryPath))
                {
                    string relativePath = Path.GetRelativePath(basePath, filePath);
                    if (ShouldSkipFile(relativePath)) continue;

                    fileList.Add(new FileInfoModel
                    {
                        RelativePath = relativePath.Replace('\\', '/'),
                        Size = new System.IO.FileInfo(filePath).Length,
                        Checksum = CalculateFileChecksum(filePath),
                        LastModified = File.GetLastWriteTime(filePath)
                    });
                }

                foreach (string subDir in Directory.GetDirectories(directoryPath))
                    ScanDirectory(subDir, basePath, fileList);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Directory scan xatolik: {ex.Message}");
            }
        }

        private bool ShouldSkipFile(string relativePath)
        {
            string fileName = Path.GetFileName(relativePath).ToLower();
            string[] skipFiles = { "updater.exe", "updater.pdb", "manifest.json", "temp.txt", ".log", ".tmp" };
            return skipFiles.Any(skip => fileName.Contains(skip));
        }

        public string CalculateFileChecksum(string filePath)
        {
            try
            {
                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(filePath);
                byte[] hash = sha256.ComputeHash(stream);
                return Convert.ToHexString(hash);
            }
            catch (Exception ex)
            {
                throw new Exception($"Checksum hisoblashda xatolik {filePath}: {ex.Message}");
            }
        }

        public List<FileDifference> CompareManifests(FileManifest local, FileManifest remote)
        {
            var differences = new List<FileDifference>();

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

            foreach (var localFile in local.Files)
            {
                if (!remote.Files.Any(f => f.RelativePath == localFile.RelativePath))
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

        public async Task<byte[]> DownloadFileAsync(string relativePath, IProgress<long> progress = null)
        {
            try
            {
                string fileUrl = $"{githubRawUrl}/{relativePath}";
                using var response = await httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var memoryStream = new MemoryStream();
                var buffer = new byte[8192];
                int bytesRead;
                long downloadedBytes = 0;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await memoryStream.WriteAsync(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;
                    progress?.Report(downloadedBytes);
                }

                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception($"Fayl download xatolik {relativePath}: {ex.Message}");
            }
        }

        public async Task SaveFileAsync(string relativePath, byte[] fileData)
        {
            try
            {
                string fullPath = Path.Combine(localAppPath, relativePath);
                string directory = Path.GetDirectoryName(fullPath);

                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                if (File.Exists(fullPath))
                    File.Copy(fullPath, fullPath + ".backup", true);

                await File.WriteAllBytesAsync(fullPath, fileData);
            }
            catch (Exception ex)
            {
                throw new Exception($"Fayl saqlashda xatolik {relativePath}: {ex.Message}");
            }
        }

        public void DeleteFile(string relativePath)
        {
            try
            {
                string fullPath = Path.Combine(localAppPath, relativePath);
                if (File.Exists(fullPath)) File.Delete(fullPath);
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
                string versionFile = Path.Combine(localAppPath, "version.txt");
                return File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : "1.0.0";
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
