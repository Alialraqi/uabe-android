using Avalonia;
using Avalonia.Controls;
using UABEAvalonia.ViewModels;

namespace UABEAvalonia;

/// <summary>
/// Dock Code-behind of the main window.
/// With the existing Forms/MainWindow.axaml coexists, as the entry for the new multi-document UI.
/// App.axaml.cs chooses at startup whether to create MainWindow or DockMainWindow.
/// </summary>
public partial class DockMainWindow : Window
{
    public DockMainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
    }

    /// <summary>
    /// Inject MainViewModel. Called by App.axaml.cs after creating the window.
    /// </summary>
    public void SetViewModel(MainViewModel viewModel)
    {
        DataContext = viewModel;
    }
}
