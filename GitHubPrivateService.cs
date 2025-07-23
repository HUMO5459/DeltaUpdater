using DeltaUpdater.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DeltaUpdater
{
    public class GitHubPrivateService : IDisposable
    {
        private readonly HttpClient httpClient;
        private readonly string token;
        private readonly string repoOwner;
        private readonly string repoName;
        private readonly string apiBaseUrl;

        public GitHubPrivateService(string owner, string repo, string accessToken)
        {
            httpClient = new HttpClient();
            token = accessToken;
            repoOwner = owner;
            repoName = repo;
            apiBaseUrl = $"https://api.github.com/repos/{owner}/{repo}";

            // GitHub API headers
            httpClient.DefaultRequestHeaders.Add("User-Agent", "DeltaUpdater/1.0");
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        }

        // Test connection
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                string apiUrl = $"{apiBaseUrl}";
                var response = await httpClient.GetAsync(apiUrl);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // Get file content from private repo
        public async Task<string> GetFileContentAsync(string filePath)
        {
            try
            {
                string apiUrl = $"{apiBaseUrl}/contents/{filePath}";

                var response = await httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"GitHub API Error {response.StatusCode}: {errorContent}");
                }

                string jsonContent = await response.Content.ReadAsStringAsync();
                var fileInfo = JsonSerializer.Deserialize<GitHubFileResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (fileInfo.Encoding == "base64")
                {
                    // Base64 decode
                    byte[] data = Convert.FromBase64String(fileInfo.Content.Replace("\n", "").Replace("\r", ""));
                    return Encoding.UTF8.GetString(data);
                }
                else
                {
                    return fileInfo.Content;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get file {filePath}: {ex.Message}");
            }
        }

        // Get manifest from private repo
        public async Task<FileManifest> GetManifestAsync()
        {
            try
            {
                string manifestContent = await GetFileContentAsync("manifest.json");

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                return JsonSerializer.Deserialize<FileManifest>(manifestContent, options);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get manifest: {ex.Message}");
            }
        }

        // Download file from private repo
        public async Task<byte[]> DownloadFileAsync(string filePath, IProgress<long> progress = null)
        {
            try
            {
                string apiUrl = $"{apiBaseUrl}/contents/{filePath}";

                var response = await httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"File not found: {filePath}");
                }

                string jsonContent = await response.Content.ReadAsStringAsync();
                var fileInfo = JsonSerializer.Deserialize<GitHubFileResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (fileInfo.Encoding == "base64")
                {
                    // Base64 decode
                    byte[] data = Convert.FromBase64String(fileInfo.Content.Replace("\n", "").Replace("\r", ""));

                    // Report progress
                    progress?.Report(data.Length);

                    return data;
                }
                else
                {
                    throw new Exception($"Unsupported file encoding: {fileInfo.Encoding}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to download file {filePath}: {ex.Message}");
            }
        }

        // Get repository info
        public async Task<GitHubRepoInfo> GetRepositoryInfoAsync()
        {
            try
            {
                var response = await httpClient.GetAsync(apiBaseUrl);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Repository access failed: {response.StatusCode}");
                }

                string jsonContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<GitHubRepoInfo>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get repository info: {ex.Message}");
            }
        }

        // List repository contents
        public async Task<List<GitHubFileInfo>> GetRepositoryContentsAsync(string path = "")
        {
            try
            {
                string apiUrl = $"{apiBaseUrl}/contents/{path}";

                var response = await httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to list contents: {response.StatusCode}");
                }

                string jsonContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<GitHubFileInfo>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get repository contents: {ex.Message}");
            }
        }

        public void Dispose()
        {
            httpClient?.Dispose();
        }

        // GitHub API Response Models
        public class GitHubFileResponse
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string Content { get; set; }
            public string Encoding { get; set; }
            public string DownloadUrl { get; set; }
            public int Size { get; set; }
            public string Sha { get; set; }
            public string Type { get; set; }
        }

        public class GitHubRepoInfo
        {
            public string Name { get; set; }
            public string FullName { get; set; }
            public bool Private { get; set; }
            public string Description { get; set; }
            public DateTime UpdatedAt { get; set; }
            public int Size { get; set; }
            public string DefaultBranch { get; set; }
        }

        public class GitHubFileInfo
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string Type { get; set; }
            public int Size { get; set; }
            public string DownloadUrl { get; set; }
        }
    }
}
