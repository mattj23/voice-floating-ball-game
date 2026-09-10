using System.Reactive.Linq;
using VoiceBallGame.Core.Configuration;
using VoiceBallGame.Core.Engine;
using VoiceBallGame.Core.Recording;
using VoiceBallGame.Core.Replay;
using VoiceBallGame.Core.Signals;

namespace VoiceBallGame.Core.Tests;

/// <summary>
/// Exercises the complete pipeline (sources, tick, engine, and recorder) with recorded signals in
/// place of hardware. This is the path that lets the game be developed and demonstrated on a
/// machine that has no flow meter attached.
/// </summary>
public class GameLoopTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"ballgame-loop-{Guid.NewGuid():N}");

    private static GameSettings Settings(string outputDirectory) => new()
    {
        EngineTickMs = 10,
        StaleTicks = 20,
        HistoryWindow = 5,
        TrialStartWindow = 3,
        OutputDirectory = outputDirectory,
        WriteCsv = true,
    };

    /// <summary>Builds a recording that is silent, then voiced on target, then silent again.</summary>
    private static (List<RecordedPoint> Flow, List<RecordedPoint> Volume) BuildSession(
        double goalRatio = 800, int intervalMs = 10)
    {
        var flow = new List<RecordedPoint>();
        var volume = new List<RecordedPoint>();

        for (int i = 0; i < 90; i++)
        {
            double t = i * intervalMs;
            bool voicing = i is >= 20 and < 65;
            double f = voicing ? 0.09 : 0.0;

            flow.Add(new RecordedPoint(t, f));
            volume.Add(new RecordedPoint(t, voicing ? goalRatio * f : 30.0));
        }

        return (flow, volume);
    }

    [Fact]
    public async Task ARecordedSessionPlaysThroughAndProducesATrialFile()
    {
        var settings = Settings(_directory);
        var clock = new StopwatchClock();
        var (flowPoints, volumePoints) = BuildSession(settings.GoalRatio);

        await using var loop = new GameLoop(
            settings,
            new ReplayFlowSource("flow", flowPoints, clock, loop: false),
            new ReplayVolumeSource("volume", volumePoints, clock, loop: false),
            clock,
            new GameEngine(settings) { SubjectId = "P01", SessionId = "replay" },
            new TrialRecorder(settings));

        var frames = new List<GameFrame>();
        var trials = new List<TrialResult>();
        var errors = new List<string>();

        using (loop.Frames.Subscribe(frames.Add))
        using (loop.CompletedTrials.Subscribe(trials.Add))
        using (loop.Errors.Subscribe(errors.Add))
        {
            await loop.StartAsync();
            await WaitUntil(() => trials.Count > 0, TimeSpan.FromSeconds(10));
            await loop.StopAsync();
        }

        Assert.Empty(errors);
        Assert.NotEmpty(frames);

        var trial = Assert.Single(trials);
        Assert.Equal("P01", trial.SubjectId);
        Assert.True(trial.DurationSeconds > 0.1,
            $"Expected a measurable trial length but got {trial.DurationSeconds}s.");

        // The recording holds the participant on the goal ratio, so most of it should score.
        Assert.True(trial.FractionInGoal > 0.5,
            $"Expected most of the trial in the goal but got {trial.FractionInGoal:P0}.");

        Assert.NotNull(trial.DataFile);
        Assert.True(File.Exists(trial.DataFile));
        Assert.True(File.Exists(Path.ChangeExtension(trial.DataFile, ".csv")));
    }

    [Fact]
    public async Task ASourceThatNeverDeliversIsReportedAsStaleRatherThanFreezing()
    {
        var settings = Settings(_directory);
        settings.StaleTicks = 2;

        var clock = new StopwatchClock();
        var (flowPoints, _) = BuildSession(settings.GoalRatio);

        // The microphone is present but silent: an empty recording never emits a sample.
        await using var loop = new GameLoop(
            settings,
            new ReplayFlowSource("flow", flowPoints, clock, loop: true),
            new ReplayVolumeSource("volume", [new RecordedPoint(0, 60)], clock, loop: false),
            clock);

        var frames = new List<GameFrame>();

        using (loop.Frames.Subscribe(frames.Add))
        {
            await loop.StartAsync();
            await WaitUntil(() => frames.Count > 20, TimeSpan.FromSeconds(5));
            await loop.StopAsync();
        }

        Assert.NotEmpty(frames);
        Assert.Contains(frames, f => f.SignalLost);
    }

    [Fact]
    public async Task StoppingTheLoopStopsTheDevices()
    {
        var settings = Settings(_directory);
        var clock = new StopwatchClock();
        var (flowPoints, volumePoints) = BuildSession(settings.GoalRatio);

        var flowSource = new ReplayFlowSource("flow", flowPoints, clock, loop: true);
        var statuses = new List<SourceStatus>();

        await using var loop = new GameLoop(
            settings, flowSource,
            new ReplayVolumeSource("volume", volumePoints, clock, loop: true),
            clock);

        using (flowSource.Status.Subscribe(statuses.Add))
        {
            await loop.StartAsync();
            await WaitUntil(() => statuses.Any(s => s.State == SourceState.Connected), TimeSpan.FromSeconds(5));
            await loop.StopAsync();
        }

        Assert.Equal(SourceState.Stopped, statuses[^1].State);
    }

    [Fact]
    public async Task AReplayFileMustMatchTheInputItIsAttachedTo()
    {
        var path = Path.Combine(_directory, "volume.jsonl");
        Directory.CreateDirectory(_directory);

        RecordedSignal.Save(path,
            new SignalHeader { Kind = SignalKind.Volume, Units = "dB SPL" },
            [new RecordedPoint(0, 60)]);

        var error = Assert.Throws<ReplayFormatException>(() =>
            ReplayFlowSource.FromFile(path, new StopwatchClock()));

        Assert.Contains("cannot drive the flow input", error.Message);
        await Task.CompletedTask;
    }

    private static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(20);
        }

        throw new TimeoutException($"The condition was not met within {timeout.TotalSeconds:F0}s.");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
