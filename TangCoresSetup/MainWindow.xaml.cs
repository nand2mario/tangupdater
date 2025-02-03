using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TangCoresSetup
{
    public partial class MainWindow : Window
    {
        private const string UpdateUrl = "https://raw.githubusercontent.com/nand2mario/tangcores/main/files/list.json";
        private readonly HttpClient _httpClient = new();
        private string? _selectedDrivePath;
        private List<RemoteFile>? _remoteFiles;
        private List<LocalFile>? _localFiles;
        private ReleaseInfo? _releaseInfo;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private class RemoteFile
        {
            public string Filename { get; set; } = "";
            public string Sha1 { get; set; } = "";
        }

        private class LocalFile
        {
            public string Filename { get; set; } = "";
            public string Sha1 { get; set; } = "";
        }

        private class ReleaseInfo
        {
            public List<RemoteFile> Latest { get; set; } = new();
            public List<string> Configs { get; set; } = new();
        }

        private class Release
        {
            public string Name { get; set; } = "";
            public List<string> Files { get; set; } = new();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshDriveList();
        }

        private void RefreshDriveList()
        {
            DriveComboBox.Items.Clear();
            
            try
            {
                var drives = DriveInfo.GetDrives()
                    .Where(d => d.DriveType == DriveType.Removable && d.IsReady)
                    .ToList();

                foreach (var drive in drives)
                {
                    DriveComboBox.Items.Add($"{drive.Name} ({drive.VolumeLabel} - {drive.TotalSize / (1024 * 1024 * 1024)}GB)");
                }

                if (drives.Any())
                {
                    DriveComboBox.SelectedIndex = 0;
                    _selectedDrivePath = drives[0].Name;
                    UpdateLocalFilesList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error detecting drives: {ex.Message}");
            }
        }

        private void UpdateLocalFilesList()
        {
            if (string.IsNullOrEmpty(_selectedDrivePath)) return;

            var coresPath = Path.Combine(_selectedDrivePath, "cores");
            if (!Directory.Exists(coresPath))
            {
                LocalFilesList.Items.Clear();
                return;
            }

            _localFiles = Directory.GetFiles(coresPath)
                .Select(f => new LocalFile
                {
                    Filename = Path.GetFileName(f),
                    Sha1 = ComputeSha1(f)
                })
                .ToList();

            LocalFilesList.Items.Clear();
            foreach (var file in _localFiles)
            {
                LocalFilesList.Items.Add($"{file.Filename} ({file.Sha1[..8]}...)");
            }
        }

        private static string ComputeSha1(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private void DriveComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DriveComboBox.SelectedItem != null)
            {
                SelectedDriveText.Text = $"Selected drive: {DriveComboBox.SelectedItem}";
                _selectedDrivePath = DriveComboBox.SelectedItem.ToString()?.Split(' ')[0];
                UpdateLocalFilesList();
            }
        }

        private void ConfigComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Update the file list for the new configuration
            if (_releaseInfo != null)
            {
                UpdateFileListForConfig();
            }
        }

        private void RefreshDrives_Click(object sender, RoutedEventArgs e)
        {
            RefreshDriveList();
        }

        private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedDrivePath))
            {
                MessageBox.Show("Please select a drive first");
                return;
            }

            try
            {
                // Get the list.json from GitHub
                //                var json = await _httpClient.GetStringAsync(UpdateUrl);
                var json = await _httpClient.GetStringAsync(UpdateUrl);
                var options = new JsonSerializerOptions
                {
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };
                _releaseInfo = JsonSerializer.Deserialize<ReleaseInfo>(json, options);

                if (_releaseInfo == null || !_releaseInfo.Latest.Any())
                {
                    MessageBox.Show("Failed to parse update information");
                    return;
                }
                _remoteFiles = _releaseInfo.Latest;
                
                // Populate configuration dropdown
                ConfigComboBox.Items.Clear();
                foreach (var c in _releaseInfo.Configs)
                {
                    ConfigComboBox.Items.Add(new { Name = c });
                }
                if (_releaseInfo.Configs.Any())
                {
                    ConfigComboBox.SelectedIndex = 0;
                }

                // Match against local board config
                var config = MatchConfig(_releaseInfo.Configs, _localFiles);
                if (config == null) config = _releaseInfo.Configs[0];

                // Select the matched configuration in the dropdown
                var matchedItem = ConfigComboBox.Items.Cast<dynamic>()
                    .FirstOrDefault(item => item.Name == config);
                if (matchedItem != null)
                {
                    ConfigComboBox.SelectedItem = matchedItem;
                }

                // Update the file list for the selected configuration
                UpdateFileListForConfig();

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking updates: {ex.Message}");
            }
        }

        private string? MatchConfig(List<string> availableConfigs, List<LocalFile>? localFiles)
        {
            if (localFiles == null || !localFiles.Any())
                return null;

            // Create a dictionary to count matches for each config
            var configMatches = availableConfigs.ToDictionary(c => c, c => 0);

            // Check each local file against the config patterns
            foreach (var localFile in localFiles)
            {
                foreach (var config in availableConfigs)
                {
                    if (localFile.Filename.Contains(config, StringComparison.OrdinalIgnoreCase))
                    {
                        configMatches[config]++;
                    }
                }
            }

            // Find the config with the most matches
            var bestMatch = configMatches
                .OrderByDescending(kvp => kvp.Value)
                .FirstOrDefault();

            // Only return if we found at least one match
            return bestMatch.Value > 0 ? bestMatch.Key : null;
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in RemoteFilesList.Items)
            {
                var container = RemoteFilesList.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                var checkBox = FindVisualChild<CheckBox>(container);
                if (checkBox != null)
                {
                    checkBox.IsChecked = true;
                }
            }
        }

        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in RemoteFilesList.Items)
            {
                var container = RemoteFilesList.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                var checkBox = FindVisualChild<CheckBox>(container);
                if (checkBox != null)
                {
                    checkBox.IsChecked = false;
                }
            }
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            var filesToUpdate = new List<RemoteFile>();
            foreach (var item in RemoteFilesList.Items)
            {
                var container = RemoteFilesList.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                var checkBox = FindVisualChild<CheckBox>(container);
                if (checkBox != null && checkBox.IsChecked == true)
                {
                    var filename = checkBox.Content.ToString().Replace("__", "_");
                    var remoteFile = _remoteFiles?.FirstOrDefault(f => f.Filename == filename);
                    if (remoteFile != null)
                    {
                        filesToUpdate.Add(remoteFile);
                    }
                }
            }

            if (filesToUpdate.Any())
            {
                _ = PerformUpgrade(filesToUpdate);
            }
            else
            {
                MessageBox.Show("No files selected for update");
            }
        }

        private static T? FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T t)
                {
                    return t;
                }
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }

        private void UpdateFileListForConfig()
        {
            if (_releaseInfo == null || _remoteFiles == null) return;

            var selectedConfig = (ConfigComboBox.SelectedItem as dynamic)?.Name ?? string.Empty;
            RemoteFilesList.Items.Clear();

            var updatesAvailable = new List<RemoteFile>();
            foreach (var remoteFile in _remoteFiles)
            {
                // Always show firmware files, or files matching the selected config
                if (remoteFile.Filename.StartsWith("firmware_") && remoteFile.Filename.EndsWith(".bin") ||
                    !string.IsNullOrEmpty(selectedConfig) && remoteFile.Filename.Contains(selectedConfig, StringComparison.OrdinalIgnoreCase))
                {
                    var localFile = _localFiles?.FirstOrDefault(f => f.Filename == remoteFile.Filename);
                    if (localFile == null || localFile.Sha1 != remoteFile.Sha1)
                    {
                        updatesAvailable.Add(remoteFile);
                        RemoteFilesList.Items.Add(remoteFile.Filename.Replace("_", "__"));       // make sure _ is properly displayed
                    }
                }
            }
            
            // Select all files by default
            //SelectAll_Click(null, null);
            
            // Update the count display
            SelectedDriveText.Text = $"Selected drive: {DriveComboBox.SelectedItem} | {updatesAvailable.Count} updates available";
        }

        private async Task PerformUpgrade(List<RemoteFile> filesToUpdate)
        {
            var progressDialog = new ProgressDialog
            {
                Owner = this
            };

            try
            {
                var coresPath = Path.Combine(_selectedDrivePath, "cores");
                Directory.CreateDirectory(coresPath);

                progressDialog.Show();
                
                for (int i = 0; i < filesToUpdate.Count; i++)
                {
                    if (progressDialog.IsCancelled)
                    {
                        MessageBox.Show("Upgrade cancelled");
                        return;
                    }

                    var file = filesToUpdate[i];
                    progressDialog.UpdateProgress(i + 1, filesToUpdate.Count, file.Filename);

                    var url = $"https://github.com/nand2mario/tangcores/raw/main/files/{file.Filename}";
                    var destination = Path.Combine(coresPath, file.Filename);

                    var response = await _httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    await using var fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
                    await response.Content.CopyToAsync(fileStream);
                }

                progressDialog.Close();
                MessageBox.Show("Upgrade completed successfully!");
                UpdateLocalFilesList();
            }
            catch (Exception ex)
            {
                progressDialog.Close();
                MessageBox.Show($"Error during upgrade: {ex.Message}");
            }
        }
    }
}
