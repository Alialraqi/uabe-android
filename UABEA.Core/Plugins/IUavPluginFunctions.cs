using System.Collections.Generic;
using System.Threading.Tasks;

namespace UABEAvalonia.Plugins
{
    /// <summary>
 /// Service interface the host provides to plugins. Platform-independent; method signatures reused from <see cref="IDialogService"/>, 
    /// Does not reference any Avalonia types, so plugins can be shared between desktop and Android.
    /// The desktop implementation can be provided by <see cref="IDialogService"/> a direct proxy.
    /// </summary>
    public interface IUavPluginFunctions
    {
        /// <summary>Open the file selection dialog, returns the list of selected file paths (empty means cancelled).</summary>
        Task<List<string>> ShowOpenFileDialog(
            string title = "",
            string? directory = null,
            List<string>? extensions = null,
            bool multiSelect = false);

        /// <summary>Save file dialog, returns the target path (empty string means cancelled).</summary>
        Task<string> ShowSaveFileDialog(
            string title = "",
            string? directory = null,
            string? defaultFileName = null,
            List<string>? extensions = null);

        /// <summary>Select folder dialog, returns the selected folder path (empty string means cancelled).</summary>
        Task<string> ShowOpenFolderDialog(
            string title = "",
            string? directory = null);

        /// <summary>Show a message box, returns the index of the button the user chose.</summary>
        Task<int> ShowMessageDialog(
            string title,
            string message,
            string[]? buttons = null);
    }
}
