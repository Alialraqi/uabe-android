using Avalonia;
using Avalonia.Controls;
using UABEAvalonia.ViewModels;

namespace UABEAvalonia.Forms;

/// <summary>
/// Settings code-behind of the window.
/// Opened by MainViewModel.FileSettingsCommand or the MainWindow menu.
/// On creation, automatically constructs SettingsViewModel as the DataContext.
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        DataContext = new SettingsViewModel();
    }
}
