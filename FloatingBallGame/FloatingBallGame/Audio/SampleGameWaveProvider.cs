using System;
using System.Windows.Threading;
using NAudio.Wave;

namespace FloatingBallGame.Audio
{
    public class SampleGameWaveProvider : IGameWaveProvider, IGameWavePrecursor
    {
        private WaveFileReader _calibrationReader;
        private WaveFileReader _playingReader;

        private DispatcherTimer _timer;
        private ApplicationSettings _settings;
        private int _bytesPerBuffer = 0;
        private byte[] _data;
        private WaveFileReader _activeReader;

        public SampleGameWaveProvider(SampleProviderConfigData configData, ApplicationSettings settings) :
            this(configData.Name, configData.ConfigReader(), configData.PlayingReader(), configData.Id, settings)
        {

        }

        public SampleGameWaveProvider(string name, WaveFileReader calibrationReader, WaveFileReader playingReader, Guid id, ApplicationSettings settings)
        {
            this.Name = name;
            _calibrationReader = calibrationReader;
            _playingReader = playingReader;
            this.Id = id;

            _settings = settings;
            _timer = new DispatcherTimer {Interval = TimeSpan.FromMilliseconds(_settings.BufferMs)};
            _timer.Tick += TimerOnTick;
        }

        private void TimerOnTick(object sender, EventArgs eventArgs)
        {
            var bytesRead = _activeReader.Read(_data, 0, _bytesPerBuffer);
            if (bytesRead == 0)
                this.StopRecording();

            var output = new WaveInEventArgs(_data, bytesRead);
            this.DataAvailable?.Invoke(this, output);
        }

        public void Dispose()
        {
            ;
        }

        public void StartRecording()
        {
 
            // Load the byte array
            if (this.Mode == WaveMode.Calibration)
                _activeReader = _calibrationReader;
            else
                _activeReader = _playingReader;

            this.WaveFormat = _activeReader.WaveFormat;
            _bytesPerBuffer = WaveFormat.AverageBytesPerSecond * _settings.BufferMs / 1000;
            _data = new byte[_bytesPerBuffer];

            _timer.Start();
        }

        public void StopRecording()
        {
            _timer.Stop();
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

        public WaveMode Mode { get; private set; }
        public void SetMode(WaveMode mode)
        {
            this.Mode = mode;
        }
    }
}