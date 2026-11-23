namespace UABEAvalonia.Plugins
{
    /// <summary>
    /// Preview types supported by plugin previewers. Adapted from UavPluginPreviewerType of UABEANext4.
    /// Located in the platform-independent UABEA.Core, shared by the main project and the Android side.
    /// </summary>
    public enum UavPluginPreviewerType
    {
        /// <summary>Preview not supported.</summary>
        None,
        /// <summary>Text preview (read-only text).</summary>
        Text,
        /// <summary>Image preview (BGRA pixel data).</summary>
        Image,
        /// <summary>Mesh preview (left for P1-C to implement).</summary>
        Mesh
    }
}
