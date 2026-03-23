using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using BitFab.KW1281Test.Ui.Views;

namespace BitFab.KW1281Test.Ui;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        // Global exception handlers
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Logger.Log?.WriteLine($"Unhandled exception: {ex?.Message}");
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Logger.Log?.WriteLine($"Unobserved task exception: {e.Exception.Message}");
            e.SetObserved();
        };

        base.OnFrameworkInitializationCompleted();
    }
}
