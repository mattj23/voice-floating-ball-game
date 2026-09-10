using Avalonia;
using ReactiveUI.Avalonia;
using System;
using Velopack;

namespace VoiceBallGame.App;

sealed class Program
{
    // Do not use Avalonia, third-party APIs, or code that depends on SynchronizationContext before
    // AppMain is called. The required services are not initialized before that point.
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack must run first. During installation, updates, and uninstallation, the installer
        // launches the app with hook arguments. This call handles the arguments and exits before
        // any window opens.
        VelopackApp.Build().Run();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Keep this Avalonia configuration because the visual designer also uses it.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI(_ => { });
}
