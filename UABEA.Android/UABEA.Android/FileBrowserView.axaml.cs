using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using UABEAvalonia;

namespace UABEA.Android
{
    /// <summary>
    /// Built-in file picker.
    /// Traverses storage directories directly via System.IO without calling the system StorageProvider,
    /// to avoid the app being reclaimed when it goes to background (on Android the system file picker is a separate Activity,
    /// and the UABEA process is easily killed by the system during the switch).
    /// Three modes supported: Open (pick a file), Save (pick a directory + enter file name), Directory (pick a directory).
    /// </summary>
    public partial class FileBrowserView : UserControl
    {
        /// <summary>File picker mode</summary>
        public enum BrowserMode
        {
            /// <summary>Open file: pick an existing file</summary>
            Open,
            /// <summary>Save file: pick a directory and enter a file name</summary>
            Save,
            /// <summary>Select directory</summary>
            Directory
        }

        /// <summary>File list item</summary>
        public class FileEntry
        {
            public string Name { get; set; } = "";
            public string FullPath { get; set; } = "";
            public bool IsDirectory { get; set; }
            public string Icon => IsDirectory ? "\U0001F4C1" : "\U0001F4C4";
            public long Size { get; set; }
        }

        private ObservableCollection<FileEntry> _entries = new();
        private FileEntry? _selectedFile;
        private string _currentDir;
        private BrowserMode _mode = BrowserMode.Open;

        /// <summary>Selected file path (valid after confirmation)</summary>
        public string? SelectedFilePath { get; private set; }

        /// <summary>Selected directory path (valid after confirmation in Directory mode)</summary>
        public string? SelectedDirectoryPath { get; private set; }

        /// <summary>Whether confirmed</summary>
        public bool Confirmed { get; private set; }

 /// <summary>File filter extension (lowercase without dot, e.g. "bundle". null means no filtering)</summary>
        public HashSet<string>? FilterExtensions { get; set; }

        /// <summary>Confirm selection event (parameter is the final path:Open=file path,Save=directory/full file name path,Directory=directory path)</summary>
        public event EventHandler<string?>? FileSelected;

        /// <summary>Cancel event</summary>
        public event EventHandler? Cancelled;

        public FileBrowserView()
        {
            InitializeComponent();
            fileList.ItemsSource = _entries;
            fileList.SelectionChanged += FileList_SelectionChanged;
            fileList.DoubleTapped += FileList_DoubleTapped;

            btnUp.Click += (s, e) => GoUp();
            btnHome.Click += (s, e) => GoHome();
            btnRefresh.Click += (s, e) => Refresh();
            btnCancel.Click += (s, e) => { Confirmed = false; Cancelled?.Invoke(this, EventArgs.Empty); };
            btnConfirm.Click += (s, e) => ConfirmSelection();

            fileNameBox.TextChanged += (s, e) => UpdateConfirmState();

            // Default start directory
            _currentDir = GetDefaultStartDir();
        }

        /// <summary>Initialize the file picker</summary>
        /// <param name="startDir">Start directory (null uses default)</param>
        /// <param name="extensions">File extension filter (only effective in Open mode)</param>
        /// <param name="mode">Selection mode</param>
        /// <param name="suggestedFileName">Suggested file name (prefilled into fileNameBox in Save mode)</param>
        public void Initialize(string? startDir = null, HashSet<string>? extensions = null,
            BrowserMode mode = BrowserMode.Open, string? suggestedFileName = null)
        {
            _mode = mode;
            FilterExtensions = extensions;

            if (!string.IsNullOrEmpty(startDir) && Directory.Exists(startDir))
                _currentDir = startDir;
            else
                _currentDir = GetDefaultStartDir();

            // Adjust according to mode UI
            saveNamePanel.IsVisible = (mode == BrowserMode.Save);
            if (mode == BrowserMode.Save)
            {
                fileNameBox.Text = suggestedFileName ?? "";
                btnConfirm.Content = "Save";
                titleText.Text = "Save to...";
            }
            else if (mode == BrowserMode.Directory)
            {
                btnConfirm.Content = "Select this directory";
                titleText.Text = "Select directory";
            }
            else
            {
                btnConfirm.Content = "Select this file";
                titleText.Text = "Select file";
            }

            Refresh();
        }

        private static string GetDefaultStartDir()
        {
            // Android Storage path priority:
 // 1. /storage/emulated/0 (internal storage root)
            // 2. /sdcard
            // 3. AppPaths.CacheDir Up two levels
            // 4. User directory
            var candidates = new[]
            {
                "/storage/emulated/0",
                "/sdcard",
                "/storage",
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                AppPaths.GetCacheDir()
            };
            foreach (var c in candidates)
            {
                if (!string.IsNullOrEmpty(c) && Directory.Exists(c))
                    return c;
            }
            return "/";
        }

        private void Refresh()
        {
            _entries.Clear();
            _selectedFile = null;
            UpdateConfirmState();

            try
            {
                pathBox.Text = _currentDir;

                // List directories first
                var dirs = new List<FileEntry>();
                var files = new List<FileEntry>();

                foreach (var d in Directory.GetDirectories(_currentDir))
                {
                    var name = Path.GetFileName(d);
                    if (string.IsNullOrEmpty(name)) continue;
                    // Skip hidden directories
                    if (name.StartsWith(".")) continue;
                    dirs.Add(new FileEntry { Name = name, FullPath = d, IsDirectory = true });
                }

                // Save and Directory mode also show files (to help the user locate things), but only Open mode filters
                foreach (var f in Directory.GetFiles(_currentDir))
                {
                    var name = Path.GetFileName(f);
                    if (string.IsNullOrEmpty(name)) continue;
                    if (name.StartsWith(".")) continue;

                    // Apply filter (Open mode only)
                    if (_mode == BrowserMode.Open && FilterExtensions != null && FilterExtensions.Count > 0)
                    {
                        var ext = Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
                        if (!FilterExtensions.Contains(ext))
                            continue;
                    }

                    var fi = new FileInfo(f);
                    files.Add(new FileEntry { Name = name, FullPath = f, IsDirectory = false, Size = fi.Length });
                }

                // Directories first, sorted by name
                foreach (var d in dirs.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                    _entries.Add(d);
                foreach (var f in files.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                    _entries.Add(f);
            }
            catch (UnauthorizedAccessException)
            {
                // No permission to access this directory, trying to fall back
                _entries.Add(new FileEntry { Name = " (No permission to access this directory)", FullPath = "", IsDirectory = false });
            }
            catch (Exception ex)
            {
                _entries.Add(new FileEntry { Name = " (Read failed: " + ex.Message + ")", FullPath = "", IsDirectory = false });
            }
        }

        private void GoUp()
        {
            try
            {
                var parent = Directory.GetParent(_currentDir);
                if (parent != null && parent.Exists)
                {
                    _currentDir = parent.FullName;
                    Refresh();
                }
                else if (_currentDir != "/")
                {
                    _currentDir = "/";
                    Refresh();
                }
            }
            catch { }
        }

        private void GoHome()
        {
            _currentDir = GetDefaultStartDir();
            Refresh();
        }

        private void FileList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (fileList.SelectedItem is FileEntry entry)
            {
                if (entry.IsDirectory)
                {
                    _selectedFile = null;
                }
                else if (!string.IsNullOrEmpty(entry.FullPath))
                {
                    _selectedFile = entry;
                    // Save mode: when a file is clicked, fill its name into fileNameBox (convenient for overwriting)
                    if (_mode == BrowserMode.Save)
                        fileNameBox.Text = entry.Name;
                }
            }
            UpdateConfirmState();
        }

        private void FileList_DoubleTapped(object? sender, RoutedEventArgs e)
        {
            if (fileList.SelectedItem is FileEntry entry)
            {
                if (entry.IsDirectory && !string.IsNullOrEmpty(entry.FullPath))
                {
                    _currentDir = entry.FullPath;
                    Refresh();
                }
                else if (!string.IsNullOrEmpty(entry.FullPath))
                {
                    _selectedFile = entry;
                    if (_mode == BrowserMode.Open)
                        ConfirmSelection();
                    else if (_mode == BrowserMode.Save)
                        fileNameBox.Text = entry.Name;
                }
            }
        }

        private void UpdateConfirmState()
        {
            switch (_mode)
            {
                case BrowserMode.Open:
                    btnConfirm.IsEnabled = _selectedFile != null;
                    break;
                case BrowserMode.Save:
                    btnConfirm.IsEnabled = !string.IsNullOrWhiteSpace(fileNameBox.Text);
                    break;
                case BrowserMode.Directory:
                    // Directory mode: the current directory is the selectable item
                    btnConfirm.IsEnabled = true;
                    break;
            }
        }

        private void ConfirmSelection()
        {
            switch (_mode)
            {
                case BrowserMode.Open:
                    if (_selectedFile == null) return;
                    SelectedFilePath = _selectedFile.FullPath;
                    Confirmed = true;
                    FileSelected?.Invoke(this, SelectedFilePath);
                    break;
                case BrowserMode.Save:
                    var name = fileNameBox.Text?.Trim();
                    if (string.IsNullOrEmpty(name)) return;
                    foreach (var c in Path.GetInvalidFileNameChars())
                        name = name.Replace(c, '_');
                    SelectedFilePath = Path.Combine(_currentDir, name);
                    Confirmed = true;
                    FileSelected?.Invoke(this, SelectedFilePath);
                    break;
                case BrowserMode.Directory:
                    SelectedDirectoryPath = _currentDir;
                    Confirmed = true;
                    FileSelected?.Invoke(this, _currentDir);
                    break;
            }
        }
    }
}
