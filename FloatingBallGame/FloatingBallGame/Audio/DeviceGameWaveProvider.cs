using System;
using NAudio.Wave;

namespace FloatingBallGame.Audio
{
    public class DeviceGameWaveProvider : WaveIn, IGameWaveProvider
    {
        public string Name { get; set; }

        public WaveMode Mode { get; set; }
    }
}