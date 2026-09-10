using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VoiceBallGame.App.ViewModels;
using VoiceBallGame.App.Views;

namespace VoiceBallGame.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var shell = new MainViewModel();

            // --demo skips the setup screen and plays against the simulated participant, so the
            // game can be shown running on a machine with no flow meter or microphone attached.
            if (desktop.Args?.Contains("--demo") == true && !shell.HasFatalError)
                shell.StartSimulatedSession();

            desktop.MainWindow = new MainWindow { DataContext = shell };
        }

        base.OnFrameworkInitializationCompleted();
    }
}