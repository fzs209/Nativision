using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI.Xaml;

namespace Nativision
{
    public partial class App : Application
    {
        public static Window MainWindow;

        // Files passed on the command line, e.g. Nativision.exe "D:\movie.mp4"
        // (also what an "Open with" entry pointing at the .exe passes in).
        public static readonly List<string> StartupFiles = new List<string>();

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                var argv = Environment.GetCommandLineArgs();
                for (int i = 1; i < argv.Length; i++)
                {
                    if (File.Exists(argv[i])) StartupFiles.Add(argv[i]);
                }
            }
            catch { }

            MainWindow = new MainWindow();
            MainWindow.Activate();
        }
    }
}
