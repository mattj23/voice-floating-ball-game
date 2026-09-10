using System;
using System.IO;
using Velopack.Locators;
using VoiceBallGame.Core.Configuration;

namespace VoiceBallGame.App.Services;

/// <summary>
/// Determines where to store the settings file, microphone calibrations, and trial data.
/// </summary>
/// <remarks>
/// A development build stores calibrations beside the executable, loads settings from beside the
/// executable or from the repository's config folder, and writes trials relative to the working
/// directory. Velopack replaces an installed build's application folder on every update, and the
/// macOS app bundle and Linux AppImage are read-only. Therefore, an installed build stores its
/// settings and calibrations in the per-user configuration folder and writes trials under the
/// user's documents folder. These files survive updates and uninstalls.
/// </remarks>
public sealed class AppPaths
{
    private const string FolderName = "VoiceBallGame";
    private const string CalibrationsFileName = "calibrations.json";

    private AppPaths(bool isInstalled, string? settingsFile, string calibrationsFile, string dataRoot)
    {
        IsInstalled = isInstalled;
        SettingsFile = settingsFile;
        CalibrationsFile = calibrationsFile;
        DataRoot = dataRoot;
    }

    /// <summary>Indicates whether the application is running from a Velopack installation.</summary>
    public bool IsInstalled { get; }

    /// <summary>
    /// The settings file to load, or <see langword="null"/> if no file was found and the default
    /// settings apply.
    /// </summary>
    public string? SettingsFile { get; }

    public string CalibrationsFile { get; }

    /// <summary>The directory a relative <c>output_directory</c> is resolved against.</summary>
    public string DataRoot { get; }

    public static AppPaths Resolve()
    {
        return IsVelopackInstall() ? Installed() : Development();
    }

    private static bool IsVelopackInstall()
    {
        try
        {
            return VelopackLocator.IsCurrentSet && VelopackLocator.Current.CurrentlyInstalledVersion is not null;
        }
        catch (Exception)
        {
            // The XAML previewer and unusual launch environments have no locator. Treat those as
            // development builds.
            return false;
        }
    }

    private static AppPaths Installed()
    {
        var configDirectory = Path.Combine(ConfigRoot(), FolderName);
        var settingsFile = Path.Combine(configDirectory, SettingsLoader.FileName);

        SeedSettings(settingsFile);

        return new AppPaths(
            isInstalled: true,
            settingsFile: settingsFile,
            calibrationsFile: Path.Combine(configDirectory, CalibrationsFileName),
            dataRoot: Path.Combine(DocumentsRoot(), FolderName));
    }

    private static AppPaths Development()
    {
        return new AppPaths(
            isInstalled: false,
            settingsFile: SearchForSettings(),
            calibrationsFile: Path.Combine(AppContext.BaseDirectory, CalibrationsFileName),
            dataRoot: Directory.GetCurrentDirectory());
    }

    /// <summary>
    /// Copies the settings file shipped with the application into the user's configuration folder
    /// the first time an installed build runs. An existing file is never replaced, because it holds
    /// the experimenter's edits.
    /// </summary>
    private static void SeedSettings(string settingsFile)
    {
        if (File.Exists(settingsFile)) return;

        var bundled = Path.Combine(AppContext.BaseDirectory, SettingsLoader.FileName);
        if (!File.Exists(bundled)) return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsFile)!);
            File.Copy(bundled, settingsFile, overwrite: false);
        }
        catch (IOException)
        {
            // Leave the file missing. Loading it then reports the path to the operator instead of
            // silently running on defaults.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Locates <c>app_settings.toml</c> beside the executable or in a <c>config</c> folder above it.
    /// </summary>
    private static string? SearchForSettings()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            foreach (var candidate in new[]
                     {
                         Path.Combine(directory.FullName, SettingsLoader.FileName),
                         Path.Combine(directory.FullName, "config", SettingsLoader.FileName),
                     })
            {
                if (File.Exists(candidate)) return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string ConfigRoot()
    {
        // On macOS, .NET maps ApplicationData to ~/.config, which is unusual for that platform.
        // LocalApplicationData maps to ~/Library/Application Support. On Windows, ApplicationData
        // maps to %AppData% (roaming). On Linux, it follows XDG_CONFIG_HOME.
        var folder = OperatingSystem.IsMacOS()
            ? Environment.SpecialFolder.LocalApplicationData
            : Environment.SpecialFolder.ApplicationData;

        return FolderOrHome(folder);
    }

    private static string DocumentsRoot() => FolderOrHome(Environment.SpecialFolder.MyDocuments);

    private static string FolderOrHome(Environment.SpecialFolder folder)
    {
        var path = Environment.GetFolderPath(folder, Environment.SpecialFolderOption.DoNotVerify);
        if (!string.IsNullOrEmpty(path)) return path;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(home) ? AppContext.BaseDirectory : home;
    }
}
