using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using System;
using System.ComponentModel;
using UABEAvalonia;

namespace UABEA.Android
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            // P3-A Android adaptation: apply the theme from configuration at startup (no longer hard-coded Light)
            ThemeHandler.ApplyConfigurationTheme(ConfigurationManager.Settings.ThemeType);

            // Subscribe to ThemeType changes: takes effect immediately after the user switches theme in the Settings panel
            ConfigurationManager.Settings.PropertyChanged += OnSettingsPropertyChanged;
        }

        private static void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ConfigurationValues.ThemeType))
            {
                ThemeHandler.ApplyConfigurationTheme(ConfigurationManager.Settings.ThemeType);
            }
        }

        public override void OnFrameworkInitializationCompleted()
        {
            try
            {
                if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
                {
                    // If OnCreate crashed, show the crash screen directly
                    if (CrashLogger.IsCrashed)
                    {
                        singleView.MainView = new CrashView();
                    }
                    else
                    {
                        singleView.MainView = new MainView();
                    }
                }
                base.OnFrameworkInitializationCompleted();
            }
            catch (Exception ex)
            {
                CrashLogger.Log("App.OnFrameworkInitializationCompleted", ex);
                // On crash, write to the clipboard directly, without depending on the UI starting
                CrashLogger.CopyCrashToClipboard(
                    CrashLogger.BuildCrashReport("App.OnFrameworkInitializationCompleted", ex));
                // Last-resort attempt: switch to the crash view
                try
                {
                    if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
                        singleView.MainView = new CrashView();
                }
                catch { }
            }
        }
    }
}
