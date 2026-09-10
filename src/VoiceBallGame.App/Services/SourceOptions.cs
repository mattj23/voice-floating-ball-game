using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VoiceBallGame.Core.Configuration;
using VoiceBallGame.Core.Replay;
using VoiceBallGame.Core.Signals;

namespace VoiceBallGame.App.Services;

/// <summary>
/// A selectable input on the setup screen, such as a real device, a recording, or the simulated
/// participant. The screen displays these options without requiring changes for each input type.
/// </summary>
public abstract class SourceOption
{
    protected SourceOption(string name, string detail)
    {
        Name = name;
        Detail = detail;
    }

    public string Name { get; }

    /// <summary>A short line under the name, such as a port or file name.</summary>
    public string Detail { get; }

    /// <summary>True for inputs that need no hardware, so the view can mark them as such.</summary>
    public virtual bool IsSimulated => false;

    public override string ToString() => Name;
}

public abstract class FlowSourceOption : SourceOption
{
    protected FlowSourceOption(string name, string detail) : base(name, detail) { }

    public abstract IFlowSource Create(IGameClock clock, GameSettings settings);
}

public abstract class VolumeSourceOption : SourceOption
{
    protected VolumeSourceOption(string name, string detail) : base(name, detail) { }

    public abstract IVolumeSource Create(IGameClock clock, GameSettings settings);

    /// <summary>
    /// Whether this input needs a loudness calibration before it can be played. Recordings and
    /// the simulated participant already carry dB SPL values.
    /// </summary>
    public virtual bool RequiresCalibration => false;
}

/// <summary>Both halves of the simulated participant, which needs no hardware at all.</summary>
public sealed class SimulatedFlowOption : FlowSourceOption
{
    public SimulatedFlowOption() : base(SimulatedParticipant.Name, "Voices in bouts around the target ratio") { }

    public override bool IsSimulated => true;

    public override IFlowSource Create(IGameClock clock, GameSettings settings) =>
        SimulatedParticipant.Create(clock, TimeSpan.FromMilliseconds(settings.BufferMs), settings.GoalRatio).Flow;
}

public sealed class SimulatedVolumeOption : VolumeSourceOption
{
    public SimulatedVolumeOption() : base(SimulatedParticipant.Name, "Voices in bouts around the target ratio") { }

    public override bool IsSimulated => true;

    public override IVolumeSource Create(IGameClock clock, GameSettings settings) =>
        SimulatedParticipant.Create(clock, TimeSpan.FromMilliseconds(settings.BufferMs), settings.GoalRatio).Volume;
}

public sealed class ReplayFlowOption : FlowSourceOption
{
    private readonly string _path;

    public ReplayFlowOption(string name, string path) : base(name, Path.GetFileName(path)) => _path = path;

    public override bool IsSimulated => true;

    public override IFlowSource Create(IGameClock clock, GameSettings settings) =>
        ReplayFlowSource.FromFile(_path, clock);
}

public sealed class ReplayVolumeOption : VolumeSourceOption
{
    private readonly string _path;

    public ReplayVolumeOption(string name, string path) : base(name, Path.GetFileName(path)) => _path = path;

    public override bool IsSimulated => true;

    public override IVolumeSource Create(IGameClock clock, GameSettings settings) =>
        ReplayVolumeSource.FromFile(_path, clock);
}

/// <summary>
/// Builds the list of inputs the operator can choose from. Hardware is discovered through the
/// providers passed in, so the application runs with an empty list of devices on a machine that
/// has none.
/// </summary>
public sealed class SourceCatalog
{
    private readonly GameSettings _settings;
    private readonly string _settingsDirectory;
    private readonly Func<IEnumerable<FlowSourceOption>>? _hardwareFlow;
    private readonly Func<IEnumerable<VolumeSourceOption>>? _hardwareVolume;

    public SourceCatalog(
        GameSettings settings,
        string settingsDirectory,
        Func<IEnumerable<FlowSourceOption>>? hardwareFlow = null,
        Func<IEnumerable<VolumeSourceOption>>? hardwareVolume = null)
    {
        _settings = settings;
        _settingsDirectory = settingsDirectory;
        _hardwareFlow = hardwareFlow;
        _hardwareVolume = hardwareVolume;
    }

    public IReadOnlyList<FlowSourceOption> FlowOptions()
    {
        var options = new List<FlowSourceOption>();

        options.AddRange(SafeEnumerate(_hardwareFlow));

        foreach (var replay in _settings.ReplayProviders)
        {
            if (string.IsNullOrWhiteSpace(replay.FlowFile)) continue;
            options.Add(new ReplayFlowOption(replay.Name, Resolve(replay.FlowFile)));
        }

        options.Add(new SimulatedFlowOption());
        return options;
    }

    public IReadOnlyList<VolumeSourceOption> VolumeOptions()
    {
        var options = new List<VolumeSourceOption>();

        options.AddRange(SafeEnumerate(_hardwareVolume));

        foreach (var replay in _settings.ReplayProviders)
        {
            if (string.IsNullOrWhiteSpace(replay.VolumeFile)) continue;
            options.Add(new ReplayVolumeOption(replay.Name, Resolve(replay.VolumeFile)));
        }

        options.Add(new SimulatedVolumeOption());
        return options;
    }

    private string Resolve(string path) =>
        Path.IsPathRooted(path) ? path : Path.Combine(_settingsDirectory, path);

    /// <summary>
    /// Device enumeration calls the operating system and can throw when a driver is missing or a
    /// port is held by another program. A failure removes only the affected device type from the
    /// available options. The rest of the setup screen remains usable.
    /// </summary>
    private static IEnumerable<T> SafeEnumerate<T>(Func<IEnumerable<T>>? enumerate)
    {
        if (enumerate is null) return [];

        try
        {
            return enumerate().ToList();
        }
        catch
        {
            return [];
        }
    }
}
