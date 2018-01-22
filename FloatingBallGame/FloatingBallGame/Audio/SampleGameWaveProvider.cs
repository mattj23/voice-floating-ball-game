using System;
using NAudio.Wave;

namespace FloatingBallGame.Audio
{
    public class SampleGameWaveProvider : IGameWaveProvider
    {
        /// <summary>
        /// Create a 
        /// </summary>
        /// <param name="configData"></param>
        /// <returns></returns>
        public static SampleGameWaveProvider Create(SampleProviderConfigData configData)
        {
            var cProvider = new WaveFileReader(configData.ConfigSample);
            var pProvider = new WaveFileReader(configData.PlayingSample);
            return new SampleGameWaveProvider(configData.Name, cProvider, pProvider);
        }

        private IWaveProvider _calibrationProvider;
        private IWaveProvider _playingProvider;


        public SampleGameWaveProvider(string name, IWaveProvider calibrationProvider, IWaveProvider playingProvider)
        {
            this.Name = name;
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