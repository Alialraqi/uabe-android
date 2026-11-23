using AssetsTools.NET;
using AssetsTools.NET.Extra;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace UABEAvalonia.Logic.Hierarchy;

/// <summary>
/// Hierarchy Tree node corresponding to a Unity GameObject.
/// Parent-child relationships are derived from the associated Transform asset; AssetContainer points to the GameObject itself.
/// Inherits ObservableObject to support IsExpanded / IsSelected two-way binding.
/// </summary>
public partial class HierarchyItem : ObservableObject
{
    /// <summary>Node display name (m_Name of the GameObject).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Associated GameObject asset container; the root wrapper node may be null.</summary>
    public AssetContainer? AssetContainer { get; set; }

    /// <summary>Child node collection, bound to ItemTemplate.ItemsSource of the TreeView.</summary>
    public ObservableCollection<HierarchyItem> Children { get; } = new();

    /// <summary>Parent node; null for the root node.</summary>
    public HierarchyItem? Parent { get; set; }

    /// <summary>Whether expanded, two-way bound to TreeViewItem.IsExpanded.</summary>
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>Whether selected, two-way bound to TreeViewItem.IsSelected.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Build the GameObject hierarchy tree for the specified assets file.
    /// Called on a background thread, via <paramref name="ct"/> supports cancellation when switching files.
    /// Parsing logic follows GameObjectViewWindow: traverse Transform/RectTransform assets,
    /// determine root nodes via m_Father, associate the GameObject via the m_GameObject PPtr.
    /// </summary>
    public static List<HierarchyItem> CreateRootItems(
        AssetsFileInstance fileInst, Workspace workspace, CancellationToken ct)
    {
 // First pass: collect all Transform/RectTransform, build PathId -> node info
        var transformInfos = new Dictionary<long, TransformInfo>();

        foreach (AssetFileInfo info in fileInst.file.AssetInfos)
        {
            ct.ThrowIfCancellationRequested();

            // Only handle Transform(4) and RectTransform(21)
            bool isTransform = info.TypeId == (uint)AssetClassID.Transform
                            || info.TypeId == (uint)AssetClassID.RectTransform;
            if (!isTransform)
                continue;

            var cont = new AssetContainer(info, fileInst);
            var tfmBf = workspace.GetBaseField(cont);
            if (tfmBf == null)
                continue;

            long pathId = info.PathId;
            // m_Father is a PPtr,m_PathID == 0 means root Transform
            long fatherPathId = tfmBf["m_Father"]["m_PathID"].AsLong;

            // Resolve the associated GameObject container via the m_GameObject PPtr
            AssetContainer? goCont = workspace.GetAssetContainer(fileInst, tfmBf["m_GameObject"]);
            string name = "[missing gameobject]";
            if (goCont != null)
            {
                var goBf = workspace.GetBaseField(goCont);
                if (goBf != null)
                    name = goBf["m_Name"].AsString;
            }

            transformInfos[pathId] = new TransformInfo
            {
                PathId = pathId,
                FatherPathId = fatherPathId,
                GameObjectContainer = goCont,
                Name = name
            };
        }

        // Second pass: create HierarchyItem and build parent-child relations by m_Father
        var pathIdToItem = new Dictionary<long, HierarchyItem>();
        foreach (var kv in transformInfos)
        {
            ct.ThrowIfCancellationRequested();
            var inf = kv.Value;
            pathIdToItem[kv.Key] = new HierarchyItem
            {
                Name = inf.Name,
                AssetContainer = inf.GameObjectContainer
            };
        }

        var rootItems = new List<HierarchyItem>();
        foreach (var kv in transformInfos)
        {
            ct.ThrowIfCancellationRequested();
            var inf = kv.Value;
            var item = pathIdToItem[kv.Key];

            if (inf.FatherPathId != 0 &&
                pathIdToItem.TryGetValue(inf.FatherPathId, out HierarchyItem? parentItem))
            {
                // Attach under the parent Transform node
                parentItem.Children.Add(item);
                item.Parent = parentItem;
            }
            else
            {
                // m_Father.m_PathID == 0 is a root; a parent Transform not in this file is also treated as a root
                rootItems.Add(item);
            }
        }

        return rootItems;
    }

    // Transform Intermediate parsing structure of the asset
    private class TransformInfo
    {
        public long PathId { get; set; }
        public long FatherPathId { get; set; }
        public AssetContainer? GameObjectContainer { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
