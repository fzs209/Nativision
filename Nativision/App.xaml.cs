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

            // Last-resort exception capture. A XAML stowed exception
            // (0xc000027b in the Windows event log) otherwise kills the app
            // with no usable detail; log the real exception to crash.log.
            this.UnhandledException += OnAppUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                CrashLog.Write("AppDomain.UnhandledException (process exiting)", e.ExceptionObject as Exception);
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                CrashLog.Write("TaskScheduler.UnobservedTaskException", e.Exception);
                e.SetObserved();
            };
            CrashLog.WriteMessage("App start", "Nativision started; exception capture registered. Log file: " + CrashLog.LogPath);
        }

        private void OnAppUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            CrashLog.Write("Application.UnhandledException", e.Exception);
            // Keep the app alive: the failure is in the log, and dying here
            // would lose the playback state along with the error detail.
            e.Handled = true;
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
