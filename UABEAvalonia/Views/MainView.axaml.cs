using Avalonia.Controls;
using UABEAvalonia.ViewModels;

namespace UABEAvalonia.Views;

/// <summary>
/// Dock Code-behind of the main view (UserControl).
/// Only does InitializeComponent; all logic is in MainViewModel.
/// </summary>
public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }
}
