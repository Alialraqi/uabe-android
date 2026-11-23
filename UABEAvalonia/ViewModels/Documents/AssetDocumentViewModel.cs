using Dock.Model.Mvvm.Controls;

namespace UABEAvalonia.ViewModels.Documents;

/// <summary>
/// Asset document, corresponding to an opened WorkspaceItem (assets / bundle). 
/// P1 stage only holds the WorkspaceItem reference and provides a placeholder Title,
/// the real asset list display (DataGrid) will be added in a later stage.
/// The base class Dock.Model.Mvvm.Controls.Document already inherits ObservableObject.
/// </summary>
public partial class AssetDocumentViewModel : Document
{
    /// <summary>
 /// The workspace item this document corresponds to (root-level assets / bundle). 
    /// later stages will use it to read the AssetContainer list.
    /// </summary>
    public WorkspaceItem WorkspaceItem { get; }

    public AssetDocumentViewModel(WorkspaceItem wsItem)
    {
        WorkspaceItem = wsItem;

        // Id Must stay unique, for DockableLocator lookups;
        // different bundles may contain sub-files with the same name, so Id carries the Parent.Name prefix to avoid conflicts.
        string parentId = wsItem.Parent?.Name ?? "root";
        Id = $"{parentId}/{wsItem.Name}";
        Title = wsItem.ToString();
    }
}
