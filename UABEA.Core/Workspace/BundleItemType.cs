namespace UABEAvalonia
{
    /// <summary>
    /// Bundle File type classification inside, for the UI layer to do color mapping and other display.
    /// Decoupled from Avalonia: the core library only returns the type, and colors are handled by a Converter in the UI layer.
    /// </summary>
    public enum BundleItemType
    {
        Serialized,
        Ress,
        Resource,
        Etc
    }
}
