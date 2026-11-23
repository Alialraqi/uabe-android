using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UABEAvalonia
{
    /// <summary>
 /// Configuration manager: responsible for loading/saving config.json, and exposing the global <see cref="Settings"/>. 
    /// P2-C Changed: migrated to System.Text.Json + JsonStringEnumConverter,
    /// and via <see cref="DebounceUtils"/> automatically writes to disk 500ms after a property change,
    /// avoiding synchronous IO on every setter.
    /// Behaves the same as ConfigurationManager in UABEANext.
    /// </summary>
    public static class ConfigurationManager
    {
        public const string CONFIG_FILENAME = "config.json";

        public static ConfigurationValues Settings { get; }
        public static bool IsInitialized { get; private set; }

        private static readonly JsonSerializerOptions OPTIONS = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        // Auto-save debounce: multiple property changes within 500ms trigger only one disk write
        private static readonly Action<int> _saveDebounced = DebounceUtils.Debounce(
            (int _) => SaveConfig(), 500);

        static ConfigurationManager()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CONFIG_FILENAME);
            if (!File.Exists(configPath))
            {
                Settings = new ConfigurationValues();
            }
            else
            {
                try
                {
                    string configText = File.ReadAllText(configPath);
                    Settings = JsonSerializer.Deserialize<ConfigurationValues>(configText, OPTIONS)
                        ?? new ConfigurationValues();
                }
                catch
                {
                    // Fall back to defaults when the config file is corrupted, to avoid blocking startup
                    Settings = new ConfigurationValues();
                }
            }

            // Subscribe to property changes to trigger debounced saving
            Settings.PropertyChanged += OnSettingPropertyChanged;

            IsInitialized = true;
        }

        private static void OnSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!IsInitialized)
                return;

            _saveDebounced(0);
        }

        public static void SaveConfig()
        {
            if (!IsInitialized || Settings == null)
                return;

            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CONFIG_FILENAME);
                string configText = JsonSerializer.Serialize(Settings, OPTIONS);
                File.WriteAllText(configPath, configText);
            }
            catch
            {
                // A disk write failure should not block the program
            }
        }

        /// <summary>
        /// Returns all configuration items with the [ConfigTitle] attribute, for SettingsViewModel to generate the UI by reflection.
        /// Order follows property declaration order.
        /// </summary>
        public static List<ConfigurationItemBase> GetConfigurationItems()
        {
            var items = new List<ConfigurationItemBase>();
            foreach (var prop in typeof(ConfigurationValues).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var titleAttr = prop.GetCustomAttribute<ConfigTitle>();
                if (titleAttr is null)
                    continue;

                ConfigurationItemBase? item = prop.PropertyType switch
                {
                    Type t when t == typeof(bool) => new ConfigurationBooleanItem(prop),
                    Type t when t == typeof(int) => new ConfigurationIntegerItem(prop),
                    Type t when t.IsEnum => new ConfigurationEnumItem(prop),
                    _ => null
                };

                if (item != null)
                    items.Add(item);
            }
            return items;
        }
    }

    /// <summary>
    /// Collection of configuration values. All configurable items are exposed with [ObservableProperty],
    /// and via [ConfigTitle]/[ConfigDesc]/[ConfigRange] attribute description metadata,
    /// for SettingsView to generate the UI by reflection.
    /// </summary>
    public partial class ConfigurationValues : ObservableObject
    {
        // ---- Theme ----
        [ObservableProperty]
        [property: ConfigTitle("Theme type")]
        [property: ConfigDesc("Choose the application theme. Auto mode follows the system theme.")]
        private ConfigurationThemeType _themeType = ConfigurationThemeType.Auto;

        // ---- Decompile ----
        [ObservableProperty]
        [property: ConfigTitle("Prefer Managed over IL2CPP")]
        [property: ConfigDesc("If a Managed folder exists, use it first instead of CPP2IL.")]
        private bool _useManagedOverIl2cpp = false;

        [ObservableProperty]
        [property: ConfigTitle("Use Cpp2Il")]
        [property: ConfigDesc("Option kept for compatibility. When enabled, IL2CPP assemblies are decompiled with CPP2IL first.")]
        private bool _useCpp2Il = true;

        // ---- File name length ----
        [ObservableProperty]
        [property: ConfigTitle("List file name length limit")]
        [property: ConfigDesc("Maximum length of asset names when generating the asset list.")]
        [property: ConfigRange(0, int.MaxValue)]
        private int _listingNameLength = 300;

        [ObservableProperty]
        [property: ConfigTitle("Export file name length limit")]
        [property: ConfigDesc("Maximum length of asset names when exporting assets.")]
        [property: ConfigRange(0, int.MaxValue)]
        private int _exportNameLength = 150;

        // ---- Import/export behavior ----
        [ObservableProperty]
        [property: ConfigTitle("Import/export with plain file names only")]
        [property: ConfigDesc("Export by name only, instead of name + source file + Path ID.\nTwo assets with the same name will conflict, use with caution!")]
        private bool _exportImportJustNames = false;

        [ObservableProperty]
        [property: ConfigTitle("Load container paths")]
        [property: ConfigDesc("Load container paths; may be slower when loading many assets.")]
        private bool _loadContainerPaths = true;

        [ObservableProperty]
        [property: ConfigTitle("Fully crop sprites")]
        [property: ConfigDesc("When off, sprites are placed on a virtual canvas with padding. When on, sprites are fully cropped.")]
        private bool _fullCropSprites = true;

        /// <summary>
        /// Backward compatibility: old code (MainWindow.axaml.cs) switches the theme via UseDarkTheme.
 /// Maps to ThemeType:true -> Dark, false -> Light. Auto state, reading returns
        /// Effective value (Dark or Light, decided by external subscribers).
        /// </summary>
        [JsonIgnore]
        public bool UseDarkTheme
        {
            get => ThemeType == ConfigurationThemeType.Dark;
            set => ThemeType = value ? ConfigurationThemeType.Dark : ConfigurationThemeType.Light;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class ConfigTitle(string title) : Attribute
    {
        public string Title { get; } = title;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class ConfigDesc(string description) : Attribute
    {
        public string Description { get; } = description;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class ConfigRange(int min, int max) : Attribute
    {
        public int Minimum { get; } = min;
        public int Maximum { get; } = max;
    }
}
