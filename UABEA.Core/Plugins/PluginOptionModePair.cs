namespace UABEAvalonia.Plugins
{
    /// <summary>
 /// Holds <see cref="IUavPluginOption"/> and one of the <see cref="UavPluginMode"/>, 
    /// it supports, used for <see cref="PluginLoader.GetOptionsThatSupport"/> returned result.
    /// Adapted from PluginOptionModePair of UABEANext4.
    /// </summary>
    public class PluginOptionModePair
    {
        /// <summary>Plugin option instance.</summary>
        public IUavPluginOption Option { get; }

        /// <summary>The mode this entry corresponds to (already split into an individual flag).</summary>
        public UavPluginMode Mode { get; }

        public PluginOptionModePair(IUavPluginOption option, UavPluginMode mode)
        {
            Option = option;
            Mode = mode;
        }

        public override string ToString() => Option.Name;
    }
}
