using System;
using System.IO;
using UABEAvalonia;

namespace UABEA.Android
{
    /// <summary>Global crash log utility, dual write: private directory (guaranteed) + public logs directory</summary>
    public static class CrashLogger
    {
        public static string PrivateLogPath { get; internal set; } = "/data/local/tmp/uabea.log";

        /// <summary>Public log directory. Injected by MainActivity. When null, only the private directory is written.</summary>
        public static string? PublicLogDir { get; set; } = "/storage/emulated/0/logs";

        /// <summary>Actual public log file path (derived from PublicLogDir)</summary>
        public static string PublicLogPath => string.IsNullOrEmpty(PublicLogDir)
            ? "/storage/emulated/0/logs/uabea.log"
            : Path.Combine(PublicLogDir, "uabea.log");

        /// <summary>Clipboard write method injected by the Android side. Any crash can copy the log to the clipboard</summary>
        public static Action<string>? ClipboardWriter { get; set; }

        /// <summary>Toast notification method injected by the Android side</summary>
        public static Action<string>? ToastShower { get; set; }

        /// <summary>Private directory path injected by Android MainActivity at startup</summary>
        public static void SetPrivateLogDir(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                PrivateLogPath = Path.Combine(dir, "uabea.log");
                // Synced to AppPaths for other modules to use
                AppPaths.CacheDir = dir;
            }
            catch { }
        }

        /// <summary>Set the public log directory (injected by MainActivity after permission is confirmed)</summary>
        public static void SetPublicLogDir(string? dir)
        {
            PublicLogDir = dir;
            AppPaths.PublicLogDir = dir;
        }

        public static void Log(string source, string message)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{source}] {message}\n";
            try { File.AppendAllText(PrivateLogPath, line); } catch { }
            // Public log: only attempted when PublicLogDir is non-empty and writable
            if (!string.IsNullOrEmpty(PublicLogDir))
            {
                try
                {
                    Directory.CreateDirectory(PublicLogDir);
                    File.AppendAllText(PublicLogPath, line);
                }
                catch { }
            }
        }

        public static void Log(string source, Exception ex) => Log(source, ex?.ToString() ?? "(null)");

        public static string ReadAll()
        {
            try { return File.ReadAllText(PrivateLogPath); }
            catch { return "(Unable to read log)"; }
        }

        /// <summary>Write crash info to the clipboard + Toast notification. Can be pasted and viewed in any app</summary>
        public static void CopyCrashToClipboard(string crashInfo)
        {
            try { ClipboardWriter?.Invoke(crashInfo); } catch { }
            try { ToastShower?.Invoke("UABEA Crash log copied to clipboard; paste it in any input box to view"); } catch { }
        }

        /// <summary>Crash flag. Set to true</summary>
        public static bool IsCrashed { get; set; } = false;

        public static void InstallManagedHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                Log("AppDomain.UnhandledException", ex);
                CopyCrashToClipboard(BuildCrashReport("AppDomain.UnhandledException", ex));
            };
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Log("TaskScheduler.Unobserved", e.Exception);
                CopyCrashToClipboard(BuildCrashReport("TaskScheduler.Unobserved", e.Exception));
                e.SetObserved();
            };
        }

        public static string BuildCrashReport(string source, Exception? ex)
        {
            var report = $"===== UABEA Crash report =====\n";
            report += $"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            report += $"Source: {source}\n";
            report += $"Device: {GetDeviceInfo()}\n";
            report += $"------------------------\n";
            report += $"{ex?.ToString() ?? "(No exception object)"}\n";
            report += $"------------------------\n";
            report += $"=== Full startup log ===\n{ReadAll()}";
            return report;
        }

        private static string GetDeviceInfo()
        {
            try { return Environment.OSVersion + " / " + Environment.MachineName; }
            catch { return "unknown"; }
        }
    }
}
