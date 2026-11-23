using System;
using System.Collections.Generic;

namespace UABEAvalonia.Plugins
{
    /// <summary>
    /// Plugin operation mode flags. Adapted from the UavPluginMode enum of UABEANext4,
    /// used to describe the trigger scenarios supported by a plugin option (import / Export / console / create).
    /// Located in the platform-independent UABEA.Core, shared by the main project and the Android side.
    /// </summary>
    [Flags]
    public enum UavPluginMode
    {
        /// <summary>No mode.</summary>
        None = 0,
        /// <summary>Import mode (import from external data into assets).</summary>
        Import = 1,
        /// <summary>Export mode (export from assets to external data).</summary>
        Export = 2,
        /// <summary>Console mode (command line / script call).</summary>
        Console = 4,
        /// <summary>Create mode (create new assets).</summary>
        Create = 8,
        /// <summary>All modes (combination ofImport | Export | Console | Create combined).</summary>
        All = 15
    }

    /// <summary>
    /// <see cref="UavPluginMode"/> extension method collection. GetUniqueFlags is both a static method,
    /// and can also be used as an extension method <c>someMode.GetUniqueFlags()</c> called.
    /// </summary>
    public static class UavPluginModeExtensions
    {
        // All individual flags (excluding the None and All combinations), used for iterating checks.
        private static readonly UavPluginMode[] _uniqueFlags =
        {
            UavPluginMode.Import,
            UavPluginMode.Export,
            UavPluginMode.Console,
            UavPluginMode.Create
        };

        /// <summary>
 /// Returns the individual flags actually set in the current combined value (Import / Export / Console / Create). 
        /// excluding the None and All combined values.
        /// For example <see cref="UavPluginMode.All"/> returns all four individual flags,
        /// <see cref="UavPluginMode.None"/> returns an empty collection.
        /// PluginLoader Use this method to split a combined mode into individual flags and check them one by one.
        /// </summary>
        public static IEnumerable<UavPluginMode> GetUniqueFlags(this UavPluginMode mode)
        {
            foreach (var flag in _uniqueFlags)
            {
                if ((mode & flag) == flag)
                {
                    yield return flag;
                }
            }
        }
    }
}
