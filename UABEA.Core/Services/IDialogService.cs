using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    /// <summary>
    /// Dialog service abstract interface, platform-independent.
    /// The main project implements it with Avalonia, the Android side can provide a touch overlay implementation,
    /// and the test environment can provide a Dummy implementation.
    /// </summary>
    public interface IDialogService
    {
        /// <summary>Show a dialog view that implements IDialogAware.</summary>
        Task<bool> ShowDialog<T>(T context) where T : class;

        /// <summary>Show a message box, returns the index of the button the user chose.</summary>
        Task<int> ShowMessageDialog(
            string title,
            string message,
            string[]? buttons = null);

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
    }

    /// <summary>
    /// Dialog context protocol; a ViewModel implements this interface to take part in dialog interaction.
    /// Title, close logic, etc. are provided by the concrete ViewModel.
    /// </summary>
    public interface IDialogAware
    {
        string Title { get; }
        event EventHandler? RequestClose;
    }
}
