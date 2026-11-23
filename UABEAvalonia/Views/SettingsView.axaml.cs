using Avalonia.Controls;
using UABEAvalonia.ViewModels;

namespace UABEAvalonia.Views;

/// <summary>
/// Settings Code-behind of the panel.
/// Closes the window it lives in when SettingsViewModel.CloseRequested fires.
/// </summary>
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is SettingsViewModel svm)
        {
            svm.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(object? sender, System.EventArgs e)
    {
        // Find the Window it lives in and close it
        if (this.VisualRoot is Window window)
        {
            window.Close();
        }
    }
}
