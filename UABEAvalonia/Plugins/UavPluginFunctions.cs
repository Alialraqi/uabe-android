using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UABEAvalonia.Plugins
{
    /// <summary>
    /// <see cref="IUavPluginFunctions"/> implementation. Adapted from UABEANext4's UavPluginFunctions,
 /// but via <see cref="IServiceProvider"/> get <see cref="IDialogService"/>, 
    /// and use <see cref="Lazy{T}"/> lazy initialization: the main window is only available after App startup,
    /// so the plugin should not resolve the dialog service immediately on construction.
    ///
    /// Note: this file is compiled by both desktop and Android (UABEA.Android includes it via source link).
    /// - The desktop provides a parameterless constructor that resolves services from <c>App.Services</c> for plugin options to call by reflection.
    /// - Android The side has no DI configured yet; the parameterless constructor is <c>#if !ANDROID</c> hidden,
    ///   and must be injected explicitly through the constructor with <see cref="IServiceProvider"/> .
 /// Service resolution uses <see cref="IServiceProvider.GetService(Type)"/> (BCL instance method),
    /// avoiding dependency on the Microsoft.Extensions.DependencyInjection extension package.
    /// </summary>
    public class UavPluginFunctions : IUavPluginFunctions
    {
        private readonly Lazy<IDialogService> _dialogService;

#if !ANDROID
        /// <summary>
        /// Parameterless constructor: from the desktop <c>App.Services</c> resolve services.
 /// For plugin option execution (<see cref="IUavPluginOption.Execute"/>)reflection calls,
        /// corresponding to the reference implementation <c>new UavPluginFunctions()</c> usage.
        /// </summary>
        public UavPluginFunctions()
            : this(App.Services)
        {
        }
#endif

        /// <summary>
        /// By specifying <see cref="IServiceProvider"/> construction, easy for testing / Android side injection.
        /// </summary>
        public UavPluginFunctions(IServiceProvider services)
        {
            _dialogService = new Lazy<IDialogService>(() =>
                (IDialogService?)services.GetService(typeof(IDialogService))
                ?? throw new InvalidOperationException(
                    "IDialogService is not registered in the service container; UavPluginFunctions cannot be initialized."));
        }

        public Task<List<string>> ShowOpenFileDialog(
            string title = "",
            string? directory = null,
            List<string>? extensions = null,
            bool multiSelect = false)
        {
            return _dialogService.Value.ShowOpenFileDialog(title, directory, extensions, multiSelect);
        }

        public Task<string> ShowSaveFileDialog(
            string title = "",
            string? directory = null,
            string? defaultFileName = null,
            List<string>? extensions = null)
        {
            return _dialogService.Value.ShowSaveFileDialog(title, directory, defaultFileName, extensions);
        }

        public Task<string> ShowOpenFolderDialog(string title = "", string? directory = null)
        {
            return _dialogService.Value.ShowOpenFolderDialog(title, directory);
        }

        public Task<int> ShowMessageDialog(string title, string message, string[]? buttons = null)
        {
            return _dialogService.Value.ShowMessageDialog(title, message, buttons);
        }
    }
}
