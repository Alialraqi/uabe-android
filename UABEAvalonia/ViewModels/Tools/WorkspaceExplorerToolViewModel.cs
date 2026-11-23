using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model.Mvvm.Controls;
using System.Collections.ObjectModel;

namespace UABEAvalonia.ViewModels.Tools;

/// <summary>
/// Workspace browser tool window. Shows the tree structure of Workspace.RootItems,
/// after selecting a node, notifies MainViewModel through SelectedWorkspaceItemChangedMessage to open a document.
/// The base class Dock.Model.Mvvm.Controls.Tool already inherits ObservableObject, so [ObservableProperty] can be used directly.
/// </summary>
public partial class WorkspaceExplorerToolViewModel : Tool
{
    private const string ToolTitle = "Workspace Explorer";

    /// <summary>
    /// Root-level workspace item collection bound to the TreeView.
    /// Directly references Workspace.RootItems, avoiding data duplication.
    /// </summary>
    public ObservableCollection<WorkspaceItem> RootItems { get; }

    /// <summary>
    /// Currently selected workspace item. After setting, notifies the main view model through the Messenger.
    /// </summary>
    [ObservableProperty]
    private WorkspaceItem? _selectedItem;

    public WorkspaceExplorerToolViewModel(Workspace workspace)
    {
        RootItems = new ObservableCollection<WorkspaceItem>(workspace.RootItems);

        Id = ToolTitle.Replace(" ", "");
        Title = ToolTitle;

        // Listen to the workspace item change event and sync the RootItems collection automatically
        workspace.ItemAdded += OnWorkspaceItemAdded;
        workspace.ItemRemoved += OnWorkspaceItemRemoved;
    }

    partial void OnSelectedItemChanged(WorkspaceItem? value)
    {
        // When the selected item changes, notify MainViewModel to open the corresponding document
        WeakReferenceMessenger.Default.Send(new SelectedWorkspaceItemChangedMessage(value));
    }

    private void OnWorkspaceItemAdded(WorkspaceItem item)
    {
 // Only root-level items (Parent == null)need to be added to the list
        if (item.Parent == null)
        {
            RootItems.Add(item);
        }
    }

    private void OnWorkspaceItemRemoved(WorkspaceItem item)
    {
        if (item.Parent == null)
        {
            RootItems.Remove(item);
        }
    }
}
