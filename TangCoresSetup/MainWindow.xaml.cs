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

namespace TangCoresSetup
{
    public partial class MainWindow : Window
    {
        private const string UpdateUrl = "https://raw.githubusercontent.com/nand2mario/tangcores/main/files/list.json";
        private readonly HttpClient _httpClient = new();
        private string? _selectedDrivePath;
        private List<RemoteFile>? _remoteFiles;
        private List<LocalFile>? _localFiles;

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
            public List<RemoteFile> Files { get; set; } = new();
            public List<Release> Releases { get; set; } = new();
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
                var json = """
                {
                    "files": [
                	    {"sha1": "fdc779c150a624f9303b80392baa6f1cf338cc16", "filename": "firmware_20250131.bin"}
                    ],
                    "releases"": [
                        {"name": "v1.0", "files": ["firmware_20250131.bin"]}
                    ]
                }
                """;
                var releaseInfo = JsonSerializer.Deserialize<ReleaseInfo>(json);

                if (releaseInfo == null || !releaseInfo.Files.Any())
                {
                    MessageBox.Show("Failed to parse update information");
                    return;
                }

                _remoteFiles = releaseInfo.Files;
                RemoteFilesList.Items.Clear();

                // Find files that need updating
                var updatesAvailable = new List<RemoteFile>();
                foreach (var remoteFile in _remoteFiles)
                {
                    var localFile = _localFiles?.FirstOrDefault(f => f.Filename == remoteFile.Filename);
                    if (localFile == null || localFile.Sha1 != remoteFile.Sha1)
                    {
                        updatesAvailable.Add(remoteFile);
                        RemoteFilesList.Items.Add($"{remoteFile.Filename} (new)");
                    }
                }

                if (!updatesAvailable.Any())
                {
                    MessageBox.Show("Your TangCores are up to date!");
                    return;
                }

                var result = MessageBox.Show(
                    $"{updatesAvailable.Count} updates available. Do you want to upgrade?",
                    "Updates Available",
                    MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                {
                    await PerformUpgrade(updatesAvailable);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking updates: {ex.Message}");
            }
        }

        private async Task PerformUpgrade(List<RemoteFile> filesToUpdate)
        {
            try
            {
                var coresPath = Path.Combine(_selectedDrivePath, "cores");
                Directory.CreateDirectory(coresPath);

                foreach (var file in filesToUpdate)
                {
                    var url = $"https://github.com/nand2mario/tangcores/raw/main/files/{file.Filename}";
                    var destination = Path.Combine(coresPath, file.Filename);

                    var response = await _httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    await using var fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
                    await response.Content.CopyToAsync(fileStream);
                }

                MessageBox.Show("Upgrade completed successfully!");
                UpdateLocalFilesList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during upgrade: {ex.Message}");
            }
        }
    }
}
