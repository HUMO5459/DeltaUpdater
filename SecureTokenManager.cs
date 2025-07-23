using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DeltaUpdater
{
    public class SecureTokenManager
    {
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DeltaUpdater");

        private static readonly string TokenFilePath = Path.Combine(AppDataPath, "auth.dat");
        private static readonly string ConfigFilePath = Path.Combine(AppDataPath, "config.json");

        // Configuration model
        public class AppConfig
        {
            public string GitHubUser { get; set; }
            public string GitHubRepo { get; set; }
            public DateTime TokenSavedAt { get; set; }
            public bool IsPrivateRepo { get; set; }
            public string LastUpdateCheck { get; set; }
        }

        // Initialize app data directory
        private static void EnsureAppDataDirectory()
        {
            if (!Directory.Exists(AppDataPath))
            {
                Directory.CreateDirectory(AppDataPath);
            }
        }

        // Save encrypted token
        public static void SaveToken(string token, string githubUser, string githubRepo)
        {
            try
            {
                EnsureAppDataDirectory();

                // Validate token format
                if (!IsValidGitHubToken(token))
                {
                    throw new ArgumentException("Invalid GitHub token format");
                }

                // Encrypt token
                byte[] encryptedToken = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(token),
                    GetEntropy(),
                    DataProtectionScope.CurrentUser);

                // Save encrypted token
                File.WriteAllBytes(TokenFilePath, encryptedToken);

                // Save config
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

        // Load decrypted token
        public static string LoadToken()
        {
            try
            {
                if (!File.Exists(TokenFilePath))
                {
                    return null;
                }

                // Read encrypted token
                byte[] encryptedToken = File.ReadAllBytes(TokenFilePath);

                // Decrypt token
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

        // Check if token exists
        public static bool HasToken()
        {
            return File.Exists(TokenFilePath);
        }

        // Delete token and config
        public static void DeleteToken()
        {
            try
            {
                if (File.Exists(TokenFilePath))
                {
                    File.Delete(TokenFilePath);
                }

                if (File.Exists(ConfigFilePath))
                {
                    File.Delete(ConfigFilePath);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Token o'chirishda xatolik: {ex.Message}");
            }
        }

        // Save app configuration
        public static void SaveConfig(AppConfig config)
        {
            try
            {
                EnsureAppDataDirectory();

                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Config saqlashda xatolik: {ex.Message}");
            }
        }

        // Load app configuration
        public static AppConfig LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    return null;
                }

                string json = File.ReadAllText(ConfigFilePath);
                return JsonSerializer.Deserialize<AppConfig>(json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Config o'qishda xatolik: {ex.Message}");
            }
        }

        // Validate GitHub token format
        private static bool IsValidGitHubToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            // GitHub classic tokens start with ghp_
            // GitHub fine-grained tokens start with github_pat_
            return token.StartsWith("ghp_") || token.StartsWith("github_pat_");
        }

        // Get entropy for encryption (simple machine-based)
        private static byte[] GetEntropy()
        {
            string machineKey = Environment.MachineName + Environment.UserName;
            return Encoding.UTF8.GetBytes(machineKey);
        }

        // Token expiry check (if needed)
        public static bool IsTokenExpired()
        {
            try
            {
                var config = LoadConfig();
                if (config == null) return true;

                // Check if token is older than 1 year (GitHub tokens can expire)
                return DateTime.Now.Subtract(config.TokenSavedAt).TotalDays > 365;
            }
            catch
            {
                return true;
            }
        }

        // Update last check time
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
            catch
            {
                // Ignore errors
            }
        }

        // Get app data folder path (for debugging)
        public static string GetAppDataPath()
        {
            return AppDataPath;
        }
    }
}
