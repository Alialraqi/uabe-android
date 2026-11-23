using System.IO;

namespace UABEAvalonia
{
 /// <summary>Platform path configuration. The Android project sets it at startup ClassDataPath / CacheDir / PublicLogDir. </summary>
    public static class AppPaths
    {
        public static string ClassDataPath { get; set; } = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk");

        /// <summary>Platform cache directory (Android injects CacheDir.AbsolutePath). Used to copy streams returned by the picker into local files.</summary>
        public static string? CacheDir { get; set; }

        /// <summary>Public log directory (Android injects /storage/emulated/0/logs or private directory). null means no public log is written.</summary>
        public static string? PublicLogDir { get; set; }

        /// <summary>Get a usable cache directory. Prefers the injected CacheDir, otherwise the system temp directory.</summary>
        public static string GetCacheDir()
        {
            if (!string.IsNullOrEmpty(CacheDir) && Directory.Exists(CacheDir))
                return CacheDir;
            return Path.GetTempPath();
        }
    }
}
