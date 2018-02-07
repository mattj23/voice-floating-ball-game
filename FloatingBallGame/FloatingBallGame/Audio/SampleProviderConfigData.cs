using System;
using NAudio.Wave;

namespace FloatingBallGame.Audio
{
    public class SampleProviderConfigData
    {
        public string Name { get; set; }

        public string ConfigSample { get; set; }

        public string PlayingSample { get; set; }

        public Guid Id { get; set; }

        public WaveFileReader ConfigReader() => new WaveFileReader(this.ConfigSample);

        public WaveFileReader PlayingReader() => new WaveFileReader(this.PlayingSample);
    }
}