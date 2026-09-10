namespace VoiceBallGame.Core.Signals;

/// <summary>
/// A source of timestamped readings. Implementations deliver on whatever thread their underlying
/// device uses, so subscribers are responsible for marshaling notifications to the UI.
/// </summary>
public interface ISignalSource<out T> : IAsyncDisposable
{
    IObservable<T> Samples { get; }

    IObservable<SourceStatus> Status { get; }

    /// <summary>A name suitable for showing the operator which device is in use.</summary>
    string Description { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();
}

public interface IFlowSource : ISignalSource<FlowSample>;

public interface IVolumeSource : ISignalSource<VolumeSample>
{
    /// <summary>
    /// The uncalibrated RMS of each buffer, used by the calibration screen where no calibration
    /// exists yet. Runs alongside <see cref="ISignalSource{T}.Samples"/> from the same capture.
    /// </summary>
    IObservable<double> RawLevels { get; }
}
