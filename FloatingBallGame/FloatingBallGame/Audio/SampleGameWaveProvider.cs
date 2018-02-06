using System;
using NAudio.Wave;

namespace FloatingBallGame.Audio
{
    public class SampleGameWaveProvider : IGameWaveProvider, IGameWavePrecursor
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
            return new SampleGameWaveProvider(configData.Name, cProvider, pProvider, configData.Id);
        }

        private IWaveProvider _calibrationProvider;
        private IWaveProvider _playingProvider;


        public SampleGameWaveProvider(string name, IWaveProvider calibrationProvider, IWaveProvider playingProvider, Guid id)
        {
            this.Name = name;
            _calibrationProvider = calibrationProvider;
            _playingProvider = playingProvider;
            this.Id = id;
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

        public string Name { get; }

        public Guid Id { get; }

        public IGameWaveProvider ToProvider()
        {
            return this;
        }

        public WaveMode Mode { get; set; }
    }
}