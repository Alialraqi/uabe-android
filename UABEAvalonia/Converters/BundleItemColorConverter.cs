using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace UABEAvalonia
{
    /// <summary>
    /// Convert BundleItemType into a themed IBrush.
    /// P3-A Changed: from hard-coded Dark/Light brush pair to look up through TryFindResource
    /// SimpleLight/SimpleDark.axaml the WorkspaceItem*Brush resources in,
    /// automatically following RequestedThemeVariant switching.
    /// </summary>
    public class BundleItemColorConverter : IValueConverter
    {
        private const string BundleKey = "WorkspaceItemBundleBrush";
        private const string AssetsKey = "WorkspaceItemAssetsBrush";
        private const string ResourceKey = "WorkspaceItemResourceBrush";
        private const string OtherKey = "WorkspaceItemOtherBrush";
        private const string EtcKey = "WorkspaceItemEtcBrush";

        private static readonly SolidColorBrush Fallback = new SolidColorBrush(Colors.Gray);

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not BundleItemType itemType)
                return null;

            string key = itemType switch
            {
                BundleItemType.Serialized => AssetsKey,
                BundleItemType.Ress => OtherKey,
                BundleItemType.Resource => ResourceKey,
                _ => EtcKey,
            };

            return TryFindBrush(key);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static IBrush TryFindBrush(string key)
        {
            var app = Application.Current;
            if (app != null
                && app.TryGetResource(key, app.ActualThemeVariant, out object? resource)
                && resource is IBrush brush)
            {
                return brush;
            }
            return Fallback;
        }
    }
}
