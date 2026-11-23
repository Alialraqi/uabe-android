using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Collections.Generic;

namespace UABEA.Android
{
    /// <summary>
    /// Recursively traverse AssetTypeValueField, collecting all string fields.
    /// Used for: TextAsset's m_Script / MonoBehaviour string fields inside.
    /// Also records field paths, to locate them when writing back.
    /// </summary>
    public class StringExtractor
    {
        /// <summary>Collected string entries</summary>
        public List<ExtractedString> Strings { get; } = new();

        /// <summary>
        /// Recursively collect all string fields starting from baseField.
        /// </summary>
        /// <param name="baseField">Asset root field (obtained by am.GetBaseField)</param>
        /// <param name="rootPath">Path prefix (usually an empty string)</param>
        public void Extract(AssetTypeValueField baseField, string rootPath = "")
        {
            if (baseField == null) return;
            ExtractInternal(baseField, rootPath);
        }

        private void ExtractInternal(AssetTypeValueField field, string path)
        {
            if (field == null) return;

            // String field: collect directly
            if (field.Value != null && field.Value.ValueType == AssetValueType.String)
            {
                string value = field.AsString ?? "";
                // Skip empty strings (translation is meaningless)
                if (!string.IsNullOrEmpty(value))
                {
                    Strings.Add(new ExtractedString
                    {
                        Path = path,
                        Original = value,
                        FieldRef = field
                    });
                }
                return; // String field has no child fields
            }

            // ManagedReferencesRegistry(SerializeReference):Expand references.data recursively
            if (field.Value != null && field.Value.ValueType == AssetValueType.ManagedReferencesRegistry)
            {
                var registry = field.AsManagedReferencesRegistry;
                if (registry?.references != null)
                {
                    for (int i = 0; i < registry.references.Count; i++)
                    {
                        ExtractInternal(registry.references[i].data, $"{path}[{i}].");
                    }
                }
                return;
            }

            // Array: iterate over each element
            var template = field.TemplateField;
            bool isArray = template != null && template.IsArray;
            var children = field.Children;
            if (children == null || children.Count == 0) return;

            if (isArray)
            {
                for (int i = 0; i < children.Count; i++)
                {
                    ExtractInternal(children[i], $"{path}[{i}]");
                }
            }
            else
            {
                // Normal structure: each child field uses the field name as its path
                foreach (var child in children)
                {
                    if (child == null) continue;
                    string childName = child.TemplateField?.Name ?? "?";
                    string childPath = string.IsNullOrEmpty(path) ? childName : $"{path}.{childName}";
                    ExtractInternal(child, childPath);
                }
            }
        }

        /// <summary>
        /// Specifically extracts the m_Script field of TextAsset (large text).
        /// TextAsset m_Script is TypelessData (ByteArray), not a normal string.
        /// </summary>
        public static ExtractedString? ExtractTextAsset(AssetTypeValueField baseField)
        {
            if (baseField == null) return null;
            var scriptField = baseField["m_Script"];
            if (scriptField == null) return null;

            byte[]? bytes = scriptField.AsByteArray;
            if (bytes == null || bytes.Length == 0) return null;

            string text;
            try
            {
                text = System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                text = System.Text.Encoding.Latin1.GetString(bytes);
            }

            return new ExtractedString
            {
                Path = "m_Script",
                Original = text,
                FieldRef = scriptField,
                IsTextAssetScript = true,
                OriginalBytes = bytes
            };
        }
    }

    /// <summary>
    /// A single extracted string entry.
 /// FieldRef Used for write-back: after translation, directly field.AsString = newValue or field.AsByteArray = bytes. 
    /// </summary>
    public class ExtractedString
    {
        /// <summary>Field path (such as m_Name / m_LocalizedString[3].text)</summary>
        public string Path { get; set; } = "";

        /// <summary>Original string</summary>
        public string Original { get; set; } = "";

        /// <summary>Translated string (assigned after user edit;null means unmodified)</summary>
        public string? Translated { get; set; }

        /// <summary>Field reference (used for write-back)</summary>
        public AssetTypeValueField? FieldRef { get; set; }

        /// <summary>Whether this is the m_Script of a TextAsset (needs to be written back as bytes)</summary>
        public bool IsTextAssetScript { get; set; }

        /// <summary>Original bytes (TextAsset only)</summary>
        public byte[]? OriginalBytes { get; set; }

        /// <summary>Whether modified</summary>
        public bool IsModified => Translated != null && Translated != Original;
    }
}
