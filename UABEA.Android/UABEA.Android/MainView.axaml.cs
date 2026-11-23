using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UABEAvalonia;

namespace UABEA.Android
{
    /// <summary>
    /// UABEA Android Text translation tool main view (portrait).
    /// Flow: choose directory → scan all Unity files → extract TextAsset + MonoBehaviour strings
    ///       → show in list → edit in app or export/Import TXT → save to the _edited subdirectory.
    /// </summary>
    public partial class MainView : UserControl
    {
        private AssetsManager _am = new AssetsManager();

        /// <summary>All text entries (can be filtered by search;_allEntries always the full set)</summary>
        private ObservableCollection<TextEntry> _entries = new();
        private List<TextEntry> _allEntries = new();

        /// <summary>Info of all loaded Unity files (used to locate them when saving)</summary>
        private List<LoadedUnityFile> _loadedFiles = new();

        /// <summary>Root directory currently scanned</summary>
        private string _rootDir = "";

        /// <summary>Currently selected entry</summary>
        private TextEntry? _selectedEntry;

        /// <summary>Currently pending file picker action</summary>
        private enum PendingAction
        {
            None,
            OpenDirectory,      // Choose scan directory
            SaveAllDir,         // Save All: choose output directory
            ExportTxtFile,      // Export TXT: choose file
            ImportTxtFile       // Import TXT: choose file
        }
        private PendingAction _pending = PendingAction.None;

        public MainView()
        {
            InitializeComponent();

            entryList.ItemsSource = _entries;
            entryList.SelectionChanged += EntryList_SelectionChanged;

            btnOpen.Click += BtnOpen_Click;
            btnSaveAll.Click += BtnSaveAll_Click;
            btnExportTxt.Click += BtnExportTxt_Click;
            btnImportTxt.Click += BtnImportTxt_Click;

            btnPrevEntry.Click += BtnPrevEntry_Click;
            btnNextEntry.Click += BtnNextEntry_Click;
            btnCopyToTrans.Click += BtnCopyToTrans_Click;
            btnApplyEntry.Click += BtnApplyEntry_Click;

            btnClearSearch.Click += (s, e) => { searchBox.Text = ""; };
            searchBox.TextChanged += (s, e) => ApplySearchFilter();
            translatedBox.TextChanged += (s, e) => UpdateEditStatus();

            Loaded += MainView_Loaded;
        }

        private void MainView_Loaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                var cdPath = AppPaths.ClassDataPath;
                if (File.Exists(cdPath))
                {
                    _am.LoadClassPackage(cdPath);
                    Log($"Loaded classdata.tpk: {cdPath}");
                    statusText.Text = "Ready - tap \"Open\" to choose a file directory";
                }
                else
                {
                    Log($"Warning: classdata.tpk not found: {cdPath}");
                    statusText.Text = "Warning: classdata.tpk missing, MonoBehaviour may not be parsed";
                }
            }
            catch (Exception ex)
            {
                Log("Initialization exception: " + ex);
            }
        }

        private void Log(string msg)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            try { CrashLogger.Log("MainView", msg); } catch { }
            logBox.Text = line + "\n" + logBox.Text;
            if (logBox.Text.Length > 5000) logBox.Text = logBox.Text.Substring(0, 5000);
        }

        private void ShowProgress(string msg)
        {
            progressText.Text = msg;
            progressOverlay.IsVisible = true;
        }

        private void HideProgress() => progressOverlay.IsVisible = false;

        // ==================== 1. Open directory ====================
        private void BtnOpen_Click(object? sender, RoutedEventArgs e)
        {
            _pending = PendingAction.OpenDirectory;
            ShowFileBrowser(FileBrowserView.BrowserMode.Directory);
        }

        // ==================== 2. Save All ====================
        private void BtnSaveAll_Click(object? sender, RoutedEventArgs e)
        {
            int modifiedCount = _allEntries.Count(x => x.IsModified);
            if (modifiedCount == 0)
            {
                statusText.Text = "No modifications to save";
                return;
            }
            _pending = PendingAction.SaveAllDir;
            ShowFileBrowser(FileBrowserView.BrowserMode.Directory, startDir: _rootDir);
        }

        // ==================== 3. Export/Import TXT ====================
        private void BtnExportTxt_Click(object? sender, RoutedEventArgs e)
        {
            if (_allEntries.Count == 0) { statusText.Text = "No text to export"; return; }
            _pending = PendingAction.ExportTxtFile;
            ShowFileBrowser(FileBrowserView.BrowserMode.Save,
                extensions: new HashSet<string> { "txt" },
                suggestedName: $"uabea-export-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        }

        private void BtnImportTxt_Click(object? sender, RoutedEventArgs e)
        {
            _pending = PendingAction.ImportTxtFile;
            ShowFileBrowser(FileBrowserView.BrowserMode.Open,
                extensions: new HashSet<string> { "txt" });
        }

        // ==================== File browser ====================
        private void ShowFileBrowser(FileBrowserView.BrowserMode mode,
            HashSet<string>? extensions = null, string? suggestedName = null, string? startDir = null)
        {
            fileBrowser.Initialize(startDir: startDir, extensions: extensions, mode: mode, suggestedFileName: suggestedName);
            fileBrowser.FileSelected -= FileBrowser_FileSelected;
            fileBrowser.Cancelled -= FileBrowser_Cancelled;
            fileBrowser.FileSelected += FileBrowser_FileSelected;
            fileBrowser.Cancelled += FileBrowser_Cancelled;
            fileBrowserOverlay.IsVisible = true;
        }

        private async void FileBrowser_FileSelected(object? sender, string? path)
        {
            fileBrowserOverlay.IsVisible = false;
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                switch (_pending)
                {
                    case PendingAction.OpenDirectory:
                        await ScanDirectoryAsync(path);
                        break;
                    case PendingAction.SaveAllDir:
                        await SaveAllToDirectoryAsync(path);
                        break;
                    case PendingAction.ExportTxtFile:
                        ExportToTxt(path);
                        break;
                    case PendingAction.ImportTxtFile:
                        await ImportFromTxtAsync(path);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log("Operation failed: " + ex);
                statusText.Text = "Operation failed: " + ex.Message;
            }
            finally
            {
                _pending = PendingAction.None;
            }
        }

        private void FileBrowser_Cancelled(object? sender, EventArgs e)
        {
            fileBrowserOverlay.IsVisible = false;
            _pending = PendingAction.None;
        }

        // ==================== Scan directory + extract text ====================
        private async Task ScanDirectoryAsync(string dir)
        {
            _rootDir = dir;
            _allEntries.Clear();
            _entries.Clear();
            _loadedFiles.Clear();
            UnloadAllFiles();

            ShowProgress("Scan directory...");
            Log($"Start scanning: {dir}");

            // Find all Unity asset files
            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "bundle", "assets", "unity3d", "ab", "dat" };
            var files = await Task.Run(() =>
            {
                var result = new List<string>();
                try
                {
                    foreach (var f in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
                    {
                        string ext = Path.GetExtension(f).TrimStart('.').ToLowerInvariant();
                        if (exts.Contains(ext))
                            result.Add(f);
                    }
                }
                catch (Exception ex) { Log("Enumerate files exception: " + ex.Message); }
                return result;
            });

            Log($"Found {files.Count} candidate files");
            topStatus.Text = $"Scanned {files.Count} files, extracting text...";

            // Open and extract one by one
            await Task.Run(() =>
            {
                int processed = 0;
                foreach (var file in files)
                {
                    processed++;
                    Dispatcher.UIThread.Post(() =>
                    {
                        progressText.Text = $"Extracting {processed}/{files.Count}\n{Path.GetFileName(file)}";
                    });
                    try
                    {
                        ExtractFromFile(file, dir);
                    }
                    catch (Exception ex)
                    {
                        Log($"Skip {Path.GetFileName(file)}: {ex.Message}");
                    }
                }
            });

            ApplySearchFilter();
            UpdateListStats();
            HideProgress();

            Log($"Scan finished, {_allEntries.Count} text entries in total");
            statusText.Text = $"Scan finished: {_allEntries.Count} text entries (from {_loadedFiles.Count} files)";
            topStatus.Text = $"{_allEntries.Count} entries total · {_loadedFiles.Count} files";

            btnSaveAll.IsEnabled = _allEntries.Count > 0;
            btnExportTxt.IsEnabled = _allEntries.Count > 0;
            btnImportTxt.IsEnabled = _allEntries.Count > 0;
        }

        /// <summary>Extract all text from a single Unity file</summary>
        private void ExtractFromFile(string filePath, string rootDir)
        {
            var fileType = FileTypeDetector.DetectFileType(filePath);
            if (fileType != DetectedFileType.BundleFile && fileType != DetectedFileType.AssetsFile)
                return;

            string relPath = Path.GetRelativePath(rootDir, filePath);
            BundleFileInstance? bundleInst = null;
            BundleWorkspace? bundleWorkspace = null;
            List<AssetsFileInstance> fileInstances = new();

            if (fileType == DetectedFileType.BundleFile)
            {
                bundleInst = _am.LoadBundleFile(filePath, false);
                bundleWorkspace = new BundleWorkspace();
                bundleWorkspace.Reset(bundleInst);
                foreach (var name in bundleInst.file.BlockAndDirInfo.DirectoryInfos.Select(d => d.Name))
                {
                    try
                    {
                        var inst = _am.LoadAssetsFileFromBundle(bundleInst, name, true);
                        if (inst != null) fileInstances.Add(inst);
                    }
                    catch { }
                }
            }
            else
            {
                var inst = _am.LoadAssetsFile(filePath, true);
                fileInstances.Add(inst);
            }

            var loadedFile = new LoadedUnityFile
            {
                FilePath = filePath,
                RelativePath = relPath,
                FileType = fileType,
                BundleInstance = bundleInst,
                BundleWorkspace = bundleWorkspace,
                FileInstances = fileInstances,
                ModifiedAssetIds = new HashSet<AssetID>()
            };

            // Create a temporary workspace for on-demand loading BaseField
            var assetWs = new AssetWorkspace(_am, bundleInst != null);
            foreach (var fi in fileInstances)
                assetWs.LoadAssetsFile(fi, false);

            foreach (var fi in fileInstances)
            {
                foreach (var info in fi.file.AssetInfos)
                {
                    try
                    {
                        int classId = info.TypeId;
                        // Only handle TextAsset(49) and MonoBehaviour(114)
                        if (classId != 49 && classId != 114) continue;

                        var cont = new AssetContainer(info, fi);
                        var baseField = assetWs.GetBaseField(cont);
                        if (baseField == null) continue;

                        if (classId == 49)
                        {
                            // TextAsset:Take the m_Script large text
                            var es = StringExtractor.ExtractTextAsset(baseField);
                            if (es != null && !string.IsNullOrEmpty(es.Original))
                            {
                                _allEntries.Add(new TextEntry
                                {
                                    FilePath = relPath,
                                    PathId = info.PathId,
                                    FieldPath = "m_Script",
                                    Original = es.Original,
                                    Translated = ""
                                });
                            }
                        }
                        else
                        {
                            // MonoBehaviour:Recursively extract all strings
                            var extractor = new StringExtractor();
                            extractor.Extract(baseField);
                            foreach (var s in extractor.Strings)
                            {
                                if (string.IsNullOrEmpty(s.Original)) continue;
                                // Skip obvious code fields (m_Name in MonoBehaviour is usually the asset name, not translated)
                                if (s.Path == "m_Name") continue;

                                _allEntries.Add(new TextEntry
                                {
                                    FilePath = relPath,
                                    PathId = info.PathId,
                                    FieldPath = s.Path,
                                    Original = s.Original,
                                    Translated = ""
                                });
                            }
                        }
                    }
                    catch { /* Skip a single asset on failure */ }
                }
            }

            _loadedFiles.Add(loadedFile);
        }

        private void UnloadAllFiles()
        {
            try
            {
                _am.UnloadAllAssetsFiles(true);
                _am.UnloadAllBundleFiles();
            }
            catch { }
        }

        // ==================== List / Search / Selected ====================
        private void ApplySearchFilter()
        {
            string keyword = searchBox.Text?.Trim() ?? "";
            var filtered = string.IsNullOrEmpty(keyword)
                ? _allEntries
                : _allEntries.Where(e =>
                    e.Original.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    e.Translated.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    e.FieldPath.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    e.FilePath.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

            _entries.Clear();
            foreach (var e in filtered)
                _entries.Add(e);

            resultCount.Text = _allEntries.Count > 0
                ? $"Showing {filtered.Count} / {_allEntries.Count}"
                : "";
            UpdateListStats();
        }

        private void UpdateListStats()
        {
            int total = _allEntries.Count;
            int modified = _allEntries.Count(x => x.IsModified);
            listStats.Text = total > 0 ? $"{total} entries · modified {modified}" : "";
        }

        private void EntryList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (entryList.SelectedItem is TextEntry entry)
            {
                _selectedEntry = entry;
                originalBox.Text = entry.Original;
                translatedBox.Text = entry.Translated;
                editStatus.Text = $"{entry.FilePath}#{entry.PathId}";
            }
        }

        // ==================== Edit actions ====================
        private void BtnCopyToTrans_Click(object? sender, RoutedEventArgs e)
        {
            if (_selectedEntry == null) return;
            translatedBox.Text = _selectedEntry.Original;
        }

        private void BtnApplyEntry_Click(object? sender, RoutedEventArgs e)
        {
            if (_selectedEntry == null) { statusText.Text = "Please select an entry first"; return; }
            _selectedEntry.Translated = translatedBox.Text ?? "";
            UpdateListStats();
            statusText.Text = "Translation applied to the current entry";

            // Trigger list refresh (so the translation shows on the left)
            int idx = _entries.IndexOf(_selectedEntry);
            if (idx >= 0)
            {
                var tmp = _entries[idx];
                _entries[idx] = tmp; // Reassign directly to trigger binding refresh
            }
        }

        private void BtnPrevEntry_Click(object? sender, RoutedEventArgs e)
        {
            int idx = entryList.SelectedIndex;
            if (idx > 0) entryList.SelectedIndex = idx - 1;
        }

        private void BtnNextEntry_Click(object? sender, RoutedEventArgs e)
        {
            int idx = entryList.SelectedIndex;
            if (idx >= 0 && idx < _entries.Count - 1) entryList.SelectedIndex = idx + 1;
        }

        private void UpdateEditStatus()
        {
            if (_selectedEntry == null) return;
            string orig = originalBox.Text ?? "";
            string trans = translatedBox.Text ?? "";
            if (trans == orig)
                editStatus.Text = "Translation equals original (unmodified)";
            else if (string.IsNullOrEmpty(trans))
                editStatus.Text = "Translation is empty";
            else
                editStatus.Text = "Modified (pending apply)";
        }

        // ==================== Save to directory ====================
        private async Task SaveAllToDirectoryAsync(string outDir)
        {
            // Collect modifications of each file
            var modifiedEntries = _allEntries.Where(x => x.IsModified).ToList();
            if (modifiedEntries.Count == 0)
            {
                statusText.Text = "No modifications";
                return;
            }

            // Group by file: multiple modifications in the same file are written back at once
            var byFile = modifiedEntries.GroupBy(x => x.FilePath).ToList();
            Log($"Save: {modifiedEntries.Count} modifications, involving {byFile.Count} files");

            ShowProgress("Saving...");

            // Reload the file, apply modifications and write out
            int savedFiles = await Task.Run(() =>
            {
                int count = 0;
                foreach (var group in byFile)
                {
                    try
                    {
                        SaveOneFile(group.Key, group.ToList(), outDir);
                        count++;
                        Dispatcher.UIThread.Post(() => { progressText.Text = $"Saving {count}/{byFile.Count}"; });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.UIThread.Post(() => Log($"Save failed {group.Key}: {ex.Message}"));
                    }
                }
                return count;
            });

            HideProgress();
            Log($"Save finished: {savedFiles}/{byFile.Count} files → {outDir}");
            statusText.Text = $"Saved {savedFiles} files to {Path.GetFileName(outDir)}";
        }

        /// <summary>Write modifications of a single file back, output to outDir (keeping the original relative path structure)</summary>
        private void SaveOneFile(string relPath, List<TextEntry> entries, string outDir)
        {
            string srcPath = Path.Combine(_rootDir, relPath);
            string destPath = Path.Combine(outDir, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

            var fileType = FileTypeDetector.DetectFileType(srcPath);
            var am = new AssetsManager();
            var cdPath = AppPaths.ClassDataPath;
            if (File.Exists(cdPath)) am.LoadClassPackage(cdPath);

            // Group by PathId: modifications of the same asset are merged
            var byAsset = entries.GroupBy(e => e.PathId).ToList();

            if (fileType == DetectedFileType.BundleFile)
            {
                var bundleInst = am.LoadBundleFile(srcPath, false);
                var bw = new BundleWorkspace();
                bw.Reset(bundleInst);

                var dirInfos = bundleInst.file.BlockAndDirInfo.DirectoryInfos;
                foreach (var di in dirInfos)
                {
                    var inst = am.LoadAssetsFileFromBundle(bundleInst, di.Name, false);
                    if (inst == null) continue;
                    var replacers = BuildReplacersForFile(am, inst, byAsset);
                    if (replacers.Count > 0)
                    {
                        using var ms = new MemoryStream();
                        using var w = new AssetsFileWriter(ms);
                        inst.file.Write(w, 0, replacers);
                        bw.AddOrReplaceFile(new MemoryStream(ms.ToArray()), di.Name, true);
                    }
                }

                var bundleReplacers = bw.GetReplacers();
                using (var fs = File.Open(destPath, FileMode.Create))
                using (var w = new AssetsFileWriter(fs))
                    bundleInst.file.Write(w, bundleReplacers);
            }
            else if (fileType == DetectedFileType.AssetsFile)
            {
                var inst = am.LoadAssetsFile(srcPath, false);
                var ws = new AssetWorkspace(am, false);
                ws.LoadAssetsFile(inst, false);
                var replacers = BuildReplacersForFile(am, inst, byAsset);
                if (replacers.Count > 0)
                {
                    using var fs = File.Open(destPath, FileMode.Create);
                    using var w = new AssetsFileWriter(fs);
                    inst.file.Write(w, 0, replacers);
                }
                else
                {
                    // Without changes, should the .assets file be copied as is? No, only files with changes are saved
                    File.Copy(srcPath, destPath, true);
                }
            }
        }

        /// <summary>Build all for one .assets file replacer</summary>
        private List<AssetsReplacer> BuildReplacersForFile(AssetsManager am, AssetsFileInstance inst,
            List<IGrouping<long, TextEntry>> byAsset)
        {
            var ws = new AssetWorkspace(am, false);
            ws.LoadAssetsFile(inst, false);
            var replacers = new List<AssetsReplacer>();

            foreach (var group in byAsset)
            {
                long pathId = group.Key;
                AssetFileInfo? info = null;
                foreach (var ai in inst.file.AssetInfos)
                {
                    if (ai.PathId == pathId) { info = ai; break; }
                }
                if (info == null) continue;

                var cont = new AssetContainer(info, inst);
                var baseField = ws.GetBaseField(cont);
                if (baseField == null) continue;

                int classId = info.TypeId;

                if (classId == 49)
                {
                    // TextAsset m_Script:Single
                    var entry = group.FirstOrDefault();
                    if (entry != null && entry.IsModified)
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(entry.Translated);
                        baseField["m_Script"].AsByteArray = bytes;
                    }
                }
                else if (classId == 114)
                {
                    // MonoBehaviour:may have multiple modifications
                    var modifiedByPath = group.Where(x => x.IsModified)
                        .ToDictionary(x => x.FieldPath, x => x.Translated);
                    if (modifiedByPath.Count > 0)
                    {
                        ApplyTranslationsToField(baseField, "", modifiedByPath);
                    }
                }

                byte[] savedBytes = baseField.WriteToByteArray();
                replacers.Add(new AssetsReplacerFromMemory(
                    info.PathId, info.TypeId, info.ScriptTypeIndex, savedBytes));
            }
            return replacers;
        }

        /// <summary>Recursively apply the translation to the corresponding field path (kept consistent with StringExtractor path generation)</summary>
        private void ApplyTranslationsToField(AssetTypeValueField field, string currentPath,
            Dictionary<string, string> translations)
        {
            if (field == null) return;

            if (field.Value != null && field.Value.ValueType == AssetValueType.String)
            {
                if (translations.TryGetValue(currentPath, out string? val))
                {
                    field.AsString = val;
                }
                return;
            }

            if (field.Value != null && field.Value.ValueType == AssetValueType.ManagedReferencesRegistry)
            {
                var registry = field.AsManagedReferencesRegistry;
                if (registry?.references != null)
                {
                    for (int i = 0; i < registry.references.Count; i++)
                    {
                        ApplyTranslationsToField(registry.references[i].data,
                            $"{currentPath}[{i}].", translations);
                    }
                }
                return;
            }

            var template = field.TemplateField;
            bool isArray = template != null && template.IsArray;
            var children = field.Children;
            if (children == null || children.Count == 0) return;

            if (isArray)
            {
                for (int i = 0; i < children.Count; i++)
                    ApplyTranslationsToField(children[i], $"{currentPath}[{i}]", translations);
            }
            else
            {
                foreach (var child in children)
                {
                    if (child == null) continue;
                    string childName = child.TemplateField?.Name ?? "?";
                    string childPath = string.IsNullOrEmpty(currentPath) ? childName : $"{currentPath}.{childName}";
                    ApplyTranslationsToField(child, childPath, translations);
                }
            }
        }

        // ==================== Export / Import TXT ====================
        private void ExportToTxt(string path)
        {
            try
            {
                TextEntryTxtIo.ExportToFile(_allEntries, path);
                Log($"Exported {_allEntries.Count} entries to {path}");
                statusText.Text = $"Exported {_allEntries.Count} entries → {Path.GetFileName(path)}";
            }
            catch (Exception ex)
            {
                Log("Export failed: " + ex);
                statusText.Text = "Export failed: " + ex.Message;
            }
        }

        private async Task ImportFromTxtAsync(string path)
        {
            int applied = await Task.Run(() =>
            {
                var map = TextEntryTxtIo.ImportFromFile(path);
                int count = 0;
                foreach (var e in _allEntries)
                {
                    string key = $"{e.FilePath}::{e.PathId}::{e.FieldPath}";
                    if (map.TryGetValue(key, out string? translated))
                    {
                        if (!string.IsNullOrEmpty(translated) && translated != e.Original)
                        {
                            e.Translated = translated;
                            count++;
                        }
                    }
                }
                return count;
            });

            ApplySearchFilter();
            UpdateListStats();
            Log($"Translations imported: {applied} entries");
            statusText.Text = $"Imported {applied} translations";

            // Refresh current selection
            if (_selectedEntry != null)
            {
                originalBox.Text = _selectedEntry.Original;
                translatedBox.Text = _selectedEntry.Translated;
            }
        }
    }

    /// <summary>Loaded Unity file info</summary>
    internal class LoadedUnityFile
    {
        public string FilePath { get; set; } = "";
        public string RelativePath { get; set; } = "";
        public DetectedFileType FileType { get; set; }
        public BundleFileInstance? BundleInstance { get; set; }
        public BundleWorkspace? BundleWorkspace { get; set; }
        public List<AssetsFileInstance> FileInstances { get; set; } = new();
        public HashSet<AssetID> ModifiedAssetIds { get; set; } = new();
    }
}
