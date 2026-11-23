using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel;
using UABEAvalonia.Plugins;
using UABEAvalonia.ViewModels;

namespace UABEAvalonia
{
    public class App : Application
    {
        public static IServiceProvider Services { get; private set; } = new ServiceCollection().BuildServiceProvider();

        /// <summary>
        /// Whether to enable the Dock multi-document UI entry.
        /// Via environment variable UABEA_USE_DOCK=1 / true switch to DockMainWindow,
        /// otherwise keep the existing MainWindow as the default entry (backward compatible).
        /// </summary>
        public static bool UseDockInterface { get; } =
            IsDockFlagEnabled(Environment.GetEnvironmentVariable("UABEA_USE_DOCK"));

        private static bool IsDockFlagEnabled(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            return value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
 // P2-C: Apply the theme from configuration at startup (no longer hard-coded Light)
            ThemeHandler.ApplyConfigurationTheme(ConfigurationManager.Settings.ThemeType);

            // Subscribe to ThemeType changes: takes effect immediately after the user switches theme in Settings
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
            ConfigureServices();

#if ANDROID
            if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            {
                singleView.MainView = new MainWindow();
            }
#else
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Choose the main window entry by environment variable, keeping the old MainWindow as default.
                if (UseDockInterface)
                {
                    var dockWindow = new DockMainWindow();
                    // Get MainViewModel from DI (Dock layout initialization completed in the constructor)
                    var mainVm = Services.GetService<MainViewModel>();
                    if (mainVm != null)
                    {
                        dockWindow.SetViewModel(mainVm);
                    }
                    desktop.MainWindow = dockWindow;

                    var dialogService = Services.GetService<IDialogService>() as DialogService;
                    dialogService?.SetMainWindow(dockWindow);
                }
                else
                {
                    desktop.MainWindow = new MainWindow();

                    var dialogService = Services.GetService<IDialogService>() as DialogService;
                    dialogService?.SetMainWindow(desktop.MainWindow);
                }
            }
#endif
            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>
        /// Configure the DI container, registering platform-independent services and desktop implementations.
        /// Android the side can override this method in its own App.axaml.cs to register a touch implementation.
        /// </summary>
        protected virtual void ConfigureServices()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IDialogService, DialogService>();
            // New plugin loader (parallel with the old PluginManager, used by the Dock PreviewerTool)
            services.AddSingleton<PluginLoader>();
 // P1 Docking: Register MainViewModel (Workspace + MainDockFactory are initialized inside its constructor)
            // MainViewModel Inject PluginLoader through DI, then forward it to PreviewerToolViewModel.
            services.AddSingleton<MainViewModel>();
            Services = services.BuildServiceProvider();

            // Scan and load plugin dlls under the plugins directory.
            // Does not affect the old PluginManager (used by InfoWindow).
            // A load failure should not block the main program from starting, so it is guarded with try-catch.
            try
            {
                var pluginLoader = Services.GetRequiredService<PluginLoader>();
                pluginLoader.LoadPluginsInDirectory("plugins");
            }
            catch
            {
                // Ignore when plugin directory loading fails; the main program keeps running
            }
        }
    }
}
