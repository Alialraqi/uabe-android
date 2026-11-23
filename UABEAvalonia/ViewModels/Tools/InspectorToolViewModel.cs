using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model.Mvvm.Controls;
using System.Collections.ObjectModel;

namespace UABEAvalonia.ViewModels.Tools;

/// <summary>
/// Inspector Tool window. Listens to AssetsSelectedMessage and shows brief info of the currently selected assets.
/// Will be extended into a full property editing panel in later stages.
/// </summary>
public partial class InspectorToolViewModel : Tool
{
    private const string ToolTitle = "Inspector";

    /// <summary>
    /// The currently selected asset collection, bound to an ItemsControl for display.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<AssetContainer> _activeAssets = new();

    public InspectorToolViewModel()
    {
        Id = ToolTitle.Replace(" ", "");
        Title = ToolTitle;

        // Listen to asset selection messages (from asset documents)
        WeakReferenceMessenger.Default.Register<AssetsSelectedMessage>(this, OnAssetsSelected);
        // Listen to the workspace closed message and clear the list
        WeakReferenceMessenger.Default.Register<WorkspaceClosingMessage>(this, OnWorkspaceClosing);
    }

    private void OnAssetsSelected(object recipient, AssetsSelectedMessage message)
    {
        ActiveAssets.Clear();
        foreach (AssetContainer asset in message.Value)
        {
            ActiveAssets.Add(asset);
        }
    }

    private void OnWorkspaceClosing(object recipient, WorkspaceClosingMessage message)
    {
        ActiveAssets.Clear();
    }
}
