using CommunityToolkit.Mvvm.Input;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using System;
using System.Collections.Generic;
using UABEAvalonia.Plugins;
using UABEAvalonia.ViewModels.Documents;
using UABEAvalonia.ViewModels.Tools;

namespace UABEAvalonia.ViewModels;

/// <summary>
/// Dock Layout factory. Follows the design of UABEANext4 MainDockFactory:
///   - CreateLayout() Build a three-column layout tree (left WorkspaceExplorer + middle DocumentDock + right Inspector)
///   - InitLayout() Register ContextLocator / DockableLocator / HostWindowLocator
/// Note: this project uses Dock.Avalonia 11.0.0.7 (compatible with Avalonia 11.0.1),
/// the factory base class is Dock.Model.Mvvm.Factory, and IRootDock is in Dock.Model.Controls.
/// </summary>
public class MainDockFactory : Factory
{
    /// <summary>
    /// Main ProportionalDock, can be used to add dockables dynamically when needed externally.
    /// </summary>
    public ProportionalDock? MainPane { get; private set; }

    private IRootDock? _rootDock;
    private IDocumentDock? _fileDocumentDock;
    private WorkspaceExplorerToolViewModel? _workspaceExplorerTool;
    private HierarchyToolViewModel? _hierarchyTool;
    private InspectorToolViewModel? _inspectorTool;
    private PreviewerToolViewModel? _previewerTool;

    private readonly Workspace _workspace;
    private readonly PluginLoader? _pluginLoader;

    public MainDockFactory(Workspace workspace)
        : this(workspace, null)
    {
    }

    /// <summary>
    /// Inject the new plugin loader, forwarded by this factory to PreviewerToolViewModel.
    /// When null, PreviewerTool falls back to the built-in previewer list.
    /// </summary>
    public MainDockFactory(Workspace workspace, PluginLoader? pluginLoader)
    {
        _workspace = workspace;
        _pluginLoader = pluginLoader;
    }

    public override IRootDock CreateLayout()
    {
        // Create tool windows ViewModel
        _workspaceExplorerTool = new WorkspaceExplorerToolViewModel(_workspace);
        _hierarchyTool = new HierarchyToolViewModel(_workspace);
        _inspectorTool = new InspectorToolViewModel();
        _previewerTool = new PreviewerToolViewModel(_workspace, _pluginLoader);

        // DocumentDock Initial content is a placeholder document that cannot be closed
        var blankDocument = new BlankDocumentViewModel();
        _fileDocumentDock = new DocumentDock
        {
            Id = "Files",
            Title = "Files",
            ActiveDockable = blankDocument,
            VisibleDockables = CreateList<IDockable>(blankDocument),
            CanCreateDocument = true,
            CreateDocument = new RelayCommand(AddNewBlankDocument),
            IsCollapsable = false,
            Proportion = double.NaN
        };

        // Left tool window (Workspace Explorer)
        var explorerDock = new ToolDock
        {
            Id = "ExplorerDock",
            ActiveDockable = _workspaceExplorerTool,
            VisibleDockables = CreateList<IDockable>(_workspaceExplorerTool),
            Alignment = Alignment.Left,
            GripMode = GripMode.Visible,
            Proportion = 0.5
        };

        // Left tool window (Hierarchy, Unity scene hierarchy browser)
        var hierarchyDock = new ToolDock
        {
            Id = "HierarchyDock",
            ActiveDockable = _hierarchyTool,
            VisibleDockables = CreateList<IDockable>(_hierarchyTool),
            Alignment = Alignment.Left,
            GripMode = GripMode.Visible,
            Proportion = 0.5
        };

        // Left tool area: Workspace Explorer on top, Hierarchy below, stacked vertically
        var leftToolsPane = new ProportionalDock
        {
            Id = "LeftToolsPane",
            Orientation = Orientation.Vertical,
            IsCollapsable = false,
            VisibleDockables = CreateList<IDockable>(
                explorerDock,
                new ProportionalDockSplitter(),
                hierarchyDock)
        };

        // Right tool window (Inspector)
        var inspectorDock = new ToolDock
        {
            Id = "InspectorDock",
            ActiveDockable = _inspectorTool,
            VisibleDockables = CreateList<IDockable>(_inspectorTool),
            Alignment = Alignment.Right,
            GripMode = GripMode.Visible,
            Proportion = 0.5
        };

        // Right tool window (Previewer)
        var previewerDock = new ToolDock
        {
            Id = "PreviewerDock",
            ActiveDockable = _previewerTool,
            VisibleDockables = CreateList<IDockable>(_previewerTool),
            Alignment = Alignment.Right,
            GripMode = GripMode.Visible,
            Proportion = 0.5
        };

        // Right tool area: Inspector on top, Previewer below, stacked vertically
        var rightToolsPane = new ProportionalDock
        {
            Id = "RightToolsPane",
            Orientation = Orientation.Vertical,
            IsCollapsable = false,
            VisibleDockables = CreateList<IDockable>(
                inspectorDock,
                new ProportionalDockSplitter(),
                previewerDock)
        };

        // Main horizontal proportional layout:[LeftTools(Explorer/Hierarchy)] | [Files] | [RightTools(Inspector/Previewer)]
        MainPane = new ProportionalDock
        {
            Id = "MainPane",
            Orientation = Orientation.Horizontal,
            IsCollapsable = false,
            VisibleDockables = CreateList<IDockable>(
                leftToolsPane,
                new ProportionalDockSplitter(),
                _fileDocumentDock,
                new ProportionalDockSplitter(),
                rightToolsPane)
        };

        _rootDock = CreateRootDock();
        _rootDock.Id = "Root";
        _rootDock.Title = "Default";
        _rootDock.IsCollapsable = false;
        _rootDock.VisibleDockables = CreateList<IDockable>(MainPane);
        _rootDock.ActiveDockable = MainPane;
        _rootDock.DefaultDockable = MainPane;

        return _rootDock;
    }

    /// <summary>
    /// DocumentDock "+" Button callback: create a new blank document.
    /// </summary>
    private void AddNewBlankDocument()
    {
        if (_fileDocumentDock is null)
        {
            return;
        }

        var newDoc = new BlankDocumentViewModel();
        AddDockable(_fileDocumentDock, newDoc);
        SetActiveDockable(newDoc);
    }

    public override void InitLayout(IDockable layout)
    {
        DockableLocator = new Dictionary<string, Func<IDockable?>>
        {
            ["Root"] = () => _rootDock,
            ["Files"] = () => _fileDocumentDock,
            ["WorkspaceExplorer"] = () => _workspaceExplorerTool,
            ["Hierarchy"] = () => _hierarchyTool,
            ["Inspector"] = () => _inspectorTool,
            ["Previewer"] = () => _previewerTool,
        };

        // HostWindow Used to support floating a dockable into a separate window
        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new HostWindow()
        };

        base.InitLayout(layout);
    }
}
