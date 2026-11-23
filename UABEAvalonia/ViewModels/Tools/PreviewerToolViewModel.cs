using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model.Mvvm.Controls;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UABEAvalonia.Mesh;
using UABEAvalonia.Plugins;

namespace UABEAvalonia.ViewModels.Tools
{
    /// <summary>
    /// Unified Previewer panel. Adapted from PreviewerToolViewModel of UABEANext4.
 /// Listen to <see cref="AssetsSelectedMessage"/>, Through the injected <see cref="PluginLoader"/>
 /// find the first previewer that supports the current selection, by type (Text / Image / Mesh)render.
    ///
    /// PluginLoader Filled when App loads the plugins directory at startup, via MainViewModel ->
    /// MainDockFactory injected into this class. When _pluginLoader is null, falls back to the built-in _previewers
 /// list (currently empty), in which case selecting an asset shows "No preview available.". 
    /// </summary>
    public partial class PreviewerToolViewModel : Tool
    {
        private const string ToolTitle = "Previewer";

        /// <summary>Reference to the multi-bundle workspace, used by previewers for queries.</summary>
        public Workspace Workspace { get; }

        /// <summary>Current preview type; the view switches the display panel accordingly.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsTextPreview))]
        [NotifyPropertyChangedFor(nameof(IsImagePreview))]
        [NotifyPropertyChangedFor(nameof(IsMeshPreview))]
        [NotifyPropertyChangedFor(nameof(IsNonePreview))]
        private UavPluginPreviewerType _previewType = UavPluginPreviewerType.None;

        /// <summary>Text preview content (shown whenPreviewType == Text is).</summary>
        [ObservableProperty]
        private string _previewText = "No preview available.";

        /// <summary>Image preview bitmap (PreviewType == Image is).</summary>
        [ObservableProperty]
        private Bitmap? _previewImage;

        /// <summary>Mesh 3D preview data (PreviewType == Mesh rendered by MeshPreviewerControl when).</summary>
        [ObservableProperty]
        private MeshObj? _activeMesh;

        /// <summary>Image preview sub VM, carrying zoom / background / size info.</summary>
        [ObservableProperty]
        private ImagePreviewViewModel _imagePreview = new();

        // List of currently registered built-in previewers. The new plugin system provides them through _pluginLoader,
        // this list is only a fallback (used when _pluginLoader is null) and is currently empty.
        private readonly List<IUavPluginPreviewer> _previewers = new();

        // New plugin loader (connected in P2). When null, falls back to the built-in _previewers list.
        // By MainViewModel -> MainDockFactory -> this constructor injects.
        private readonly PluginLoader? _pluginLoader;

        /// <summary>Whether it is currently a text preview.</summary>
        public bool IsTextPreview => PreviewType == UavPluginPreviewerType.Text;

        /// <summary>Whether it is currently an image preview.</summary>
        public bool IsImagePreview => PreviewType == UavPluginPreviewerType.Image;

        /// <summary>Whether it is currently a Mesh 3D preview.</summary>
        public bool IsMeshPreview => PreviewType == UavPluginPreviewerType.Mesh;

        /// <summary>Whether there is currently no preview to show (shows hint text).</summary>
        public bool IsNonePreview => PreviewType == UavPluginPreviewerType.None;

        public PreviewerToolViewModel(Workspace workspace)
            : this(workspace, null)
        {
        }

        /// <summary>
        /// Inject the new plugin loader. When _pluginLoader is not null, prefer through
        /// <see cref="PluginLoader.GetPreviewersThatSupport"/> to find previewers;
        /// when null, falls back to the built-in <c>_previewers</c> list.
        /// </summary>
        public PreviewerToolViewModel(Workspace workspace, PluginLoader? pluginLoader)
        {
            Workspace = workspace;
            _pluginLoader = pluginLoader;

            Id = ToolTitle;
            Title = ToolTitle;

            // Listen to asset selection messages (from asset documents)
            WeakReferenceMessenger.Default.Register<AssetsSelectedMessage>(this, OnAssetsSelected);
            // Listen to the workspace closed message and clear the preview
            WeakReferenceMessenger.Default.Register<WorkspaceClosingMessage>(this, OnWorkspaceClosing);
        }

        private void OnAssetsSelected(object recipient, AssetsSelectedMessage message)
        {
            HandleAssetPreview(message.Value);
        }

        private void OnWorkspaceClosing(object recipient, WorkspaceClosingMessage message)
        {
            HandleAssetPreview(null);
        }

        /// <summary>
        /// Handle asset preview: find a previewer supporting the current selection and render by type.
        /// </summary>
        private void HandleAssetPreview(List<AssetContainer>? assets)
        {
            if (assets == null || assets.Count == 0)
            {
                SetEmpty();
                return;
            }

            IUavPluginPreviewer? previewer = null;
            UavPluginPreviewerType previewType = UavPluginPreviewerType.None;

            // New plugin system: find a previewer supporting the current selection through PluginLoader.
            // _pluginLoader When null (not injected), falls back to the built-in _previewers list.
            // Wrap the lookup logic in try-catch, ensuring no crash when the plugin system is not ready / or a plugin throws.
            try
            {
                if (_pluginLoader != null)
                {
                    var pairs = _pluginLoader.GetPreviewersThatSupport(Workspace, assets);
                    if (pairs.Count > 0)
                    {
                        previewer = pairs[0].Previewer;
                        previewType = pairs[0].PreviewType;
                    }
                }
                else
                {
                    foreach (var p in _previewers)
                    {
                        var type = p.SupportsPreview(Workspace, assets);
                        if (type != UavPluginPreviewerType.None)
                        {
                            previewer = p;
                            previewType = type;
                            break;
                        }
                    }
                }
            }
            catch
            {
                // The plugin system is not ready or threw an exception; ignore and show an empty preview
            }

            if (previewer == null)
            {
                SetDisplayText("No preview available.");
                return;
            }

            try
            {
                switch (previewType)
                {
                    case UavPluginPreviewerType.Text:
                        PreviewType = UavPluginPreviewerType.Text;
                        ClearImage();
                        PreviewText = previewer.ExecuteText(Workspace, assets) ?? string.Empty;
                        break;

                    case UavPluginPreviewerType.Image:
                        {
                            PreviewType = UavPluginPreviewerType.Image;
                            byte[] bgra = previewer.ExecuteImage(Workspace, assets, out int width, out int height);
                            WriteableBitmap? bitmap = CreateBitmapFromBgra(bgra, width, height);
                            if (bitmap != null)
                            {
                                ImagePreview.UpdateImage(bitmap);
                                PreviewImage = bitmap;
                            }
                            else
                            {
                                SetDisplayText("Failed to decode image.");
                            }
                            break;
                        }

                    case UavPluginPreviewerType.Mesh:
                        {
                            PreviewType = UavPluginPreviewerType.Mesh;
                            ClearImage();
                            MeshObj? meshObj = previewer.ExecuteMesh(Workspace, assets);
                            if (meshObj != null)
                            {
                                ActiveMesh = meshObj;
                            }
                            else
                            {
                                SetDisplayText("No preview available (mesh couldn't be loaded).");
                            }
                            break;
                        }

                    default:
                        SetDisplayText($"Preview type {previewType} not supported.");
                        break;
                }
            }
            catch (Exception ex)
            {
                SetDisplayText($"Preview error: {ex.Message}");
            }
        }

        /// <summary>Switch to text preview and show the specified text.</summary>
        private void SetDisplayText(string text)
        {
            ClearImage();
            PreviewType = UavPluginPreviewerType.Text;
            PreviewText = text;
        }

        /// <summary>Clear the preview (back to the None state).</summary>
        private void SetEmpty()
        {
            ClearImage();
            PreviewType = UavPluginPreviewerType.None;
            PreviewText = string.Empty;
        }

        /// <summary>Release image preview resources and clear the bitmap reference.</summary>
        private void ClearImage()
        {
            ImagePreview.UpdateImage(null);
            PreviewImage = null;
            ActiveMesh = null;
        }

        /// <summary>
 /// Convert BGRA pixel data into Avalonia <see cref="WriteableBitmap"/>. 
        /// Task constraint reference: ExecuteImage returns BGRA bytes, and the host converts them into a bitmap.
        /// </summary>
        private static WriteableBitmap? CreateBitmapFromBgra(byte[] bgra, int width, int height)
        {
            if (bgra == null || bgra.Length == 0 || width <= 0 || height <= 0)
            {
                return null;
            }

            var bitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Unpremul);

            using (var framebuffer = bitmap.Lock())
            {
                int sourceStride = width * 4;
                int targetStride = framebuffer.RowBytes;
                if (targetStride == sourceStride)
                {
                    // stride consistent, copy the whole block
                    Marshal.Copy(bgra, 0, framebuffer.Address, bgra.Length);
                }
                else
                {
                    // stride inconsistent, copy row by row
                    for (int y = 0; y < height; y++)
                    {
                        Marshal.Copy(bgra, y * sourceStride, IntPtr.Add(framebuffer.Address, y * targetStride), sourceStride);
                    }
                }
            }

            return bitmap;
        }
    }
}
