using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace UABEAvalonia.Plugins
{
    /// <summary>
    /// New plugin loader. Adapted from PluginLoader of UABEANext4.
    /// Scans the plugins directory and uses an independent <see cref="PluginLoadContext"/> to load each dll,
    /// collecting types that implement <see cref="IUavPluginOption"/> or <see cref="IUavPluginPreviewer"/> via reflection.
    ///
 /// Coexists with the old <c>PluginManager</c> (based on <c>UABEAPlugin</c>, used by InfoWindow and others),
    /// without affecting each other. The new PluginLoader targets the PreviewerTool of the Dock UI and the future AssetDocument,
    /// and via <see cref="IUavPluginFunctions"/> injects host services, so plugins do not depend on Avalonia directly.
    /// </summary>
    public class PluginLoader
    {
        // List of loaded plugin options
        private readonly List<IUavPluginOption> _options = new();
        // List of loaded plugin previewers
        private readonly List<IUavPluginPreviewer> _previewers = new();
        // Full paths of successfully loaded dlls, used for de-duplication to avoid loading twice
        private readonly HashSet<string> _loadedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Load a single plugin dll. Returns whether at least one plugin type was loaded successfully.
        /// Load failed (file does not exist / reflection exception, etc.) returns false without throwing.
        /// </summary>
        public bool LoadPlugin(string path)
        {
            try
            {
                string fullPath = Path.GetFullPath(path);

                if (_loadedPaths.Contains(fullPath))
                    return true;

                if (!File.Exists(fullPath))
                    return false;

                // Each dll gets its own PluginLoadContext, isolating dependencies between plugins
                var plugLoadCtx = new PluginLoadContext(fullPath);
                Assembly asm = plugLoadCtx.LoadAssemblyByPath(fullPath);

                bool anyAdded = false;

                foreach (Type type in asm.GetTypes())
                {
                    if (type.IsAbstract || type.IsInterface)
                        continue;

                    if (typeof(IUavPluginOption).IsAssignableFrom(type))
                    {
                        // Do not add the same type twice
                        if (_options.Any(o => o.GetType() == type))
                            continue;

                        if (Activator.CreateInstance(type) is IUavPluginOption plugInst)
                        {
                            _options.Add(plugInst);
                            anyAdded = true;
                        }
                    }
                    else if (typeof(IUavPluginPreviewer).IsAssignableFrom(type))
                    {
                        if (_previewers.Any(p => p.GetType() == type))
                            continue;

                        if (Activator.CreateInstance(type) is IUavPluginPreviewer plugInst)
                        {
                            _previewers.Add(plugInst);
                            anyAdded = true;
                        }
                    }
                }

                if (anyAdded)
                    _loadedPaths.Add(fullPath);

                return anyAdded;
            }
            catch
            {
                // A single plugin failing to load does not affect other plugins or the main program
                return false;
            }
        }

        /// <summary>
        /// Scan all *.dll under the directory and load them. If the directory does not exist, create it (the plugins directory is created automatically on first run).
        /// </summary>
        public void LoadPluginsInDirectory(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                return;
            }

            foreach (string file in Directory.EnumerateFiles(directory, "*.dll"))
            {
                LoadPlugin(file);
            }
        }

        /// <summary>
        /// Return all options that support the specified mode and selection.
        /// Intersect the requested mode with the option's Options, then call for each individual flag
        /// <see cref="IUavPluginOption.SupportsSelection"/> check; each supported flag produces one result.
        /// </summary>
        public List<PluginOptionModePair> GetOptionsThatSupport(
            Workspace workspace, UavPluginMode mode, List<AssetContainer> selection)
        {
            var options = new List<PluginOptionModePair>();
            foreach (var option in _options)
            {
                UavPluginMode bothOpt = mode & option.Options;
                foreach (var flag in bothOpt.GetUniqueFlags())
                {
                    // All is a combined value and should not appear as an individual mode
                    if (flag == UavPluginMode.All)
                        continue;

                    bool supported = option.SupportsSelection(workspace, flag, selection);
                    if (supported)
                        options.Add(new PluginOptionModePair(option, flag));
                }
            }

            return options;
        }

        /// <summary>
        /// Return all previewers that support the current selection and their preview types.
        /// PreviewerToolViewModel Use this method to find the first available previewer.
        /// </summary>
        public List<PluginPreviewerTypePair> GetPreviewersThatSupport(
            Workspace workspace, List<AssetContainer> selection)
        {
            var previewers = new List<PluginPreviewerTypePair>();
            foreach (var previewer in _previewers)
            {
                var previewType = previewer.SupportsPreview(workspace, selection);
                if (previewType != UavPluginPreviewerType.None)
                    previewers.Add(new PluginPreviewerTypePair(previewer, previewType));
            }

            return previewers;
        }
    }
}
