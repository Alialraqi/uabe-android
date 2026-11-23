using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Provider;
using Android.Widget;
using Avalonia;
using Avalonia.Android;
using System;
using System.IO;
using UABEAvalonia;

namespace UABEA.Android.Android;

[Activity(
    Label = "UABEA",
    Theme = "@style/MyTheme.NoActionBar",
    MainLauncher = true,
    Exported = true,
    ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<global::UABEA.Android.App>
{
    private const int RequestStoragePermId = 1001;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // ===== 1. Set up earliest: log path + clipboard + Toast (before any code that may crash)=====
        global::UABEA.Android.CrashLogger.SetPrivateLogDir(FilesDir!.AbsolutePath);
        // First try to set the public log directory (fails safely even without permission, only writes to the private directory)
        TryEnablePublicLog();

        // Inject clipboard write ability: use Android's native ClipboardManager, always writable
        global::UABEA.Android.CrashLogger.ClipboardWriter = text =>
        {
            try
            {
                var clipboard = (ClipboardManager)GetSystemService(ClipboardService);
                var clip = ClipData.NewPlainText("UABEA Crash Log", text);
                clipboard.PrimaryClip = clip;
            }
            catch { }
        };

        // Inject Toast notification ability
        global::UABEA.Android.CrashLogger.ToastShower = msg =>
        {
            try { RunOnUiThread(() => Toast.MakeText(this, msg, ToastLength.Long)?.Show()); }
            catch { }
        };

        // ===== 2. Install global exception capture (managed + Android runtime)=====
        global::UABEA.Android.CrashLogger.InstallManagedHandlers();
        AndroidEnvironment.UnhandledExceptionRaiser += (s, e) =>
        {
            global::UABEA.Android.CrashLogger.Log("AndroidEnv.UnhandledException", e.Exception);
            global::UABEA.Android.CrashLogger.CopyCrashToClipboard(
                global::UABEA.Android.CrashLogger.BuildCrashReport("AndroidEnv.UnhandledException", e.Exception));
            e.Handled = true;
        };

        global::UABEA.Android.CrashLogger.Log("MainActivity", $"OnCreate start, sdk={Build.VERSION.SdkInt}");

        try
        {
            ExtractClassData();
            base.OnCreate(savedInstanceState);
            global::UABEA.Android.CrashLogger.Log("MainActivity", "OnCreate base ok");

            // ===== 3. Actively request storage permission =====
            RequestStoragePermissions();
        }
        catch (Exception ex)
        {
            global::UABEA.Android.CrashLogger.Log("MainActivity.OnCreate", ex);
            global::UABEA.Android.CrashLogger.IsCrashed = true;
            global::UABEA.Android.CrashLogger.CopyCrashToClipboard(
                global::UABEA.Android.CrashLogger.BuildCrashReport("MainActivity.OnCreate", ex));
        }
    }

    /// <summary>Try to enable the public log directory. Android 11+ needs MANAGE_EXTERNAL_STORAGE, otherwise fall back to the app private directory.</summary>
    private void TryEnablePublicLog()
    {
        try
        {
            // Android 11 (R, API 30) and above requires MANAGE_EXTERNAL_STORAGE to write /storage/emulated/0/logs
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                if (global::Android.OS.Environment.IsExternalStorageManager)
                {
                    global::UABEA.Android.CrashLogger.SetPublicLogDir("/storage/emulated/0/logs");
                    global::UABEA.Android.CrashLogger.Log("TryEnablePublicLog", "MANAGE_EXTERNAL_STORAGE Granted, writing public logs /storage/emulated/0/logs");
                }
                else
                {
                    // Not granted, falling back to the app-specific external directory (no permission needed)
                    var extDir = GetExternalFilesDir(null)?.AbsolutePath;
                    if (!string.IsNullOrEmpty(extDir))
                    {
                        var logDir = Path.Combine(extDir, "logs");
                        global::UABEA.Android.CrashLogger.SetPublicLogDir(logDir);
                        global::UABEA.Android.CrashLogger.Log("TryEnablePublicLog", $"MANAGE_EXTERNAL_STORAGE Not granted, logs fall back to {logDir}");
                    }
                }
            }
            else
            {
                // Android 10 and below, legacy permission is enough
                global::UABEA.Android.CrashLogger.SetPublicLogDir("/storage/emulated/0/logs");
                global::UABEA.Android.CrashLogger.Log("TryEnablePublicLog", "Android<=10, Public log writing /storage/emulated/0/logs");
            }
        }
        catch (Exception ex)
        {
            global::UABEA.Android.CrashLogger.Log("TryEnablePublicLog", ex);
        }
    }

    /// <summary>Request storage permission. On Android 11+ guide the user to Settings to enable MANAGE_EXTERNAL_STORAGE.</summary>
    private void RequestStoragePermissions()
    {
        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
 // Android 11+: MANAGE_EXTERNAL_STORAGE is required to access the public directory
                if (!global::Android.OS.Environment.IsExternalStorageManager)
                {
                    global::UABEA.Android.CrashLogger.Log("RequestStorage", "Request MANAGE_EXTERNAL_STORAGE (jump to Settings)");
                    try
                    {
                        var intent = new Intent(Settings.ActionManageAppAllFilesAccessPermission);
                        intent.SetData(global::Android.Net.Uri.Parse("package:" + PackageName));
                        StartActivityForResult(intent, RequestStoragePermId);
                    }
                    catch (Exception ex)
                    {
                        global::UABEA.Android.CrashLogger.Log("RequestStorage.ManageAll", ex);
                        // Some devices do not support this Intent; fall back to the normal permission request
                        RequestLegacyStoragePermissions();
                    }
                }
                else
                {
                    global::UABEA.Android.CrashLogger.Log("RequestStorage", "MANAGE_EXTERNAL_STORAGE Already granted");
                }
            }
            else
            {
                RequestLegacyStoragePermissions();
            }
        }
        catch (Exception ex)
        {
            global::UABEA.Android.CrashLogger.Log("RequestStorage", ex);
        }
    }

    /// <summary>Request legacy storage permission (Android 10 and below)</summary>
    private void RequestLegacyStoragePermissions()
    {
        try
        {
            string[] perms = {
                global::Android.Manifest.Permission.ReadExternalStorage,
                global::Android.Manifest.Permission.WriteExternalStorage
            };
            var need = new System.Collections.Generic.List<string>();
            foreach (var p in perms)
            {
                if (CheckSelfPermission(p) != Permission.Granted)
                    need.Add(p);
            }
            if (need.Count > 0)
            {
                global::UABEA.Android.CrashLogger.Log("RequestLegacy", "Request permission: " + string.Join(", ", need));
                RequestPermissions(need.ToArray(), RequestStoragePermId);
            }
            else
            {
                global::UABEA.Android.CrashLogger.Log("RequestLegacy", "Legacy storage permission already granted");
            }
        }
        catch (Exception ex)
        {
            global::UABEA.Android.CrashLogger.Log("RequestLegacy", ex);
        }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        try
        {
            if (requestCode == RequestStoragePermId)
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.R && global::Android.OS.Environment.IsExternalStorageManager)
                {
                    global::UABEA.Android.CrashLogger.SetPublicLogDir("/storage/emulated/0/logs");
                    global::UABEA.Android.CrashLogger.Log("OnActivityResult", "The user granted MANAGE_EXTERNAL_STORAGE; public logs switched to /storage/emulated/0/logs");
                    global::UABEA.Android.CrashLogger.ToastShower?.Invoke("All files access permission obtained; logs will be written to /storage/emulated/0/logs/");
                }
                else
                {
                    global::UABEA.Android.CrashLogger.Log("OnActivityResult", "The user did not grant MANAGE_EXTERNAL_STORAGE; continuing to use the app private directory");
                }
            }
        }
        catch (Exception ex)
        {
            global::UABEA.Android.CrashLogger.Log("OnActivityResult", ex);
        }
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        try
        {
            if (requestCode == RequestStoragePermId)
            {
                bool allGranted = grantResults.Length > 0;
                foreach (var r in grantResults)
                {
                    if (r != Permission.Granted) allGranted = false;
                }
                global::UABEA.Android.CrashLogger.Log("OnRequestPermResult", $"Legacy permission result: allGranted={allGranted}");
            }
        }
        catch (Exception ex)
        {
            global::UABEA.Android.CrashLogger.Log("OnRequestPermResult", ex);
        }
    }

    private void ExtractClassData()
    {
        try
        {
            var path = Path.Combine(FilesDir!.AbsolutePath, "classdata.tpk");
            if (!File.Exists(path))
            {
                using var src = Assets!.Open("classdata.tpk");
                using var dst = File.Create(path);
                src.CopyTo(dst);
            }
            AppPaths.ClassDataPath = path;
            global::UABEA.Android.CrashLogger.Log("ExtractClassData", $"path={path}");
        }
        catch (Exception ex)
        {
            global::UABEA.Android.CrashLogger.Log("ExtractClassData", ex);
        }
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        global::UABEA.Android.CrashLogger.Log("CustomizeAppBuilder", "start");
        try
        {
            var b = base.CustomizeAppBuilder(builder).LogToTrace();
            global::UABEA.Android.CrashLogger.Log("CustomizeAppBuilder", "ok");
            return b;
        }
        catch (Exception ex)
        {
            global::UABEA.Android.CrashLogger.Log("CustomizeAppBuilder", ex);
            global::UABEA.Android.CrashLogger.CopyCrashToClipboard(
                global::UABEA.Android.CrashLogger.BuildCrashReport("CustomizeAppBuilder", ex));
            throw;
        }
    }
}
