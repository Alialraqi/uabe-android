using AssetsTools.NET.Extra;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model.Mvvm.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UABEAvalonia.Logic.Hierarchy;

namespace UABEAvalonia.ViewModels.Tools;

/// <summary>
/// Hierarchy Tool window. Asynchronously and incrementally loads the GameObject hierarchy tree of the selected assets file.
/// Loading happens on a background thread; a DispatcherTimer flushes pending nodes into RootItems in batches every second,
/// avoiding UI freezes on large files; when switching files the previous load is cancelled through a CancellationTokenSource.
/// When a GameObject is selected, collects all its components and sends them together in an AssetsSelectedMessage.
/// The base class Dock.Model.Mvvm.Controls.Tool already inherits ObservableObject, so [ObservableProperty] can be used directly.
/// </summary>
public partial class HierarchyToolViewModel : Tool
{
    private const string ToolTitle = "Hierarchy";

 /// <summary>Multi-bundle workspace, used to resolve GameObject / component. </summary>
    public Workspace Workspace { get; }

    /// <summary>TreeView Root node collection.</summary>
    [ObservableProperty]
    private ObservableCollection<HierarchyItem> _rootItems = new();

    /// <summary>Currently selected node, two-way bound to TreeView.SelectedItem.</summary>
    [ObservableProperty]
    private HierarchyItem? _selectedItem;

    /// <summary>Whether to sort root nodes alphabetically.</summary>
    [ObservableProperty]
    private bool _sortAlphabetically;

    /// <summary>Search text; when it changes, recursively finds and selects the first node whose name matches.</summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>Whether loading is in progress (used to disable the sort checkbox, etc.).</summary>
    [ObservableProperty]
    private bool _isLoadingNewItems;

    // Cancel the previous load when switching files
    private CancellationTokenSource? _loadCts;
    // Every second, move _pendingRootItems in batches into RootItems
    private DispatcherTimer? _loadTimer;
    // Pending node buffer (written by the background thread, read periodically by the UI thread; access needs a lock)
    private readonly List<HierarchyItem> _pendingRootItems = new();

    /// <summary>Constructor for the designer.</summary>
    public HierarchyToolViewModel()
    {
        Workspace = new Workspace();
        Id = ToolTitle;
        Title = ToolTitle;
    }

    public HierarchyToolViewModel(Workspace workspace)
    {
        Workspace = workspace;
        Id = ToolTitle;
        Title = ToolTitle;

        // Load its GameObject hierarchy when a workspace file is selected
        WeakReferenceMessenger.Default.Register<SelectedWorkspaceItemChangedMessage>(this, OnSelectedWorkspaceItemChanged);
        // Request to jump to the specified GameObject (from asset documents / previewers, etc.)
        WeakReferenceMessenger.Default.Register<RequestSceneViewMessage>(this, OnRequestSceneView);
        // Clear when the workspace closes
        WeakReferenceMessenger.Default.Register<WorkspaceClosingMessage>(this, OnWorkspaceClosing);
    }

    // Load its GameObject hierarchy when a workspace file is selected (only AssetsFile type is handled)
    private void OnSelectedWorkspaceItemChanged(object recipient, SelectedWorkspaceItemChangedMessage message)
    {
        if (message.Value is not WorkspaceItem wsItem)
            return;
        if (wsItem.ItemType != WorkspaceItemType.AssetsFile || wsItem.AssetsFileInstance == null)
            return;
        LoadFile(wsItem.AssetsFileInstance);
    }

    private void OnRequestSceneView(object recipient, RequestSceneViewMessage message)
    {
        SelectItem(message.Value);
    }

    private void OnWorkspaceClosing(object recipient, WorkspaceClosingMessage message)
    {
        _loadCts?.Cancel();
        _loadTimer?.Stop();
        IsLoadingNewItems = false;
        lock (_pendingRootItems)
        {
            _pendingRootItems.Clear();
        }
        RootItems.Clear();
        SelectedItem = null;
    }

    /// <summary>
    /// Asynchronously and incrementally load the GameObject hierarchy tree of the specified assets file.
    /// Cancel the previous load, build the tree on a background thread and write to the buffer in batches,
    /// DispatcherTimer and flush the buffer in batches into RootItems every second.
    /// </summary>
    public async void LoadFile(AssetsFileInstance fileInst)
    {
        // Cancel the previous load and stop its timer
        if (_loadCts != null)
        {
            _loadCts.Cancel();
        }
        _loadTimer?.Stop();

        _loadCts = new CancellationTokenSource();
        CancellationToken ct = _loadCts.Token;

        RootItems.Clear();
        SelectedItem = null;
        lock (_pendingRootItems)
        {
            _pendingRootItems.Clear();
        }
        IsLoadingNewItems = true;

        // Start the timer: every second move pending nodes in batches into RootItems (UI thread)
        _loadTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(1),
            DispatcherPriority.Background,
            (s, e) => AddPendingRootItems());
        _loadTimer.Start();

        // Parse the GameObject tree on a background thread, adding to the buffer in batches
        var loadTask = Task.Run(() =>
        {
            List<HierarchyItem> items = HierarchyItem.CreateRootItems(fileInst, Workspace, ct);
            // Add to the pending buffer in batches, working with DispatcherTimer for incremental refresh
            const int batchSize = 100;
            for (int i = 0; i < items.Count; i += batchSize)
            {
                ct.ThrowIfCancellationRequested();
                int end = Math.Min(i + batchSize, items.Count);
                lock (_pendingRootItems)
                {
                    for (int j = i; j < end; j++)
                        _pendingRootItems.Add(items[j]);
                }
            }
        }, ct);

        try
        {
            await loadTask;
        }
        catch (OperationCanceledException)
        {
            // Cancelled when switching files, ignore
        }

        // Only finish up if this load was not cancelled by a later LoadFile
        if (!ct.IsCancellationRequested)
        {
            AddPendingRootItems();
            _loadTimer?.Stop();
            if (SortAlphabetically)
                SortRootItems();
            IsLoadingNewItems = false;
        }
    }

    // Move pending nodes in the buffer into RootItems in batches (called on the UI thread)
    private void AddPendingRootItems()
    {
        lock (_pendingRootItems)
        {
            if (_pendingRootItems.Count == 0)
                return;
            foreach (var item in _pendingRootItems)
                RootItems.Add(item);
            _pendingRootItems.Clear();
        }
    }

    // When the sort toggle changes, re-sort the already loaded root nodes
    partial void OnSortAlphabeticallyChanged(bool value)
    {
        SortRootItems();
    }

    private void SortRootItems()
    {
        if (!SortAlphabetically || RootItems.Count == 0)
            return;
        var sorted = RootItems.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
        RootItems.Clear();
        foreach (var item in sorted)
            RootItems.Add(item);
    }

    // When the selected node changes, collect the GameObject + all its components and send AssetsSelectedMessage
    partial void OnSelectedItemChanged(HierarchyItem? value)
    {
        if (value?.AssetContainer == null)
            return;
        NotifySelectionChanged(value.AssetContainer);
    }

    /// <summary>
    /// Collect the specified GameObject and all its components (through m_Component.Array),
    /// as List&lt;AssetContainer&gt; send AssetsSelectedMessage, for Inspector / Previewer to display.
    /// </summary>
    public void NotifySelectionChanged(AssetContainer gameObjectCont)
    {
        var allAssets = new List<AssetContainer> { gameObjectCont };

        var goBf = Workspace.GetBaseField(gameObjectCont);
        if (goBf != null)
        {
            var components = goBf["m_Component"]["Array"];
            foreach (var data in components)
            {
                // The last child field of each component entry is PPtr<Component>
                if (data.Children.Count == 0)
                    continue;
                var component = data[data.Children.Count - 1];
                var componentCont = Workspace.GetAssetContainer(gameObjectCont.FileInstance, component);
                if (componentCont != null)
                    allAssets.Add(componentCont);
            }
        }

        WeakReferenceMessenger.Default.Send(new AssetsSelectedMessage(allAssets));
    }

    // Request to jump to the specified GameObject: recursively search in RootItems, then expand and select
    private void SelectItem(AssetContainer asset)
    {
        foreach (var root in RootItems)
        {
            if (SearchForItemAndSelectRecursive(asset, root))
            {
                root.IsExpanded = true;
                break;
            }
        }
    }

    private bool SearchForItemAndSelectRecursive(AssetContainer asset, HierarchyItem item)
    {
        if (item.AssetContainer != null &&
            item.AssetContainer.FileInstance == asset.FileInstance &&
            item.AssetContainer.PathId == asset.PathId)
        {
            SelectedItem = item;
            return true;
        }

        foreach (var child in item.Children)
        {
            if (SearchForItemAndSelectRecursive(asset, child))
            {
                item.IsExpanded = true;
                return true;
            }
        }
        return false;
    }

    // When the search text changes, recursively find the first node whose name matches, then expand and select it
    partial void OnSearchTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        SearchByText(value);
    }

    private void SearchByText(string searchText)
    {
        foreach (var root in RootItems)
        {
            if (SearchByTextRecursive(searchText, root))
            {
                root.IsExpanded = true;
                break;
            }
        }
    }

    private bool SearchByTextRecursive(string searchText, HierarchyItem item)
    {
        if (!string.IsNullOrEmpty(item.Name) &&
            item.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            SelectedItem = item;
            return true;
        }
        foreach (var child in item.Children)
        {
            if (SearchByTextRecursive(searchText, child))
            {
                item.IsExpanded = true;
                return true;
            }
        }
        return false;
    }
}
