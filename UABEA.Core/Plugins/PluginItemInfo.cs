namespace UABEAvalonia.Plugins
{
    /// <summary>
    /// Wraps plugin info: plugin name + option + mode. Used for menu / list display.
    /// Adapted from PluginItemInfo of UABEANext4, but without the dependency on AssetDocumentViewModel
 /// (execution logic is handled by the caller), staying platform-independent.
    /// </summary>
    public class PluginItemInfo
    {
        /// <summary>Plugin name (from the assembly the option instance type lives in / namespace, set by the caller).</summary>
        public string PluginName { get; }

        /// <summary>Plugin option instance, may be null (only when used to show groups).</summary>
        public IUavPluginOption? Option { get; }

        /// <summary>The operation mode this entry corresponds to.</summary>
        public UavPluginMode Mode { get; }

        public PluginItemInfo(string pluginName, IUavPluginOption? option, UavPluginMode mode)
        {
            PluginName = pluginName;
            Option = option;
            Mode = mode;
        }

        /// <summary>Display name, menu / used for list binding. Prefers the option name, falls back to the plugin name.</summary>
        public string DisplayName => Option?.Name ?? PluginName;

        public override string ToString() => DisplayName;
    }
}
