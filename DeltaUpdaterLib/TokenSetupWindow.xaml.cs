using System.Windows;

namespace DeltaUpdater
{
    public partial class TokenSetupWindow : Window
    {
        private GitHubPrivateService testService;

        public string GitHubUser { get; private set; }
        public string GitHubRepo { get; private set; }
        public string AccessToken { get; private set; }

        public TokenSetupWindow()
        {
            InitializeComponent();
            LoadExistingConfig();
        }

        private void LoadExistingConfig()
        {
            try
            {
                var config = SecureTokenManager.LoadConfig();
                if (config != null)
                {
                    GitHubUserTextBox.Text = config.GitHubUser ?? "";
                    GitHubRepoTextBox.Text = config.GitHubRepo ?? "";
                }
                else
                {
                    GitHubUserTextBox.Text = "yourusername";
                    GitHubRepoTextBox.Text = "your-repo";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Config loading error: {ex.Message}", "Warning",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void TokenPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            string token = TokenPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(token))
            {
                TokenValidationText.Text = "Enter your GitHub token...";
                TokenValidationText.Foreground = System.Windows.Media.Brushes.Gray;
                TestButton.IsEnabled = false;
                SaveButton.IsEnabled = false;
            }
            else if (token.StartsWith("ghp_") || token.StartsWith("github_pat_"))
            {
                TokenValidationText.Text = "✅ Valid token format";
                TokenValidationText.Foreground = System.Windows.Media.Brushes.Green;
                TestButton.IsEnabled = !string.IsNullOrWhiteSpace(GitHubUserTextBox.Text) &&
                                       !string.IsNullOrWhiteSpace(GitHubRepoTextBox.Text);
                SaveButton.IsEnabled = false;
            }
            else
            {
                TokenValidationText.Text = "❌ Invalid token format (should start with 'ghp_' or 'github_pat_')";
                TokenValidationText.Foreground = System.Windows.Media.Brushes.Red;
                TestButton.IsEnabled = false;
                SaveButton.IsEnabled = false;
            }
        }

        private async void TestButton_Click(object sender, RoutedEventArgs e)
        {
            TestButton.IsEnabled = false;
            TestButton.Content = "🔄 Testing...";

            try
            {
                string user = GitHubUserTextBox.Text.Trim();
                string repo = GitHubRepoTextBox.Text.Trim();
                string token = TokenPasswordBox.Password;

                if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(repo))
                {
                    MessageBox.Show("Please enter GitHub user and repository name.", "Error",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                testService?.Dispose();
                testService = new GitHubPrivateService(user, repo, token);
                bool connectionOK = await testService.TestConnectionAsync();

                if (connectionOK)
                {
                    var repoInfo = await testService.GetRepositoryInfoAsync();
                    string message = $"✅ Connection successful!\n\n" +
                                     $"Repository: {repoInfo.FullName}\n" +
                                     $"Private: {(repoInfo.Private ? "Yes" : "No")}\n" +
                                     $"Description: {repoInfo.Description ?? "No description"}\n" +
                                     $"Last updated: {repoInfo.UpdatedAt:yyyy-MM-dd}";

                    MessageBox.Show(message, "Connection Test", MessageBoxButton.OK, MessageBoxImage.Information);
                    TokenValidationText.Text = "✅ Connection verified - Ready to save";
                    TokenValidationText.Foreground = System.Windows.Media.Brushes.Green;
                    SaveButton.IsEnabled = true;
                }
                else
                {
                    MessageBox.Show("❌ Connection failed!\n\nPlease check:\n" +
                                  "• Token is valid and not expired\n" +
                                  "• Repository name is correct\n" +
                                  "• Token has 'repo' scope permissions",
                                  "Connection Test Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    TokenValidationText.Text = "❌ Connection failed";
                    TokenValidationText.Foreground = System.Windows.Media.Brushes.Red;
                    SaveButton.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection test error:\n{ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                TokenValidationText.Text = "❌ Test failed";
                TokenValidationText.Foreground = System.Windows.Media.Brushes.Red;
                SaveButton.IsEnabled = false;
            }
            finally
            {
                TestButton.Content = "🧪 Test Connection";
                TestButton.IsEnabled = true;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string user = GitHubUserTextBox.Text.Trim();
                string repo = GitHubRepoTextBox.Text.Trim();
                string token = TokenPasswordBox.Password;

                SecureTokenManager.SaveToken(token, user, repo);

                GitHubUser = user;
                GitHubRepo = repo;
                AccessToken = token;

                MessageBox.Show("✅ GitHub token saved successfully!\n\n" +
                              "Your token has been encrypted and stored securely.",
                              "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save error:\n{ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            testService?.Dispose();
        }
    }
}
