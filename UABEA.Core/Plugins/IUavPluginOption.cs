using System.Collections.Generic;
using System.Threading.Tasks;

namespace UABEAvalonia.Plugins
{
    /// <summary>
    /// New plugin operation interface. Compared with the old <c>UABEAPluginOption</c> exists in parallel,
    /// Based on IUavPluginOption of UABEANext4, but made platform-independent:
    ///   - Selection uses <see cref="List{AssetContainer}"/> rather than a single asset
    ///   - Host services via <see cref="IUavPluginFunctions"/> injection, avoiding a direct dependency on Avalonia Window
    /// After a plugin implements this interface, it is <see cref="PluginLoader"/> loaded by reflection and registered.
    /// </summary>
    public interface IUavPluginOption
    {
        /// <summary>Option name (menu / used for list display).</summary>
        string Name { get; }

        /// <summary>Option description (used for tooltips).</summary>
        string Description { get; }

        /// <summary>Combination of mode flags supported by this option.</summary>
        UavPluginMode Options { get; }

        /// <summary>
        /// Determines whether the current selection can be handled by this option in the specified mode.
        /// </summary>
        bool SupportsSelection(Workspace workspace, UavPluginMode mode, List<AssetContainer> selection);

        /// <summary>
        /// Execute the plugin operation. Returns true on success (may trigger a later refresh), false on failure or cancel.
        /// </summary>
        Task<bool> Execute(Workspace workspace, IUavPluginFunctions funcs, UavPluginMode mode, List<AssetContainer> selection);
    }
}
