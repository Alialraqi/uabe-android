using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace UABEAvalonia.Plugins
{
    /// <summary>
    /// Plugin isolated load context. Adapted from PluginLoadContext of UABEANext4.
 /// Each plugin dll has its own independent <see cref="AssemblyLoadContext"/>, together with
    /// <see cref="AssemblyDependencyResolver"/> resolves plugin dependencies (including unmanaged dlls),
    /// avoiding dependency conflicts between plugins. The isolation granularity is a single dll.
    ///
 /// System.Runtime.Loader (AssemblyLoadContext / AssemblyDependencyResolver)
    /// Built into .NET 8, no extra PackageReference needed.
    /// </summary>
    internal class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string pluginPath)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        /// <summary>Load by assembly name (goes through the resolver to resolve the plugin deps.json).</summary>
        public Assembly LoadAssemblyByName(string name)
        {
            return LoadFromAssemblyName(new AssemblyName(name));
        }

        /// <summary>
 /// Load by dll path: build from the file name <see cref="AssemblyName"/>, 
        /// and then hand over to <see cref="Load"/> resolves to that plugin dll through the resolver.
        /// </summary>
        public Assembly LoadAssemblyByPath(string path)
        {
            return LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(path)));
        }

        /// <summary>
        /// Resolve plugin dependency assemblies: prefer resolving with the plugin's deps.json,
        /// and fall back to the default context if null is returned (sharing assemblies already loaded by the host).
        /// </summary>
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        /// <summary>Resolve unmanaged dlls that the plugin depends on (such as native libraries).</summary>
        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }
}
