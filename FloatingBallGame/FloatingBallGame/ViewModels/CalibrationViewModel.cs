using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using FloatingBallGame.Annotations;
using FloatingBallGame.Audio;
using FloatingBallGame.Tools;
using NAudio.Wave;

namespace FloatingBallGame.ViewModels
{
    public class CalibrationViewModel : INotifyPropertyChanged
    {
        private List<double> _sampleBuffer = new List<double>();
        private bool _isRecording;
        private bool _isProcessing;
        private int _tick;
        private WaveFormat _waveFormat;
        private bool _isProcessed;
        public event PropertyChangedEventHandler PropertyChanged;

        public MeasurementType DeviceType { get; private set; }
        public IGameWavePrecursor Precursor { get; private set; }
        public IGameWaveProvider Provider { get; private set; }
        public WaveformViewModel WaveForm { get; }

        
        public DelegateCommand ToggleRecordCommand { get; }

        public bool IsRecording
        {
            get => _isRecording;
            private set
            {
                if (value == _isRecording) return;
                _isRecording = value;
                OnPropertyChanged();
            }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            private set
            {
                if (value == _isProcessing) return;
                _isProcessing = value;
                OnPropertyChanged();
            }
        }

        public bool IsProcessed
        {
            get { return _isProcessed; }
            private set
            {
                if (value == _isProcessed) return;
                _isProcessed = value;
                OnPropertyChanged();
            }
        }

        public CalibrationViewModel()
        {
            this.ToggleRecordCommand = new DelegateCommand(ToggleRecording, o => !this.IsProcessing);
            this.IsRecording = false;
            this.IsProcessing = false;
            this.WaveForm = new WaveformViewModel();
        }

        private void ToggleRecording(object o)
        {
            if (this.IsRecording)
            {
                // stop recording and start processing
                this.Provider.StopRecording();
            }
            else
            {
                // Start recording
                this.WaveForm.Clear();
                this.Provider.SetMode(WaveMode.Calibration);
                this.IsRecording = true;
                _waveFormat = null;
                _tick = 0;
                this.Provider.StartRecording();
            }
        }

        public void SetPrecursor(IGameWavePrecursor precursor, MeasurementType deviceType)
        {
            // Unsubscribe
            if (this.Provider != null)
            {
                this.Provider.RecordingStopped -= ProviderOnRecordingStopped;
                this.Provider.DataAvailable -= ProviderOnDataAvailable;
            }

            this.Precursor = precursor;
            this.Provider = precursor.ToProvider();
            this.Provider.DataAvailable += ProviderOnDataAvailable;
            this.Provider.RecordingStopped += ProviderOnRecordingStopped;

            this.DeviceType = deviceType;
        }

        private void ProviderOnRecordingStopped(object sender, StoppedEventArgs stoppedEventArgs)
        {
            this.IsRecording = false;
            if (this.WaveForm.Values.Count > 3000 / AppViewModel.Global.AppSettings.BufferMs)
            {
                this.ProcessData();
            }
        }

        private void ProviderOnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (_waveFormat == null)
                _waveFormat = this.Provider.WaveFormat;

            // We know that this is single channel audio

            int bytesPerSample = _waveFormat.BitsPerSample / 8;

            byte[] buffer = e.Buffer;
            int sampleCount = buffer.Length / bytesPerSample;
            float[] sampleBuffer = new float[sampleCount];

            int offset = 0;
            int count = 0;
            while (count < sampleCount)
            {
                if (_waveFormat.BitsPerSample == 16)
                {
                    sampleBuffer[count] = BitConverter.ToInt16(buffer, offset) / 32768f;
                    offset += 2;
                }
                else if (_waveFormat.BitsPerSample == 24)
                {
                    sampleBuffer[count] = (((sbyte)buffer[offset + 2] << 16) | (buffer[offset + 1] << 8) | buffer[offset]) / 8388608f;
                    offset += 3;
                }
                else if (_waveFormat.BitsPerSample == 32 && _waveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                {
                    sampleBuffer[count] = BitConverter.ToSingle(buffer, offset);
                    offset += 4;
                }
                else if (_waveFormat.BitsPerSample == 32)
                {
                    sampleBuffer[count] = BitConverter.ToInt32(buffer, offset) / (Int32.MaxValue + 1f);
                    offset += 4;
                }
                else
                {
                    throw new InvalidOperationException("Unsupported bit depth");
                }
                count++;
            }

            _tick += AppViewModel.Global.AppSettings.BufferMs;
            var sampleValue = sampleBuffer.Max();
            this.WaveForm.AddSample(_tick, sampleValue);
        }

        private void ProcessData()
        {
            this.WaveForm.Process(this.DeviceType);
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
 