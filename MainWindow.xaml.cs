using DeltaUpdater.Enums;
using DeltaUpdater.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DeltaUpdater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private GitHubPrivateService githubService;
        private readonly string appName = "SQLiteClipher";
        private readonly string localAppPath;
        private FileManifest remoteManifest;
        private FileManifest localManifest;
        private List<FileDifference> differences;
        private bool isPrivateRepo = false;

        public MainWindow()
        {
            InitializeComponent();

            localAppPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            LogMessage("DeltaUpdater (Private Repository Support) ishga tushdi");
            LogMessage($"Local path: {localAppPath}");

            // Initialize GitHub service
            _ = Task.Run(InitializeGitHubServiceAsync);
        }

        private async Task InitializeGitHubServiceAsync()
        {
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    StatusText.Text = "GitHub authentication tekshirilmoqda...";
                });

                // Check if token exists
                if (!SecureTokenManager.HasToken())
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        ShowTokenSetupDialog();
                    });
                    return;
                }

                // Check if token is expired
                if (SecureTokenManager.IsTokenExpired())
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        var result = MessageBox.Show(
                            "GitHub token might be expired. Do you want to update it?",
                            "Token Expiry",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            ShowTokenSetupDialog();
                            return;
                        }
                    });
                }

                // Load config and token
                var config = SecureTokenManager.LoadConfig();
                string token = SecureTokenManager.LoadToken();

                if (config == null || string.IsNullOrEmpty(token))
                {
                    throw new Exception("Configuration or token not found");
                }

                // Initialize GitHub service
                githubService = new GitHubPrivateService(config.GitHubUser, config.GitHubRepo, token);
                isPrivateRepo = true;

                // Test connection
                bool connectionOK = await githubService.TestConnectionAsync();

                await Dispatcher.InvokeAsync(() =>
                {
                    if (connectionOK)
                    {
                        LogMessage($"✅ Private GitHub repository configured: {config.GitHubUser}/{config.GitHubRepo}");
                        StatusText.Text = "GitHub authentication muvaffaqiyatli";
                        this.Title = $"DeltaUpdater - {appName} (Private: {config.GitHubRepo})";
                    }
                    else
                    {
                        LogMessage("❌ GitHub authentication failed");
                        StatusText.Text = "GitHub authentication xatolik - token yangilash kerak";

                        var result = MessageBox.Show(
                            "GitHub authentication failed. Token might be invalid or expired.\n\n" +
                            "Do you want to update your token?",
                            "Authentication Failed",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Error);

                        if (result == MessageBoxResult.Yes)
                        {
                            ShowTokenSetupDialog();
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    LogMessage($"GitHub service initialization error: {ex.Message}");
                    StatusText.Text = "GitHub service xatolik";

                    MessageBox.Show(
                        $"GitHub service setup failed:\n{ex.Message}\n\n" +
                        "Please check your token and try again.",
                        "Setup Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
            }
        }

        private void ShowTokenSetupDialog()
        {
            try
            {
                var tokenDialog = new TokenSetupWindow();
                if (tokenDialog.ShowDialog() == true)
                {
                    // Token saved successfully, reinitialize service
                    LogMessage("Token updated, reinitializing GitHub service...");
                    _ = Task.Run(InitializeGitHubServiceAsync);
                }
                else
                {
                    LogMessage("Token setup cancelled - using fallback mode");
                    StatusText.Text = "GitHub token setup iptal qilindi";
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Token setup error: {ex.Message}");
                MessageBox.Show($"Token setup error: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (githubService == null)
            {
                MessageBox.Show("GitHub service not configured. Please setup your token first.",
                              "Service Not Ready", MessageBoxButton.OK, MessageBoxImage.Warning);
                ShowTokenSetupDialog();
                return;
            }

            CheckUpdateButton.IsEnabled = false;
            UpdateButton.IsEnabled = false;
            ProgressBar.Value = 0;
            StatusText.Text = "Private repository dan fayllar tekshirilmoqda...";

            try
            {
                await CheckFileUpdates();
            }
            catch (Exception ex)
            {
                LogMessage($"Xatolik: {ex.Message}");
                StatusText.Text = "Tekshirishda xatolik";

                // Check if it's an authentication error
                if (ex.Message.Contains("401") || ex.Message.Contains("403"))
                {
                    var result = MessageBox.Show(
                        "Authentication failed. Your token might be expired or invalid.\n\n" +
                        "Do you want to update your token?",
                        "Authentication Error",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Error);

                    if (result == MessageBoxResult.Yes)
                    {
                        ShowTokenSetupDialog();
                    }
                }
            }
            finally
            {
                CheckUpdateButton.IsEnabled = true;
            }
        }

        private async Task CheckFileUpdates()
        {
            try
            {
                // 1. Local manifest yaratish
                LogMessage("SQLiteClipher local fayllar scan qilinmoqda...");
                StatusText.Text = "Local fayllar tekshirilmoqda...";
                ProgressBar.Value = 10;

                localManifest = CreateLocalManifest();
                LogMessage($"Local da {localManifest.Files.Count} ta fayl topildi");
                CurrentVersionText.Text = localManifest.Version;
                LocalFilesCountText.Text = localManifest.Files.Count.ToString();

                // 2. Private GitHub dan manifest olish
                LogMessage("Private GitHub dan manifest olinmoqda...");
                StatusText.Text = "Private GitHub dan ma'lumot olinmoqda...";
                ProgressBar.Value = 30;

                remoteManifest = await githubService.GetManifestAsync();
                LogMessage($"Private GitHub da {remoteManifest.Files.Count} ta fayl mavjud");
                AvailableVersionText.Text = remoteManifest.Version;
                RemoteFilesCountText.Text = remoteManifest.Files.Count.ToString();

                // 3. Fayllarni solishtirish
                LogMessage("SQLiteClipher fayllari solishtirilmoqda...");
                StatusText.Text = "Fayllar solishtirilmoqda...";
                ProgressBar.Value = 50;

                differences = CompareManifests(localManifest, remoteManifest);

                // 4. Natijalarni ko'rsatish
                ProgressBar.Value = 100;

                if (differences.Any())
                {
                    var addedFiles = differences.Where(d => d.ChangeType == FileChangeType.Added).Count();
                    var modifiedFiles = differences.Where(d => d.ChangeType == FileChangeType.Modified).Count();
                    var deletedFiles = differences.Where(d => d.ChangeType == FileChangeType.Deleted).Count();

                    StatusText.Text = $"Yangilanish kerak: {differences.Count} ta fayl";
                    UpdateButton.IsEnabled = true;

                    // UI da o'zgarishlar statistikasini ko'rsatish
                    ChangesPanel.Visibility = Visibility.Visible;
                    AddedFilesText.Text = $"➕ {addedFiles}";
                    ModifiedFilesText.Text = $"🔄 {modifiedFiles}";
                    DeletedFilesText.Text = $"❌ {deletedFiles}";

                    LogMessage($"Private repo da farqlar topildi:");
                    LogMessage($"  ➕ Yangi fayllar: {addedFiles}");
                    LogMessage($"  🔄 O'zgargan fayllar: {modifiedFiles}");
                    LogMessage($"  ❌ O'chirilgan fayllar: {deletedFiles}");

                    // Batafsil ro'yxat
                    foreach (var diff in differences.Take(10))
                    {
                        string icon = diff.ChangeType switch
                        {
                            FileChangeType.Added => "➕",
                            FileChangeType.Modified => "🔄",
                            FileChangeType.Deleted => "❌",
                            _ => "?"
                        };
                        LogMessage($"  {icon} {diff.RelativePath}");
                    }

                    if (differences.Count > 10)
                    {
                        LogMessage($"  ... va yana {differences.Count - 10} ta fayl");
                    }
                }
                else
                {
                    StatusText.Text = "Barcha fayllar yangini";
                    ChangesPanel.Visibility = Visibility.Collapsed;
                    LogMessage("Yangilanish talab qilinmaydi - barcha fayllar yangini");

                    if (localManifest.Version != remoteManifest.Version)
                    {
                        LogMessage($"Versiya farqli: Local={localManifest.Version}, Remote={remoteManifest.Version}");
                    }
                }

                // Update last check time
                SecureTokenManager.UpdateLastCheckTime();
            }
            catch (Exception ex)
            {
                LogMessage($"Private GitHub tekshirishda xatolik: {ex.Message}");
                throw;
            }
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateButton.IsEnabled = false;
            CheckUpdateButton.IsEnabled = false;

            try
            {
                await ApplyFileUpdates();
            }
            catch (Exception ex)
            {
                LogMessage($"Yangilashda xatolik: {ex.Message}");
                StatusText.Text = "Yangilashda xatolik";
            }
            finally
            {
                CheckUpdateButton.IsEnabled = true;
            }
        }

        private async Task ApplyFileUpdates()
        {
            try
            {
                // SQLiteClipher dasturining ishlab turganini tekshirish
                if (IsMainAppRunning())
                {
                    MessageBox.Show("Iltimos, avval SQLiteClipher dasturini yoping!", "Ogohlantirish",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                StatusText.Text = "Private GitHub dan fayllar yuklanmoqda...";
                LogMessage("Private repository dan fayllar yangilanishi boshlandi...");

                int processedFiles = 0;
                int totalFiles = differences.Count;

                // Avval o'chirilgan fayllarni o'chirish
                var deletedFiles = differences.Where(d => d.ChangeType == FileChangeType.Deleted);
                foreach (var file in deletedFiles)
                {
                    try
                    {
                        LogMessage($"O'chirilmoqda: {file.RelativePath}");
                        DeleteFile(file.RelativePath);

                        processedFiles++;
                        ProgressBar.Value = (double)processedFiles / totalFiles * 100;
                        ProgressText.Text = $"{processedFiles}/{totalFiles}";
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"O'chirishda xatolik {file.RelativePath}: {ex.Message}");
                    }
                }

                // Keyin yangi va o'zgargan fayllarni download qilish
                var filesToDownload = differences.Where(d =>
                    d.ChangeType == FileChangeType.Added ||
                    d.ChangeType == FileChangeType.Modified);

                foreach (var file in filesToDownload)
                {
                    try
                    {
                        LogMessage($"Private GitHub dan yuklanmoqda: {file.RelativePath}");

                        var progress = new Progress<long>(bytes =>
                        {
                            // File download progress
                            SpeedText.Text = $"{bytes / 1024} KB";
                        });

                        byte[] fileData = await githubService.DownloadFileAsync(file.RelativePath, progress);

                        // Checksum tekshirish
                        string downloadedChecksum = CalculateFileChecksum(fileData);

                        if (downloadedChecksum != file.RemoteFile.Checksum)
                        {
                            throw new Exception($"Checksum mos kelmadi: {file.RelativePath}");
                        }

                        // Faylni saqlash
                        await SaveFileAsync(file.RelativePath, fileData);
                        LogMessage($"Saqlandi: {file.RelativePath}");

                        processedFiles++;
                        ProgressBar.Value = (double)processedFiles / totalFiles * 100;
                        ProgressText.Text = $"{processedFiles}/{totalFiles}";
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Xatolik {file.RelativePath}: {ex.Message}");
                    }
                }

                // Version faylini yangilash
                try
                {
                    string versionPath = System.IO.Path.Combine(localAppPath, "version.txt");
                    await File.WriteAllTextAsync(versionPath, remoteManifest.Version);
                }
                catch (Exception ex)
                {
                    LogMessage($"Version faylini yangilashda xatolik: {ex.Message}");
                }

                StatusText.Text = "Private repository dan yangilanish tugallandi!";
                ProgressBar.Value = 100;
                LogMessage("Private GitHub dan barcha fayllar muvaffaqiyatli yangilandi!");

                MessageBox.Show("SQLiteClipher muvaffaqiyatli yangilandi!\nDasturni qayta ishga tushiring.",
                              "Muvaffaqiyat", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogMessage($"Private GitHub yangilashda umumiy xatolik: {ex.Message}");
                throw;
            }
        }

        // Menu items for token management
        private void ChangeTokenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ShowTokenSetupDialog();
        }

        private void ViewTokenInfoMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = SecureTokenManager.LoadConfig();
                if (config != null)
                {
                    string info = $"GitHub User: {config.GitHubUser}\n" +
                                $"Repository: {config.GitHubRepo}\n" +
                                $"Private Repository: {(config.IsPrivateRepo ? "Yes" : "No")}\n" +
                                $"Token Saved: {config.TokenSavedAt:yyyy-MM-dd HH:mm}\n" +
                                $"Last Check: {config.LastUpdateCheck}\n" +
                                $"Config Path: {SecureTokenManager.GetAppDataPath()}";

                    MessageBox.Show(info, "Token Information",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("No token configuration found.", "Token Information",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading token info: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteTokenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "Are you sure you want to delete the saved GitHub token?\n\n" +
                    "You will need to setup authentication again.",
                    "Delete Token",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    SecureTokenManager.DeleteToken();
                    githubService?.Dispose();
                    githubService = null;
                    isPrivateRepo = false;

                    LogMessage("GitHub token deleted");
                    StatusText.Text = "GitHub token o'chirildi";
                    this.Title = "DeltaUpdater - Token Required";

                    MessageBox.Show("Token deleted successfully.", "Success",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting token: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Helper methods (reuse existing code)
        private FileManifest CreateLocalManifest()
        {
            var manifest = new FileManifest
            {
                Version = GetCurrentVersion(),
                GeneratedAt = DateTime.Now,
                Files = new List<FileInfoModel>()
            };

            if (Directory.Exists(localAppPath))
            {
                ScanDirectory(localAppPath, localAppPath, manifest.Files);
            }

            return manifest;
        }

        private void ScanDirectory(string directoryPath, string basePath, List<FileInfoModel> fileList)
        {
            try
            {
                foreach (string filePath in Directory.GetFiles(directoryPath))
                {
                    string relativePath = System.IO.Path.GetRelativePath(basePath, filePath);

                    if (ShouldSkipFile(relativePath))
                        continue;

                    var fileInfo = new Models.FileInfoModel
                    {
                        RelativePath = relativePath.Replace('\\', '/'),
                        Size = new System.IO.FileInfo(filePath).Length,
                        Checksum = CalculateFileChecksum(filePath),
                        LastModified = File.GetLastWriteTime(filePath)
                    };

                    fileList.Add(fileInfo);
                }

                foreach (string subDir in Directory.GetDirectories(directoryPath))
                {
                    ScanDirectory(subDir, basePath, fileList);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Directory scan error: {ex.Message}");
            }
        }

        private bool ShouldSkipFile(string relativePath)
        {
            string fileName = System.IO.Path.GetFileName(relativePath).ToLower();
            string[] skipFiles = {
                "updater.exe", "updater.pdb",
                "deltaupdater.exe", "deltaupdater.pdb", "deltaupdater.dll",
                "manifestgenerator.exe",
                "manifest.json", "temp.txt",
                ".log", ".tmp", ".pdb"
            };

            return skipFiles.Any(skip => fileName.Contains(skip));
        }

        private string CalculateFileChecksum(string filePath)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = sha256.ComputeHash(stream);
                return Convert.ToHexString(hash);
            }
        }

        private string CalculateFileChecksum(byte[] fileData)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(fileData);
                return Convert.ToHexString(hash);
            }
        }

        private List<FileDifference> CompareManifests(FileManifest local, FileManifest remote)
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

        private async Task SaveFileAsync(string relativePath, byte[] fileData)
        {
            string fullPath = System.IO.Path.Combine(localAppPath, relativePath);
            string directory = System.IO.Path.GetDirectoryName(fullPath);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(fullPath))
            {
                string backupPath = fullPath + ".backup";
                File.Copy(fullPath, backupPath, true);
            }

            await File.WriteAllBytesAsync(fullPath, fileData);
        }

        private void DeleteFile(string relativePath)
        {
            try
            {
                string fullPath = System.IO.Path.Combine(localAppPath, relativePath);
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

        private string GetCurrentVersion()
        {
            try
            {
                string versionFile = System.IO.Path.Combine(localAppPath, "version.txt");
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

        private bool IsMainAppRunning()
        {
            try
            {
                var processes = Process.GetProcessesByName(appName);
                return processes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("explorer.exe", localAppPath);
            }
            catch (Exception ex)
            {
                LogMessage($"Papka ochishda xatolik: {ex.Message}");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            githubService?.Dispose();
            Close();
        }

        private void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBox.Text += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
                LogTextBox.ScrollToEnd();
            });
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            githubService?.Dispose();
        }
    }
}