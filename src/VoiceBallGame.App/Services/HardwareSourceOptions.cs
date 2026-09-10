using System;
using System.Collections.Generic;
using System.Linq;
using VoiceBallGame.Core.Calibration;
using VoiceBallGame.Core.Configuration;
using VoiceBallGame.Core.Signals;
using VoiceBallGame.Hardware;

namespace VoiceBallGame.App.Services;

/// <summary>A flow meter connected through a serial port.</summary>
public sealed class SerialFlowOption : FlowSourceOption
{
    public SerialFlowOption(SerialPortInfo port)
        : base($"Flow meter on {port.PortName}", port.Description)
    {
        PortName = port.PortName;
    }

    public string PortName { get; }

    public override IFlowSource Create(IGameClock clock, GameSettings settings) =>
        new NicolayFlowSource(
            PortName,
            clock,
            TimeSpan.FromMilliseconds(settings.EngineTickMs),
            settings.FlowCorrectionFactor);
}

/// <summary>A microphone accessed through PortAudio.</summary>
public sealed class MicrophoneOption : VolumeSourceOption
{
    private readonly AudioInputDevice _device;
    private readonly CalibrationStore _calibrations;

    public MicrophoneOption(AudioInputDevice device, CalibrationStore calibrations)
        : base(device.Name, Describe(device, calibrations))
    {
        _device = device;
        _calibrations = calibrations;
    }

    public AudioInputDevice Device => _device;

    public MicCalibration? Calibration => _calibrations.Find(_device.Key);

    /// <summary>
    /// A microphone reports a signal level, not decibels, so without a calibration the game has no
    /// way to know what loudness the participant is producing.
    /// </summary>
    public override bool RequiresCalibration => Calibration is null;

    public override IVolumeSource Create(IGameClock clock, GameSettings settings) =>
        new PortAudioVolumeSource(
            _device.Index,
            _device.Name,
            settings.SampleRate,
            settings.BufferMs,
            clock,
            Calibration);

    /// <summary>Builds an uncalibrated source for the calibration screen to measure with.</summary>
    public PortAudioVolumeSource CreateForCalibration(IGameClock clock, GameSettings settings) =>
        new(_device.Index, _device.Name, settings.SampleRate, settings.BufferMs, clock, calibration: null);

    public void SaveCalibration(MicCalibration calibration) => _calibrations.Save(calibration);

    public string DeviceKey => _device.Key;

    private static string Describe(AudioInputDevice device, CalibrationStore calibrations)
    {
        var calibration = calibrations.Find(device.Key);

        return calibration is null
            ? "Not calibrated"
            : $"Calibrated {calibration.Created:yyyy-MM-dd} at {calibration.ReferenceDb:F0} dB SPL";
    }
}

/// <summary>Discovers the attached hardware for the setup screen.</summary>
public static class HardwareSources
{
    public static IEnumerable<FlowSourceOption> Flow() =>
        SerialPortCatalog.List().Select(port => new SerialFlowOption(port));

    public static IEnumerable<VolumeSourceOption> Volume(CalibrationStore calibrations) =>
        AudioDeviceCatalog.List().Select(device => new MicrophoneOption(device, calibrations));
}
