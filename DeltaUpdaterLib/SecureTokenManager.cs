using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DeltaUpdater
{
    public class SecureTokenManager
    {
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DeltaUpdater");

        private static readonly string TokenFilePath = Path.Combine(AppDataPath, "auth.dat");
        private static readonly string ConfigFilePath = Path.Combine(AppDataPath, "config.json");

        public class AppConfig
        {
            public string GitHubUser { get; set; }
            public string GitHubRepo { get; set; }
            public DateTime TokenSavedAt { get; set; }
            public bool IsPrivateRepo { get; set; }
            public string LastUpdateCheck { get; set; }
        }

        private static void EnsureAppDataDirectory()
        {
            if (!Directory.Exists(AppDataPath))
                Directory.CreateDirectory(AppDataPath);
        }

        public static void SaveToken(string token, string githubUser, string githubRepo)
        {
            try
            {
                EnsureAppDataDirectory();

                if (!IsValidGitHubToken(token))
                    throw new ArgumentException("Invalid GitHub token format");

                byte[] encryptedToken = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(token),
                    GetEntropy(),
                    DataProtectionScope.CurrentUser);

                File.WriteAllBytes(TokenFilePath, encryptedToken);

                var config = new AppConfig
                {
                    GitHubUser = githubUser,
                    GitHubRepo = githubRepo,
                    TokenSavedAt = DateTime.Now,
                    IsPrivateRepo = true,
                    LastUpdateCheck = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                SaveConfig(config);
            }
            catch (Exception ex)
            {
                throw new Exception($"Token saqlashda xatolik: {ex.Message}");
            }
        }

        public static string LoadToken()
        {
            try
            {
                if (!File.Exists(TokenFilePath))
                    return null;

                byte[] encryptedToken = File.ReadAllBytes(TokenFilePath);
                byte[] decryptedToken = ProtectedData.Unprotect(
                    encryptedToken,
                    GetEntropy(),
                    DataProtectionScope.CurrentUser);

                return Encoding.UTF8.GetString(decryptedToken);
            }
            catch (Exception ex)
            {
                throw new Exception($"Token o'qishda xatolik: {ex.Message}");
            }
        }

        public static bool HasToken() => File.Exists(TokenFilePath);

        public static void DeleteToken()
        {
            try
            {
                if (File.Exists(TokenFilePath)) File.Delete(TokenFilePath);
                if (File.Exists(ConfigFilePath)) File.Delete(ConfigFilePath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Token o'chirishda xatolik: {ex.Message}");
            }
        }

        public static void SaveConfig(AppConfig config)
        {
            try
            {
                EnsureAppDataDirectory();
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Config saqlashda xatolik: {ex.Message}");
            }
        }

        public static AppConfig LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigFilePath)) return null;
                string json = File.ReadAllText(ConfigFilePath);
                return JsonSerializer.Deserialize<AppConfig>(json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Config o'qishda xatolik: {ex.Message}");
            }
        }

        public static bool IsTokenExpired()
        {
            try
            {
                var config = LoadConfig();
                if (config == null) return true;
                return DateTime.Now.Subtract(config.TokenSavedAt).TotalDays > 365;
            }
            catch
            {
                return true;
            }
        }

        public static void UpdateLastCheckTime()
        {
            try
            {
                var config = LoadConfig();
                if (config != null)
                {
                    config.LastUpdateCheck = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    SaveConfig(config);
                }
            }
            catch { }
        }

        public static string GetAppDataPath() => AppDataPath;

        private static bool IsValidGitHubToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            return token.StartsWith("ghp_") || token.StartsWith("github_pat_");
        }

        private static byte[] GetEntropy()
        {
            string machineKey = Environment.MachineName + Environment.UserName;
            return Encoding.UTF8.GetBytes(machineKey);
        }
    }
}
