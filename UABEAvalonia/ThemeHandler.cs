using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEAvalonia
{
    /// <summary>
    /// Theme switch handler.
 /// P2-C Changed: added <see cref="ApplyConfigurationTheme"/>, 
    /// Map <see cref="ConfigurationThemeType"/> to Avalonia ThemeVariant;
    /// and called by App.axaml.cs at startup and when Settings.ThemeType changes.
    /// Keep <see cref="UseDarkTheme"/> static property for compatibility with the old menu toggle call.
    /// </summary>
    public static class ThemeHandler
    {
        private static bool _useDarkTheme;
        public static bool UseDarkTheme
        {
            get
            {
                return _useDarkTheme;
            }
            set
            {
                if (Application.Current == null)
                    return;

                Application.Current.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light;
                _useDarkTheme = value;
            }
        }

        /// <summary>
        /// According to <see cref="ConfigurationThemeType"/> apply the theme.
 /// Auto -> ThemeVariant.Default (follow the system),Light/Dark -> corresponds to ThemeVariant.
        /// Synchronously update the internal <see cref="UseDarkTheme"/> state to stay backward compatible.
        /// </summary>
        public static void ApplyConfigurationTheme(ConfigurationThemeType themeType)
        {
            if (Application.Current == null)
                return;

            Application.Current.RequestedThemeVariant = themeType switch
            {
                ConfigurationThemeType.Auto => ThemeVariant.Default,
                ConfigurationThemeType.Light => ThemeVariant.Light,
                ConfigurationThemeType.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };

            // Auto In mode _useDarkTheme cannot simply be determined; here only records Light/Dark binary
            _useDarkTheme = themeType == ConfigurationThemeType.Dark;
        }
    }
}
