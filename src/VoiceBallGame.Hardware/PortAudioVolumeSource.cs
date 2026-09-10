using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Channels;
using PortAudioSharp;
using VoiceBallGame.Core.Calibration;
using VoiceBallGame.Core.Dsp;
using VoiceBallGame.Core.Signals;
using Stream = PortAudioSharp.Stream;

namespace VoiceBallGame.Hardware;

/// <summary>
/// Captures the microphone through PortAudio and reports loudness in dB SPL.
/// </summary>
/// <remarks>
/// PortAudio calls back on its own high-priority audio thread. That callback computes the level of
/// the buffer, which is a few thousand multiplications and allocates nothing, and hands the single
/// resulting number to a channel. All other work, including notifying subscribers, happens on a
/// task reading that channel, so a slow subscriber can never stall audio capture.
/// </remarks>
public sealed class PortAudioVolumeSource : IVolumeSource
{
    private readonly int _deviceIndex;
    private readonly string _deviceName;
    private readonly int _sampleRate;
    private readonly int _bufferMs;
    private readonly IGameClock _clock;
    private readonly MicCalibration? _calibration;

    private readonly Subject<VolumeSample> _samples = new();
    private readonly Subject<double> _rawLevels = new();
    private readonly BehaviorSubject<SourceStatus> _status = new(SourceStatus.Stopped);
    private readonly Channel<double> _levels =
        Channel.CreateBounded<double>(new BoundedChannelOptions(256)
        {
            // If the reader falls behind, the newest level is the one worth keeping.
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true,
        });

    private Stream? _stream;
    private Stream.Callback? _callback;
    private CancellationTokenSource? _cancellation;
    private Task? _pump;

    /// <param name="calibration">
    /// Used to convert measured level to dB SPL. Without one the source still reports raw levels,
    /// which is what the calibration screen needs.
    /// </param>
    public PortAudioVolumeSource(
        int deviceIndex,
        string deviceName,
        int sampleRate,
        int bufferMs,
        IGameClock clock,
        MicCalibration? calibration)
    {
        _deviceIndex = deviceIndex;
        _deviceName = deviceName;
        _sampleRate = sampleRate;
        _bufferMs = bufferMs;
        _clock = clock;
        _calibration = calibration;
    }

    public string Description => _deviceName;

    public IObservable<VolumeSample> Samples => _samples.AsObservable();

    /// <summary>Uncalibrated buffer levels, used while calibrating.</summary>
    public IObservable<double> RawLevels => _rawLevels.AsObservable();

    public IObservable<SourceStatus> Status => _status.AsObservable();

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_stream is not null) return Task.CompletedTask;

        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _status.OnNext(SourceStatus.Connecting);

        try
        {
            PortAudioRuntime.Acquire();

            var parameters = new StreamParameters
            {
                device = _deviceIndex,
                channelCount = 1,
                sampleFormat = SampleFormat.Float32,
                suggestedLatency = PortAudio.GetDeviceInfo(_deviceIndex).defaultLowInputLatency,
            };

            uint framesPerBuffer = (uint)Math.Max(1, _sampleRate * _bufferMs / 1000);

            // The delegate is held in a field: PortAudio keeps an unmanaged pointer to it, and a
            // collected delegate would crash the audio thread rather than fail here.
            _callback = OnAudio;

            _stream = new Stream(
                inParams: parameters,
                outParams: null,
                sampleRate: _sampleRate,
                framesPerBuffer: framesPerBuffer,
                streamFlags: StreamFlags.ClipOff,
                callback: _callback,
                userData: IntPtr.Zero);

            _stream.Start();
        }
        catch (Exception e)
        {
            _status.OnNext(SourceStatus.Faulted($"The microphone '{_deviceName}' could not be opened: {e.Message}"));
            Cleanup();
            return Task.CompletedTask;
        }

        _status.OnNext(SourceStatus.Connected);
        _pump = Task.Run(() => PumpAsync(_cancellation.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    private unsafe StreamCallbackResult OnAudio(
        IntPtr input,
        IntPtr output,
        uint frameCount,
        ref StreamCallbackTimeInfo timeInfo,
        StreamCallbackFlags statusFlags,
        IntPtr userData)
    {
        if (input == IntPtr.Zero) return StreamCallbackResult.Continue;

        var buffer = new ReadOnlySpan<float>((void*)input, (int)frameCount);
        _levels.Writer.TryWrite(SignalMath.Rms(buffer));

        return StreamCallbackResult.Continue;
    }

    private async Task PumpAsync(CancellationToken token)
    {
        try
        {
            await foreach (var level in _levels.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                var now = _clock.Elapsed;
                _rawLevels.OnNext(level);

                if (_calibration is not null)
                    _samples.OnNext(new VolumeSample(now, _calibration.ToDecibels(level)));
            }
        }
        catch (OperationCanceledException)
        {
            // Stopping is not a fault.
        }
    }

    public async Task StopAsync()
    {
        if (_cancellation is not null) await _cancellation.CancelAsync().ConfigureAwait(false);

        if (_pump is not null)
        {
            try
            {
                await _pump.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping.
            }

            _pump = null;
        }

        Cleanup();
        _status.OnNext(SourceStatus.Stopped);
    }

    private void Cleanup()
    {
        if (_stream is not null)
        {
            try
            {
                if (!_stream.IsStopped) _stream.Stop();
                _stream.Close();
            }
            catch (Exception)
            {
                // The device may already be gone; there is nothing useful to do about it here.
            }

            _stream.Dispose();
            _stream = null;
            _callback = null;
            PortAudioRuntime.Release();
        }

        _cancellation?.Dispose();
        _cancellation = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _samples.Dispose();
        _rawLevels.Dispose();
        _status.Dispose();
        GC.SuppressFinalize(this);
    }
}
