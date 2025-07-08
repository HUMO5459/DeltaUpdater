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
        private readonly FileSyncService syncService;
        private readonly string appName = "YourAppName"; // Dastur nomi
        private readonly string githubUser = "yourusername"; // GitHub username
        private readonly string githubRepo = "your-repo-name"; // Repository nomi
        private readonly string localAppPath;
        private FileManifest remoteManifest;
        private FileManifest localManifest;
        private List<FileDifference> differences;

        public MainWindow()
        {
            InitializeComponent();

            // Local app path ni aniqlash
            localAppPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            // Yoki qo'lda ko'rsatish: localAppPath = @"C:\Program Files\YourApp";

            syncService = new FileSyncService(githubUser, githubRepo, localAppPath);

            LogMessage("GitHub File Sync Updater ishga tushdi");
            LogMessage($"GitHub: {githubUser}/{githubRepo}");
            LogMessage($"Local path: {localAppPath}");
        }

        private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdateButton.IsEnabled = false;
            UpdateButton.IsEnabled = false;
            ProgressBar.Value = 0;
            StatusText.Text = "Fayllar tekshirilmoqda...";

            try
            {
                await CheckFileUpdates();
            }
            catch (Exception ex)
            {
                LogMessage($"Xatolik: {ex.Message}");
                StatusText.Text = "Tekshirishda xatolik";
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
                LogMessage("Local fayllar scan qilinmoqda...");
                StatusText.Text = "Local fayllar tekshirilmoqda...";
                ProgressBar.Value = 10;

                localManifest = syncService.CreateLocalManifest();
                LogMessage($"Local da {localManifest.Files.Count} ta fayl topildi");
                CurrentVersionText.Text = localManifest.Version;
                LocalFilesCountText.Text = localManifest.Files.Count.ToString();

                // 2. Remote manifest olish
                LogMessage("GitHub dan manifest olinmoqda...");
                StatusText.Text = "GitHub dan ma'lumot olinmoqda...";
                ProgressBar.Value = 30;

                remoteManifest = await syncService.GetRemoteManifestAsync();
                LogMessage($"GitHub da {remoteManifest.Files.Count} ta fayl mavjud");
                AvailableVersionText.Text = remoteManifest.Version;
                RemoteFilesCountText.Text = remoteManifest.Files.Count.ToString();

                // 3. Fayllarni solishtirish
                LogMessage("Fayllar solishtirilmoqda...");
                StatusText.Text = "Fayllar solishtirilmoqda...";
                ProgressBar.Value = 50;

                differences = syncService.CompareManifests(localManifest, remoteManifest);

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

                    LogMessage($"Farqlar topildi:");
                    LogMessage($"  ➕ Yangi fayllar: {addedFiles}");
                    LogMessage($"  🔄 O'zgargan fayllar: {modifiedFiles}");
                    LogMessage($"  ❌ O'chirilgan fayllar: {deletedFiles}");

                    // Batafsil ro'yxat
                    foreach (var diff in differences.Take(10)) // Faqat birinchi 10 tani ko'rsatish
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
                        LogMessage($"Lekin versiya farqli: Local={localManifest.Version}, Remote={remoteManifest.Version}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Tekshirishda xatolik: {ex.Message}");
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
                // Asosiy dasturning ishlab turganini tekshirish
                if (IsMainAppRunning())
                {
                    MessageBox.Show("Iltimos, avval asosiy dasturni yoping!", "Ogohlantirish",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                StatusText.Text = "Fayllar yangilanmoqda...";
                LogMessage("Fayllar yangilanishi boshlandi...");

                int processedFiles = 0;
                int totalFiles = differences.Count;

                // Avval o'chirilgan fayllarni o'chirish
                var deletedFiles = differences.Where(d => d.ChangeType == FileChangeType.Deleted);
                foreach (var file in deletedFiles)
                {
                    try
                    {
                        LogMessage($"O'chirilmoqda: {file.RelativePath}");
                        syncService.DeleteFile(file.RelativePath);

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
                        LogMessage($"Yuklanmoqda: {file.RelativePath}");

                        var progress = new Progress<long>(bytes =>
                        {
                            // Fayl download progress ni ko'rsatish
                        });

                        byte[] fileData = await syncService.DownloadFileAsync(file.RelativePath, progress);

                        // Checksum ni tekshirish
                        string tempPath = System.IO.Path.GetTempFileName();
                        await File.WriteAllBytesAsync(tempPath, fileData);
                        string downloadedChecksum = syncService.CalculateFileChecksum(tempPath);
                        File.Delete(tempPath);

                        if (downloadedChecksum != file.RemoteFile.Checksum)
                        {
                            throw new Exception($"Checksum mos kelmadi: {file.RelativePath}");
                        }

                        // Faylni saqlash
                        await syncService.SaveFileAsync(file.RelativePath, fileData);
                        LogMessage($"Saqlandi: {file.RelativePath}");

                        processedFiles++;
                        ProgressBar.Value = (double)processedFiles / totalFiles * 100;
                        ProgressText.Text = $"{processedFiles}/{totalFiles}";
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Xatolik {file.RelativePath}: {ex.Message}");
                        // Davom etish yoki to'xtatish?
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

                StatusText.Text = "Yangilanish tugallandi!";
                ProgressBar.Value = 100;
                LogMessage("Barcha fayllar muvaffaqiyatli yangilandi!");

                MessageBox.Show("Fayllar muvaffaqiyatli yangilandi!\nDasturni qayta ishga tushiring.",
                              "Muvaffaqiyat", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogMessage($"Yangilashda umumiy xatolik: {ex.Message}");
                throw;
            }
        }

        private bool IsMainAppRunning()
        {
            try
            {
                var processes = Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(appName));
                return processes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBox.Text += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
                LogTextBox.ScrollToEnd();
            });
        }

        // Yangi tugma hodisalari
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
            syncService?.Dispose();
            Close();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            syncService?.Dispose();
        }
    }
}