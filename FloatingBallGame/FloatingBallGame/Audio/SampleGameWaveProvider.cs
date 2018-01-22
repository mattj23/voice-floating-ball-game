using System;
using NAudio.Wave;

namespace FloatingBallGame.Audio
{
    public class SampleGameWaveProvider : IGameWaveProvider
    {
        private IWaveProvider _calibrationProvider;
        private IWaveProvider _playingProvider;


        public SampleGameWaveProvider(IWaveProvider calibrationProvider, IWaveProvider playingProvider)
        {
            _calibrationProvider = calibrationProvider;
            _playingProvider = playingProvider;
        }

        public void Dispose()
        {
            ;
        }

        public void StartRecording()
        {
            throw new NotImplementedException();

        }

        public void StopRecording()
        {
            this.RecordingStopped?.Invoke(this, new StoppedEventArgs());
        }

        public WaveFormat WaveFormat { get; set; }
        public event EventHandler<WaveInEventArgs> DataAvailable;
        public event EventHandler<StoppedEventArgs> RecordingStopped;

        public string Name { get; set; }

        public WaveMode Mode { get; set; }
    }
}