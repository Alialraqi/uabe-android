using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UABEA.Android
{
    /// <summary>
    /// Translation entry data model (decoupled from AssetTypeValueField, serializable to TXT).
    /// Each entry corresponds to one translatable piece of text.
    /// </summary>
    public class TextEntry
    {
        /// <summary>Relative path of the file it belongs to (relative to the scan root directory)</summary>
        public string FilePath { get; set; } = "";

        /// <summary>Asset PathID (used to locate it when writing back)</summary>
        public long PathId { get; set; }

        /// <summary>Field path (such as m_Name / m_Script / m_LocalizedString[3].text)</summary>
        public string FieldPath { get; set; } = "";

        /// <summary>Original text</summary>
        public string Original { get; set; } = "";

        /// <summary>Translated text(null/Empty means untranslated)</summary>
        public string Translated { get; set; } = "";

        /// <summary>Whether modified</summary>
        public bool IsModified => !string.IsNullOrEmpty(Translated) && Translated != Original;

        /// <summary>Unified display name</summary>
        public string DisplayLabel
        {
            get
            {
                string fileName = Path.GetFileName(FilePath);
                string preview = Original.Length > 30 ? Original.Substring(0, 30) + "..." : Original;
                return $"[{fileName}#{PathId}] {FieldPath}\n{preview}";
            }
        }
    }

    /// <summary>
    /// TXT Import/export format:
    ///
    /// === FILE::PATHID::FIELD ===
    /// Original line1
    /// Original line2
    /// ---
    /// Translation line1
    /// Translation line2
    /// ===
    ///
    /// Entries are separated by blank lines.
    /// Within strings, the ===/--- will be escaped as \=== \--- to avoid conflicts.
    /// </summary>
    public static class TextEntryTxtIo
    {
        private const string EntryHeader = "=== FILE::";
        private const string EntrySeparator = "---";
        private const string EntryFooter = "===";

        /// <summary>Export entries to a TXT string</summary>
        public static string Export(List<TextEntry> entries)
        {
            var sb = new StringBuilder();
            foreach (var e in entries)
            {
                sb.Append(EntryHeader);
                sb.Append(e.FilePath).Append("::").Append(e.PathId).Append("::").Append(e.FieldPath);
                sb.AppendLine(" ===");
                sb.AppendLine(Escape(e.Original));
                sb.AppendLine(EntrySeparator);
                sb.AppendLine(Escape(e.Translated));
                sb.AppendLine(EntryFooter);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>Export and write to a file</summary>
        public static void ExportToFile(List<TextEntry> entries, string path)
        {
            File.WriteAllText(path, Export(entries), Encoding.UTF8);
        }

        /// <summary>Import from TXT text, returns PathId+FieldPath → a map of translations</summary>
        public static Dictionary<string, string> Import(string txt)
        {
            var result = new Dictionary<string, string>();
            var lines = txt.Replace("\r\n", "\n").Split('\n');

            int i = 0;
            while (i < lines.Length)
            {
                string line = lines[i];
                if (line.StartsWith(EntryHeader) && line.TrimEnd().EndsWith(" ==="))
                {
                    // Parse header:=== FILE::path::pathid::field ===
                    string mid = line.Substring(EntryHeader.Length);
                    int endIdx = mid.LastIndexOf(" ===");
                    if (endIdx < 0) { i++; continue; }
                    string header = mid.Substring(0, endIdx);

                    // Find the last two :: separators (field path may contain ::, simplified: search from right to left)
                    // header = filepath::pathid::field
                    // filepath may contain ::, so find the last :: first, then find the last one in the left part ::
                    int lastSep = header.LastIndexOf("::");
                    if (lastSep < 0) { i++; continue; }
                    string fieldPath = header.Substring(lastSep + 2);

                    string beforeField = header.Substring(0, lastSep);
                    int secondLastSep = beforeField.LastIndexOf("::");
                    if (secondLastSep < 0) { i++; continue; }
                    string pathIdStr = beforeField.Substring(secondLastSep + 2);
                    string filePath = beforeField.Substring(0, secondLastSep);

                    if (!long.TryParse(pathIdStr, out long pathId)) { i++; continue; }

                    // Collect the original until ---
                    i++;
                    var origLines = new List<string>();
                    while (i < lines.Length && lines[i] != EntrySeparator)
                    {
                        origLines.Add(Unescape(lines[i]));
                        i++;
                    }
                    i++; // Skip ---

                    // Collect the translation until ===
                    var transLines = new List<string>();
                    while (i < lines.Length && lines[i] != EntryFooter)
                    {
                        transLines.Add(Unescape(lines[i]));
                        i++;
                    }

                    string original = string.Join("\n", origLines);
                    string translated = string.Join("\n", transLines);

                    string key = $"{filePath}::{pathId}::{fieldPath}";
                    result[key] = translated;
                }
                i++;
            }
            return result;
        }

        /// <summary>Import from file</summary>
        public static Dictionary<string, string> ImportFromFile(string path)
        {
            return Import(File.ReadAllText(path, Encoding.UTF8));
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s
                .Replace("\\", "\\\\")
                .Replace("===", "\\=== ")
                .Replace("---", "\\--- ");
        }

        private static string Unescape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s
                .Replace("\\--- ", "---")
                .Replace("\\=== ", "===")
                .Replace("\\\\", "\\");
        }
    }
}
