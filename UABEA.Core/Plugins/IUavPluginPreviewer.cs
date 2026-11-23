using System.Collections.Generic;
using UABEAvalonia.Mesh;

namespace UABEAvalonia.Plugins
{
    /// <summary>
    /// Plugin previewer interface. Plugins implement this interface to provide text / and image previews to the unified Previewer panel.
    /// Adapted from IUavPluginPreviewer of UABEANext4, but made platform-independent:
    ///   - Does not reference Avalonia (types such as Bitmap are converted by the host in the UI layer)
    ///   - Selection uses <see cref="List{AssetContainer}"/> rather than a single AssetInst,
    ///     to support multi-selection preview extension
    ///   - Images are returned as BGRA raw pixel bytes and converted by the host into WriteableBitmap
    /// </summary>
    public interface IUavPluginPreviewer
    {
        /// <summary>Previewer name (for display).</summary>
        string Name { get; }

        /// <summary>Previewer description.</summary>
        string Description { get; }

        /// <summary>
        /// Determines whether the current selection can be handled by this previewer, returns the supported preview type;
        /// returns <see cref="UavPluginPreviewerType.None"/> means not supported.
        /// </summary>
        UavPluginPreviewerType SupportsPreview(Workspace workspace, List<AssetContainer> selection);

        /// <summary>
        /// Execute text preview, returns the preview text content.
        /// Only called when <see cref="SupportsPreview"/> returns <see cref="UavPluginPreviewerType.Text"/> is returned.
        /// </summary>
        string ExecuteText(Workspace workspace, List<AssetContainer> selection);

        /// <summary>
        /// Execute image preview, returns BGRA pixel data, and returns width and height through out parameters.
        /// Only called when <see cref="SupportsPreview"/> returns <see cref="UavPluginPreviewerType.Image"/> is returned.
        /// The host converts the returned byte array into a platform bitmap (such as Avalonia WriteableBitmap).
        /// </summary>
        byte[] ExecuteImage(Workspace workspace, List<AssetContainer> selection, out int width, out int height);

        /// <summary>
 /// Execute Mesh 3D preview, returns the parsed <see cref="MeshObj"/> (containing vertices, / normals, / indices).
        /// Only called when <see cref="SupportsPreview"/> returns <see cref="UavPluginPreviewerType.Mesh"/> is returned.
        /// The host binds it to an OpenGL rendering control (such as MeshPreviewerControl).
        /// </summary>
        MeshObj? ExecuteMesh(Workspace workspace, List<AssetContainer> selection);

        /// <summary>
        /// Workspace closed / called on reset, releases resources held by the previewer.
        /// </summary>
        void Cleanup();
    }
}
