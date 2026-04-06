using System;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Satellite.Views;

namespace Satellite;

public class App : Application
{
    public static string? ShowWindowEventName { get; set; }

    private EventWaitHandle? _showWindowEvent;
    private RegisteredWaitHandle? _showWindowEventRegistration;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow();
            InitSingleInstanceSignal(desktop);
            // desktop.MainWindow.DataContext = new MainWindowViewModel((MainWindow)desktop.MainWindow);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove = BindingPlugins
            .DataValidators.OfType<DataAnnotationsValidationPlugin>()
            .ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
            BindingPlugins.DataValidators.Remove(plugin);
    }

    private void InitSingleInstanceSignal(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (string.IsNullOrWhiteSpace(ShowWindowEventName))
            return;

        _showWindowEvent = new EventWaitHandle(
            false,
            EventResetMode.AutoReset,
            ShowWindowEventName
        );

        _showWindowEventRegistration = ThreadPool.RegisterWaitForSingleObject(
            _showWindowEvent,
            (_, _) =>
                Dispatcher.UIThread.Post(() =>
                {
                    if (desktop.MainWindow is MainWindow mainWindow)
                        mainWindow.BringToFront();
                }),
            null,
            -1,
            false
        );

        desktop.Exit += (_, _) =>
        {
            _showWindowEventRegistration?.Unregister(null);
            _showWindowEventRegistration = null;
            _showWindowEvent?.Dispose();
            _showWindowEvent = null;
        };
    }
}
