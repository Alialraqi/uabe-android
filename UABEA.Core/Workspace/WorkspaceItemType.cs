namespace UABEAvalonia
{
    // Type classification of file items in the workspace.
    // BundleFile   - Unity bundle file (may contain multiple internal files)
    // AssetsFile   - Serialized asset file (.assets or serialized entries inside a bundle)
    // ResourceFile - Resource file (.resS / .resource and other binary resources)
    // OtherFile    - Other files that cannot be categorized
    public enum WorkspaceItemType
    {
        BundleFile,
        AssetsFile,
        ResourceFile,
        OtherFile
    }
}
