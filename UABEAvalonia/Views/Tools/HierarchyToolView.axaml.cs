using Avalonia.Controls;
using Avalonia.Input;
using UABEAvalonia.Logic.Hierarchy;
using UABEAvalonia.ViewModels.Tools;

namespace UABEAvalonia.Views.Tools;

/// <summary>
/// Hierarchy Tool window view. Double-click a node to toggle expand/collapse.
/// </summary>
public partial class HierarchyToolView : UserControl
{
    public HierarchyToolView()
    {
        InitializeComponent();
    }

    // Double-click toggles the expand/collapse state of the selected node
    private void HierarchyTreeView_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is HierarchyToolViewModel vm && vm.SelectedItem is HierarchyItem item)
        {
            item.IsExpanded = !item.IsExpanded;
        }
    }
}
