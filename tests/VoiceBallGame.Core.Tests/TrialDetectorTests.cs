using VoiceBallGame.Core.Configuration;
using VoiceBallGame.Core.Engine;

namespace VoiceBallGame.Core.Tests;

public class TrialDetectorTests
{
    private static GameSettings Settings() => new()
    {
        TrialStartWindow = 5,
        TrialStartThreshold = 0.001,
    };

    private static TrialTransition Feed(TrialDetector detector, double flow, int frames)
    {
        var last = TrialTransition.None;
        for (int i = 0; i < frames; i++)
        {
            var transition = detector.Update(flow);
            if (transition != TrialTransition.None) last = transition;
        }

        return last;
    }

    [Fact]
    public void NoTrialStartsBeforeTheWindowFills()
    {
        var detector = new TrialDetector(Settings());

        for (int i = 0; i < 4; i++)
            Assert.Equal(TrialTransition.None, detector.Update(0.09));

        Assert.False(detector.IsInTrial);

        Assert.Equal(TrialTransition.Started, detector.Update(0.09));
        Assert.True(detector.IsInTrial);
    }

    [Fact]
    public void VoicingStartsATrialAndSilenceEndsIt()
    {
        var detector = new TrialDetector(Settings());

        Assert.Equal(TrialTransition.Started, Feed(detector, 0.09, 5));
        Assert.True(detector.IsInTrial);

        // The window has to refill with silence before the trial closes, which is what stops a
        // single dropped frame from cutting a trial short.
        Assert.Equal(TrialTransition.Ended, Feed(detector, 0.0, 5));
        Assert.False(detector.IsInTrial);
    }

    [Fact]
    public void ASingleQuietFrameDoesNotEndATrial()
    {
        var detector = new TrialDetector(Settings());
        Feed(detector, 0.09, 5);

        Assert.Equal(TrialTransition.None, detector.Update(0.0));
        Assert.True(detector.IsInTrial);
    }

    [Fact]
    public void OnlyOneStartIsReportedForContinuousVoicing()
    {
        var detector = new TrialDetector(Settings());
        Feed(detector, 0.09, 5);

        for (int i = 0; i < 20; i++)
            Assert.Equal(TrialTransition.None, detector.Update(0.09));

        Assert.True(detector.IsInTrial);
    }

    [Fact]
    public void ALostSignalEndsAnOpenTrial()
    {
        var detector = new TrialDetector(Settings());
        Feed(detector, 0.09, 5);

        Assert.Equal(TrialTransition.Ended, detector.Update(0.09, signalValid: false));
        Assert.False(detector.IsInTrial);
    }

    [Fact]
    public void ALostSignalCannotStartATrial()
    {
        var detector = new TrialDetector(Settings());

        for (int i = 0; i < 10; i++)
            Assert.Equal(TrialTransition.None, detector.Update(0.09, signalValid: false));

        Assert.False(detector.IsInTrial);
    }

    [Fact]
    public void MultipleTrialsCanRunInSequence()
    {
        var detector = new TrialDetector(Settings());

        for (int trial = 0; trial < 3; trial++)
        {
            Assert.Equal(TrialTransition.Started, Feed(detector, 0.09, 5));
            Assert.Equal(TrialTransition.Ended, Feed(detector, 0.0, 5));
        }
    }

    [Fact]
    public void ResetClosesAnOpenTrialWithoutReportingATransition()
    {
        var detector = new TrialDetector(Settings());
        Feed(detector, 0.09, 5);

        detector.Reset();

        Assert.False(detector.IsInTrial);
        Assert.Equal(0.0, detector.DetectionFlow);
    }
}
