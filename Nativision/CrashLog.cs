using System;
using System.IO;
using System.Text;

namespace Nativision
{
    // Appends exception records to %LOCALAPPDATA%\Nativision\crash.log.
    // The Windows event log only shows the generic 0xc000027b for a XAML
    // stowed-exception crash; this file keeps the actual exception type,
    // message and stack so the cause can be identified after the fact.
    // Every method swallows its own failures: logging must never crash the app.
    internal static class CrashLog
    {
        private static readonly object Gate = new object();

        public static string LogPath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Nativision");
                return Path.Combine(dir, "crash.log");
            }
        }

        public static void Write(string source, Exception ex)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("==================================================");
                sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  source: " + source);
                AppendException(sb, ex, 0);
                lock (Gate)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                    File.AppendAllText(LogPath, sb.ToString());
                }
            }
            catch { }
        }

        public static void WriteMessage(string source, string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                    File.AppendAllText(LogPath,
                        "==================================================\n" +
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  source: " + source + "\n" +
                        message + "\n");
                }
            }
            catch { }
        }

        private static void AppendException(StringBuilder sb, Exception ex, int depth)
        {
            if (ex == null || depth > 6) return;
            sb.AppendLine("type: " + ex.GetType().FullName);
            sb.AppendLine("hresult: 0x" + ex.HResult.ToString("X8"));
            sb.AppendLine("message: " + ex.Message);
            if (!string.IsNullOrEmpty(ex.StackTrace))
                sb.AppendLine("stack:\n" + ex.StackTrace);
            if (ex.InnerException != null)
            {
                sb.AppendLine("--- inner exception ---");
                AppendException(sb, ex.InnerException, depth + 1);
            }
        }
    }
}
