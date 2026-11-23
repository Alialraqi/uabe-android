using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using UABEAvalonia.Plugins;
using UABEAvalonia.ViewModels.Documents;

namespace UABEAvalonia.ViewModels;

/// <summary>
/// Dock Root ViewModel of the main window. Holds Workspace, DockFactory and Layout(IRootDock).
/// Follows the design of UABEANext4 MainViewModel:
///   - On construction, call factory.CreateLayout + factory.InitLayout to finish Dock tree initialization
///   - Listen to SelectedWorkspaceItemChangedMessage and open the corresponding asset document automatically
///   - Provide FileOpen / FileCloseAll and other commands for menu binding
/// Note: this class inherits ViewModelBase (which lives in UABEA.Core and includes ObservableObject),
/// so it can use directly [ObservableProperty] / [RelayCommand]. 
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private const string DefaultWindowTitle = "UABEA - Dock";

    /// <summary>Multi-bundle workspace</summary>
    public Workspace Workspace { get; }

    /// <summary>Dock factory, can be used for dynamic operations when needed externally dockable</summary>
    public MainDockFactory Factory { get; }

    /// <summary>Dock Root layout, bound to DockControl.Layout</summary>
    [ObservableProperty]
    private IRootDock? _layout;

    /// <summary>Window title; version info is appended after a file is loaded</summary>
    [ObservableProperty]
    private string _windowTitle = DefaultWindowTitle;

    public MainViewModel()
        : this(null)
    {
    }

    /// <summary>
    /// Inject the new plugin loader (filled when App loads the plugins directory at startup).
    /// Via <see cref="MainDockFactory"/> forwarded to PreviewerToolViewModel.
    /// pluginLoader When null, falls back to the built-in previewer list (backward compatible).
    /// </summary>
    public MainViewModel(PluginLoader? pluginLoader)
    {
        Workspace = new Workspace();
        Factory = new MainDockFactory(Workspace, pluginLoader);
        Layout = Factory.CreateLayout();
        if (Layout is not null)
        {
            Factory.InitLayout(Layout);
        }

        // Listen to workspace selection changes (from WorkspaceExplorer) and open documents automatically
        WeakReferenceMessenger.Default.Register<SelectedWorkspaceItemChangedMessage>(this, OnSelectedItemChanged);
    }

    /// <summary>
    /// File > Open Menu command: pop up the file picker dialog and load into the workspace.
    /// Through the IDialogService abstraction, easy for the Android side to replace the implementation.
    /// </summary>
    [RelayCommand]
    private async Task FileOpen()
    {
        var dialogService = App.Services.GetService(typeof(IDialogService)) as IDialogService;
        if (dialogService is null)
        {
            return;
        }

        var files = await dialogService.ShowOpenFileDialog(
            title: "Open bundle or assets file",
            multiSelect: true);

        if (files is null || files.Count == 0)
        {
            return;
        }

        foreach (string path in files)
        {
            if (!File.Exists(path))
            {
                continue;
            }
            try
            {
                Workspace.LoadAnyFile(path);
            }
            catch (Exception ex)
            {
                await dialogService.ShowMessageDialog("Load error", $"{Path.GetFileName(path)}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// File > Close All Menu command: clear the workspace and close all documents.
    /// </summary>
    [RelayCommand]
    private void FileCloseAll()
    {
        // Notify components (Inspector, Asset documents, etc.) to clear their state
        WeakReferenceMessenger.Default.Send(new WorkspaceClosingMessage(true));

        // Note: the current P1 stage does not implement the full RemoveFile loop (depends on save logic),
        // only clears RootItems. It will be completed after Workspace.CloseAll is connected in a later stage.
        var rootCopy = Workspace.RootItems.ToArray();
        foreach (var item in rootCopy)
        {
            Workspace.RemoveFile(item.Name);
        }

        WindowTitle = DefaultWindowTitle;
    }

    /// <summary>
    /// File > Settings Menu command: open the Settings window.
    /// SettingsViewModel Reads and writes ConfigurationManager.Settings directly,
    /// and triggers an immediate save when closed.
    /// </summary>
    [RelayCommand]
    private void FileSettings()
    {
        var window = new Forms.SettingsWindow();
        if (Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            window.ShowDialog(desktop.MainWindow);
        }
        else
        {
            window.Show();
        }
    }

    /// <summary>
    /// File > Exit Menu command: close the main window.
    /// </summary>
    [RelayCommand]
    private void FileExit()
    {
        WeakReferenceMessenger.Default.Send(new WorkspaceClosingMessage(true));
        // Closing the window is handled by DockMainWindow (through the Closing event or main menu command binding)
    }

    /// <summary>
    /// Try to open an asset document when a WorkspaceExplorer selection change message is received.
 /// Only handles root-level AssetsFile; other types (Bundle / Resource)do not open a document for now.
    /// </summary>
    private void OnSelectedItemChanged(object recipient, SelectedWorkspaceItemChangedMessage message)
    {
        _ = OpenAssetDocumentFor(message.Value);
    }

    private Task OpenAssetDocumentFor(object? selectedItem)
    {
        if (Layout is null || Factory is null)
        {
            return Task.CompletedTask;
        }

        if (selectedItem is not WorkspaceItem wsItem)
        {
            return Task.CompletedTask;
        }

        // Only root-level items of AssetsFile type make sense for opening an asset document
        if (wsItem.ItemType != WorkspaceItemType.AssetsFile)
        {
            return Task.CompletedTask;
        }

        var files = Factory.GetDockable<IDocumentDock>("Files");
        if (files is null)
        {
            return Task.CompletedTask;
        }

        var document = new AssetDocumentViewModel(wsItem);
        Factory.AddDockable(files, document);
        Factory.SetActiveDockable(document);
        Factory.SetFocusedDockable(files, document);

        return Task.CompletedTask;
    }
}
