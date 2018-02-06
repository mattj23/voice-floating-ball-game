using System;
using NAudio.Wave;

namespace FloatingBallGame.Audio
{
    /// <summary>
    /// A WaveIn wrapped inside a common interface.
    /// </summary>
    public class DeviceGameWaveProvider : WaveIn, IGameWaveProvider
    {
        public DeviceGameWaveProvider(string name) : base()
        {
            this.Name = name;
        }

        

        public string Name { get; }

        public WaveMode Mode { get; set; }
    }
}