using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace UABEAvalonia.ViewModels;

/// <summary>
/// Settings panel ViewModel: via <see cref="ConfigurationManager.GetConfigurationItems"/>
/// reflection gets all configuration items with [ConfigTitle], binds them to SettingsView for dynamic rendering.
/// The Value of a configuration item reads and writes ConfigurationManager.Settings directly through reflection,
/// and writes automatically trigger the 500ms debounced save of ConfigurationManager.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    /// <summary>All configurable items (boolean/integer/enum)</summary>
    public ObservableCollection<ConfigurationItemBase> Items { get; }

    public SettingsViewModel()
    {
        Items = new ObservableCollection<ConfigurationItemBase>(ConfigurationManager.GetConfigurationItems());
    }

    /// <summary>
    /// Command to close the settings window. Setting changes are already written to Settings in real time and saved with debounce,
    /// so here it only triggers closing the window (handled by the view through CloseBehavior).
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        // Explicitly trigger one save, to prevent the user from closing within 500ms and losing the last change
        ConfigurationManager.SaveConfig();
        CloseRequested?.Invoke(this, System.EventArgs.Empty);
    }

    /// <summary>Notify the view to close the window</summary>
    public event System.EventHandler? CloseRequested;
}
