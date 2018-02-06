using System;
using FloatingBallGame.ViewModels;
using NAudio.Wave;

namespace FloatingBallGame.Audio
{
    public class WaveInPrecursor : IGameWavePrecursor
    {
        private WaveInCapabilities _capabilities;

        public WaveInPrecursor(WaveInCapabilities capabilities)
        {
            _capabilities = capabilities;
        }

        public string Name => _capabilities.ProductName;

        public Guid Id => _capabilities.NameGuid;

        public IGameWaveProvider ToProvider()
        {
            var provider = new DeviceGameWaveProvider(this.Name)
            {
                DeviceNumber = this.GetDeviceNumber(_capabilities),
                WaveFormat = new WaveFormat(AppViewModel.Global.AppSettings.SampleRate, 1),
                BufferMilliseconds = AppViewModel.Global.AppSettings.BufferMs
            };
            return provider;
        }

        private int GetDeviceNumber(WaveInCapabilities device)
        {
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                if (WaveIn.GetCapabilities(i).ProductGuid == device.ProductGuid)
                    return i;
            }
            throw new ArgumentException($"Device {device.ProductName} ({device.ProductGuid.ToString()}) not found in the current device list.");
        }
    }
}