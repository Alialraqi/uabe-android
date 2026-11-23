namespace UABEAvalonia.Plugins
{
    /// <summary>
 /// Holds <see cref="IUavPluginPreviewer"/> and the <see cref="UavPluginPreviewerType"/>, 
    /// it supports, used for <see cref="PluginLoader.GetPreviewersThatSupport"/> returned result.
    /// it returns. Adapted from PluginPreviewerTypePair of UABEANext4.
    /// </summary>
    public class PluginPreviewerTypePair
    {
        /// <summary>Plugin previewer instance.</summary>
        public IUavPluginPreviewer Previewer { get; }

        /// <summary>The preview type this previewer supports for the current selection.</summary>
        public UavPluginPreviewerType PreviewType { get; }

        public PluginPreviewerTypePair(IUavPluginPreviewer previewer, UavPluginPreviewerType previewType)
        {
            Previewer = previewer;
            PreviewType = previewType;
        }

        public override string ToString() => Previewer.Name;
    }
}
