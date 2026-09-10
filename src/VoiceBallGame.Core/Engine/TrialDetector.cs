using VoiceBallGame.Core.Configuration;
using VoiceBallGame.Core.Dsp;

namespace VoiceBallGame.Core.Engine;

public enum TrialTransition
{
    None,
    Started,
    Ended,
}

/// <summary>
/// Decides when a trial is underway. Trials are not started by a button: the participant simply
/// begins voicing, and flow rising above the threshold opens a trial while falling back below it
/// closes one. The threshold is applied to its own short average, separate from the longer window
/// the game itself uses, so that trial boundaries respond faster than the ball does.
/// </summary>
public sealed class TrialDetector
{
    private readonly MovingAverage _window;
    private readonly double _threshold;

    public TrialDetector(GameSettings settings)
    {
        _window = new MovingAverage(settings.TrialStartWindow);
        _threshold = settings.TrialStartThreshold;
    }

    public bool IsInTrial { get; private set; }

    /// <summary>The short-window flow average the decision is made on.</summary>
    public double DetectionFlow => _window.Average;

    /// <summary>
    /// Feeds one frame of flow and reports whether a trial boundary was crossed.
    /// </summary>
    /// <param name="flowLps">Flow for this frame in L/s.</param>
    /// <param name="signalValid">
    /// False when the flow signal is stale or the device has faulted. An invalid signal cannot
    /// start a trial, and ends one that is underway rather than leaving it open indefinitely.
    /// </param>
    public TrialTransition Update(double flowLps, bool signalValid = true)
    {
        if (!signalValid)
        {
            _window.Clear();
            if (!IsInTrial) return TrialTransition.None;
            IsInTrial = false;
            return TrialTransition.Ended;
        }

        _window.Add(flowLps);

        // Wait until the window is full so a trial boundary is never decided from a partial
        // average, which would otherwise trigger on the first frame after the game starts.
        if (!_window.IsFull) return TrialTransition.None;

        if (IsInTrial && _window.Average < _threshold)
        {
            IsInTrial = false;
            return TrialTransition.Ended;
        }

        if (!IsInTrial && _window.Average > _threshold)
        {
            IsInTrial = true;
            return TrialTransition.Started;
        }

        return TrialTransition.None;
    }

    public void Reset()
    {
        _window.Clear();
        IsInTrial = false;
    }
}
