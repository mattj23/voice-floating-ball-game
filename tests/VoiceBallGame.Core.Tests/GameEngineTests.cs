using VoiceBallGame.Core.Configuration;
using VoiceBallGame.Core.Engine;
using VoiceBallGame.Core.Signals;

namespace VoiceBallGame.Core.Tests;

public class GameEngineTests
{
    private const double TickSeconds = 0.05;

    private static GameSettings Settings() => new();

    /// <summary>Runs frames at the engine tick rate, returning every frame produced.</summary>
    private static List<GameFrame> Run(GameEngine engine, int frames, double flow, double spl,
        ref TimeSpan clock)
    {
        var produced = new List<GameFrame>(frames);
        for (int i = 0; i < frames; i++)
        {
            clock += TimeSpan.FromSeconds(TickSeconds);
            produced.Add(engine.Step(new GameInput(clock, flow, spl)));
        }

        return produced;
    }

    [Fact]
    public void ATrialOpensOnVoicingAndClosesOnSilence()
    {
        var engine = new GameEngine(Settings());
        var clock = TimeSpan.Zero;

        var voicing = Run(engine, 40, flow: 0.09, spl: 72.0, ref clock);
        Assert.Contains(voicing, f => f.TrialStarted);
        Assert.True(engine.IsInTrial);

        var silence = Run(engine, 40, flow: 0.0, spl: 30.0, ref clock);
        var ending = silence.Single(f => f.CompletedTrial is not null);

        Assert.False(engine.IsInTrial);
        Assert.Equal(1, ending.CompletedTrial!.TrialNumber);
        Assert.True(ending.CompletedTrial.DurationSeconds > 0);
        Assert.NotEmpty(ending.CompletedSamples!);
    }

    [Fact]
    public void SamplesAreOnlyRecordedDuringATrial()
    {
        var engine = new GameEngine(Settings());
        var clock = TimeSpan.Zero;

        Run(engine, 20, flow: 0.0, spl: 30.0, ref clock);
        Run(engine, 30, flow: 0.09, spl: 72.0, ref clock);
        var silence = Run(engine, 30, flow: 0.0, spl: 30.0, ref clock);

        var samples = silence.Single(f => f.CompletedTrial is not null).CompletedSamples!;

        Assert.All(samples, s => Assert.True(s.Time >= 0));
        Assert.Equal(0.0, samples[0].Time, 9);
    }

    [Fact]
    public void AnOnTargetTrialScoresNearlyZeroError()
    {
        var engine = new GameEngine(Settings());
        var clock = TimeSpan.Zero;

        // 0.09 L/s at 72 dB is exactly the goal ratio of 800.
        Run(engine, 60, flow: 0.09, spl: 72.0, ref clock);
        var silence = Run(engine, 40, flow: 0.0, spl: 30.0, ref clock);
        var result = silence.Single(f => f.CompletedTrial is not null).CompletedTrial!;

        Assert.True(result.FractionInGoal > 0.9,
            $"Expected most of the trial in the goal but got {result.FractionInGoal:P0}.");
        Assert.True(result.AverageRatioError < 0.05,
            $"Expected a small ratio error but got {result.AverageRatioError}.");
        Assert.Equal(1, result.GoalEntryCount);
    }

    [Fact]
    public void TrialsAreNumberedInSequence()
    {
        var engine = new GameEngine(Settings());
        var clock = TimeSpan.Zero;
        var numbers = new List<int>();

        for (int i = 0; i < 3; i++)
        {
            Run(engine, 30, flow: 0.09, spl: 72.0, ref clock);
            var silence = Run(engine, 30, flow: 0.0, spl: 30.0, ref clock);
            numbers.Add(silence.Single(f => f.CompletedTrial is not null).CompletedTrial!.TrialNumber);
        }

        Assert.Equal([1, 2, 3], numbers);
        Assert.Equal(3, engine.CompletedTrialCount);
    }

    [Fact]
    public void ASingleFrameTrialSummarizesSafely()
    {
        // The original called Last() and Average() on the sample list with no emptiness guard and
        // divided by the trial length without checking it, so the shortest possible trials were
        // the ones most likely to take the game down. A trial of one frame has zero length, which
        // is the case that has to not divide by zero.
        var settings = Settings();
        settings.TrialStartWindow = 1;
        var engine = new GameEngine(settings);

        var clock = TimeSpan.FromSeconds(1);
        engine.Step(new GameInput(clock, 0.09, 72.0));

        clock += TimeSpan.FromSeconds(TickSeconds);
        var frame = engine.Step(new GameInput(clock, 0.09, 72.0, FlowStale: true));

        Assert.NotNull(frame.CompletedTrial);
        Assert.Equal(1, frame.CompletedTrial!.SampleCount);
        Assert.Equal(0.0, frame.CompletedTrial.DurationSeconds);
        Assert.Equal(0.0, frame.CompletedTrial.SecondsInGoal);
        Assert.Equal(0.0, frame.CompletedTrial.FractionInGoal);
        Assert.False(double.IsNaN(frame.CompletedTrial.AverageError));
    }

    [Fact]
    public void FlowOutsideTheLimitsTurnsTheBallGrayAndSuppressesRatioFeedback()
    {
        var engine = new GameEngine(Settings());
        var clock = TimeSpan.Zero;

        var frames = Run(engine, 40, flow: 0.30, spl: 90.0, ref clock);
        var frame = frames[^1];

        Assert.True(frame.FlowOutOfLimits);
        Assert.Equal(FlowBoundState.AboveLimit, frame.FlowState);
        Assert.Equal(engine.Colors.OutOfLimitsColor, frame.BallColor);
    }

    [Fact]
    public void TheRecordedGoalBandMatchesTheBoxThatIsDrawn()
    {
        // The original recorded the box center plus and minus its full height rather than half of
        // it, so the band stored in the trial file was twice as tall as the one on screen.
        var engine = new GameEngine(Settings());
        var clock = TimeSpan.Zero;

        Run(engine, 40, flow: 0.09, spl: 72.0, ref clock);
        var silence = Run(engine, 40, flow: 0.0, spl: 30.0, ref clock);
        var samples = silence.Single(f => f.CompletedTrial is not null).CompletedSamples!;

        var sample = samples[^1];
        Assert.True(sample.GoalUpper > sample.GoalLower);

        // The band's height is the goal box height for the flow that produced it, which the box
        // takes from the flow clamped to the configured limits.
        var settings = Settings();
        double boundedFlow = Math.Clamp(sample.Flow, settings.LowerFlowLimit, settings.UpperFlowLimit);
        double expectedHeight = 2 * settings.GoalHalfHeightFactor * boundedFlow * settings.GraphicsScale
                                + settings.BallSize;
        Assert.Equal(expectedHeight, sample.GoalUpper - sample.GoalLower, 6);
    }

    [Fact]
    public void FlowIsFlooredSoTheRatioStaysFinite()
    {
        var engine = new GameEngine(Settings());
        var clock = TimeSpan.Zero;

        var frames = Run(engine, 20, flow: 0.0, spl: 50.0, ref clock);
        var frame = frames[^1];

        Assert.Equal(GameEngine.MinimumFlowLps, frame.Flow, 9);
        Assert.False(double.IsInfinity(frame.Score.RatioFraction));
        Assert.False(double.IsNaN(frame.Score.RatioFraction));
    }

    [Fact]
    public void FramesReportWhenTheSmoothingWindowIsStillFilling()
    {
        var settings = Settings();
        var engine = new GameEngine(settings);
        var clock = TimeSpan.Zero;

        var frames = Run(engine, settings.HistoryWindow + 2, flow: 0.09, spl: 72.0, ref clock);

        Assert.False(frames[0].IsWarmedUp);
        Assert.True(frames[^1].IsWarmedUp);
    }

    [Fact]
    public void AStaleSignalIsReportedOnTheFrame()
    {
        var engine = new GameEngine(Settings());

        var frame = engine.Step(new GameInput(TimeSpan.FromSeconds(1), 0.09, 72.0, FlowStale: true));

        Assert.True(frame.SignalLost);
    }

    [Fact]
    public void ResetClearsTheTrialAndTheSmoothingWindows()
    {
        var engine = new GameEngine(Settings());
        var clock = TimeSpan.Zero;
        Run(engine, 40, flow: 0.09, spl: 72.0, ref clock);

        engine.Reset();

        Assert.False(engine.IsInTrial);

        clock += TimeSpan.FromSeconds(TickSeconds);
        var frame = engine.Step(new GameInput(clock, 0.09, 72.0));
        Assert.False(frame.IsWarmedUp);
    }

    [Fact]
    public void SubjectAndSessionAreStampedOntoEachTrial()
    {
        var engine = new GameEngine(Settings()) { SubjectId = "P07", SessionId = "baseline" };
        var clock = TimeSpan.Zero;

        Run(engine, 30, flow: 0.09, spl: 72.0, ref clock);
        var silence = Run(engine, 30, flow: 0.0, spl: 30.0, ref clock);
        var result = silence.Single(f => f.CompletedTrial is not null).CompletedTrial!;

        Assert.Equal("P07", result.SubjectId);
        Assert.Equal("baseline", result.SessionId);
    }

    [Fact]
    public void TheSameInputsAlwaysProduceTheSameFrames()
    {
        // Determinism is what allows a recorded session to be replayed and compared frame for
        // frame, so it is worth pinning down.
        static List<double> RunOnce()
        {
            var engine = new GameEngine(Settings());
            var clock = TimeSpan.Zero;
            var positions = new List<double>();

            for (int i = 0; i < 100; i++)
            {
                clock += TimeSpan.FromSeconds(TickSeconds);
                double flow = 0.09 + 0.01 * Math.Sin(i / 5.0);
                double spl = 72.0 + 3.0 * Math.Cos(i / 7.0);
                positions.Add(engine.Step(new GameInput(clock, flow, spl)).Ball.BallCenter);
            }

            return positions;
        }

        Assert.Equal(RunOnce(), RunOnce());
    }
}
