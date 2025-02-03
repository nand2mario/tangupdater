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
            ProgressBar.Value = current * 100 / total;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsCancelled = true;
            Close();
        }
    }
}
