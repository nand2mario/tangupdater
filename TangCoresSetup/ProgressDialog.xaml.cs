using System.Windows;

namespace TangCoresSetup
{
    public partial class ProgressDialog : Window
    {
        public bool IsCancelled { get; private set; }

        public ProgressDialog()
        {
            InitializeComponent();
        }

        public void UpdateProgress(int current, int total, string filename)
        {
            StatusText.Text = $"Downloading {filename} ({current}/{total})";
            OverallProgressBar.Value = current * 100 / total;
        }

        public void UpdateFileProgress(long bytesReceived, long totalBytes)
        {
            if (totalBytes > 0)
            {
                FileProgressBar.Value = bytesReceived * 100 / totalBytes;
                FileSizeText.Text = FormatBytes(bytesReceived) + " / " + FormatBytes(totalBytes);
            }
        }

        private static string FormatBytes(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;

            if (bytes >= MB)
                return $"{bytes / MB:0.0} MB";
            if (bytes >= KB)
                return $"{bytes / KB:0} KB";
            return $"{bytes} B";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsCancelled = true;
            Close();
        }
    }
}
