using System.Reactive.Linq;
using System.Reactive.Subjects;
using NicolaySerialSFM3x00;
using VoiceBallGame.Core.Signals;

namespace VoiceBallGame.Hardware;

/// <summary>
/// Reads flow from a Sensirion SFM3x00 through a Nicolay serial connector.
/// </summary>
/// <remarks>
/// <para>
/// The device streams at roughly 1100 packets per second, far faster than the game's 20 Hz tick,
/// and unread packets accumulate. Rather than hand every packet to the game or take one sample per
/// tick and discard the rest, this source averages all packets that arrive within each output
/// interval and emits their mean. Averaging before decimation suppresses noise and prevents
/// aliasing into the frequency band used by the game.
/// </para>
/// <para>
/// The device reports standard liters per minute. The game works in liters per second, so readings
/// are divided by 60 and then multiplied by the configured correction factor. Use that factor for
/// a BTPS correction if the protocol requires one.
/// </para>
/// </remarks>
public sealed class NicolayFlowSource : IFlowSource
{
    private readonly string _portName;
    private readonly byte _address;
    private readonly IGameClock _clock;
    private readonly TimeSpan _outputInterval;
    private readonly double _scale;

    private readonly Subject<FlowSample> _samples = new();
    private readonly BehaviorSubject<SourceStatus> _status = new(SourceStatus.Stopped);

    private SfmDevice? _device;
    private CancellationTokenSource? _cancellation;
    private Task? _pump;

    /// <param name="correctionFactor">
    /// Applied after converting standard liters per minute to liters per second.
    /// </param>
    public NicolayFlowSource(
        string portName,
        IGameClock clock,
        TimeSpan outputInterval,
        double correctionFactor = 1.0,
        byte address = 1)
    {
        _portName = portName;
        _clock = clock;
        _outputInterval = outputInterval > TimeSpan.Zero ? outputInterval : TimeSpan.FromMilliseconds(50);
        _scale = correctionFactor / 60.0;
        _address = address;
    }

    public string Description => $"SFM3x00 on {_portName}";

    public IObservable<FlowSample> Samples => _samples.AsObservable();

    public IObservable<SourceStatus> Status => _status.AsObservable();

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_pump is not null) return Task.CompletedTask;

        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _status.OnNext(SourceStatus.Connecting);
        _pump = Task.Run(() => PumpAsync(_cancellation.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    private async Task PumpAsync(CancellationToken token)
    {
        try
        {
            _device = new SfmDevice(_portName, _address);
            await _device.Connect().ConfigureAwait(false);

            if (!await _device.Check().ConfigureAwait(false))
            {
                _status.OnNext(SourceStatus.Faulted(
                    $"The flow meter on {_portName} did not pass its self test."));
                return;
            }

            _status.OnNext(SourceStatus.Connected);
            await ConsumeStreamAsync(_device, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stopping is not a fault.
        }
        catch (SfmException e)
        {
            _status.OnNext(SourceStatus.Faulted($"The flow meter on {_portName} stopped responding: {e.Message}"));
            return;
        }
        catch (Exception e)
        {
            _status.OnNext(SourceStatus.Faulted($"The flow meter on {_portName} could not be opened: {e.Message}"));
            return;
        }

        _status.OnNext(SourceStatus.Stopped);
    }

    private async Task ConsumeStreamAsync(SfmDevice device, CancellationToken token)
    {
        var windowEnd = _clock.Elapsed + _outputInterval;
        double sum = 0;
        int count = 0;
        int unreadable = 0;

        await foreach (var packet in device.StreamAsync(token).ConfigureAwait(false))
        {
            if (packet.FlowSlm is { } slm)
            {
                sum += slm;
                count++;
            }
            else
            {
                unreadable++;
            }

            var now = _clock.Elapsed;
            if (now < windowEnd) continue;

            if (count > 0)
            {
                _samples.OnNext(new FlowSample(now, sum / count * _scale));
            }
            else if (unreadable > 0)
            {
                // Every packet in this window came back unreadable. Emitting nothing lets the
                // game loop's staleness check report a lost signal, which is what the operator
                // needs to see rather than a flatlined zero that looks like silence.
                _status.OnNext(SourceStatus.Faulted(
                    $"The flow sensor on {_portName} could not be read. It may need a hardware reset."));
            }

            sum = 0;
            count = 0;
            unreadable = 0;

            // Advance to the next window boundary, skipping any the consumer fell behind on so
            // output does not burst to catch up.
            do
            {
                windowEnd += _outputInterval;
            } while (windowEnd <= now);
        }
    }

    public async Task StopAsync()
    {
        if (_cancellation is null) return;

        await _cancellation.CancelAsync().ConfigureAwait(false);

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

        if (_device is not null)
        {
            await _device.DisposeAsync().ConfigureAwait(false);
            _device = null;
        }

        _cancellation.Dispose();
        _cancellation = null;
        _status.OnNext(SourceStatus.Stopped);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _samples.Dispose();
        _status.Dispose();
        GC.SuppressFinalize(this);
    }
}
