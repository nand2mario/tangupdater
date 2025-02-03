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
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsCancelled = true;
            Close();
        }
    }
}
