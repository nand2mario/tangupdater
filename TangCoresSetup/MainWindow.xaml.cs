using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace TangCoresSetup
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
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
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error detecting drives: {ex.Message}");
            }
        }

        private void DriveComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DriveComboBox.SelectedItem != null)
            {
                SelectedDriveText.Text = $"Selected drive: {DriveComboBox.SelectedItem}";
            }
        }

        private void RefreshDrives_Click(object sender, RoutedEventArgs e)
        {
            RefreshDriveList();
        }
    }
}
