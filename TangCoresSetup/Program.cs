using System;
using System.Windows;

namespace TangCoresSetup
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            Application app = new Application();
            app.Run(new MainWindow());
        }
    }
}
