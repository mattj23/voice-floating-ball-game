using System;
using NAudio.Wave;

namespace FloatingBallGame.Audio
{
    public class SampleProviderConfigData
    {
        public string Name { get; set; }

        public string CalibrationSample { get; set; }

        public string PlayingSample { get; set; }

        public Guid Id { get; set; }

        public WaveFileReader CalibrationReader() => new WaveFileReader(this.CalibrationSample);

        public WaveFileReader PlayingReader() => new WaveFileReader(this.PlayingSample);
    }
}