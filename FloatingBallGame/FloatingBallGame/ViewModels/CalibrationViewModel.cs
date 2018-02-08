using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FloatingBallGame.Annotations;
using FloatingBallGame.Audio;
using FloatingBallGame.Tools;
using NAudio.Wave;

namespace FloatingBallGame.ViewModels
{
    public class CalibrationViewModel : INotifyPropertyChanged
    {
        private bool _isRecording;
        private bool _isProcessing;
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
                this.Provider.StartRecording();
            }
        }

        public void SetPrecursor(IGameWavePrecursor precursor, MeasurementType deviceType)
        {
            // Unsubscribe
            if (this.Provider != null)
                this.Provider.DataAvailable -= ProviderOnDataAvailable;

            this.Precursor = precursor;
            this.Provider = precursor.ToProvider();
            this.Provider.DataAvailable += ProviderOnDataAvailable;

            this.DeviceType = deviceType;
        }

        private void ProviderOnDataAvailable(object sender, WaveInEventArgs e)
        {
            // We know that this is single channel audio

            int bytesPerSample = this.Provider.WaveFormat.BitsPerSample / 8;

            byte[] buffer = e.Buffer;
            int frameCount = buffer.Length / bytesPerSample;
            float[] floatBuffer = new float[frameCount];

            int bytesRecorded = e.BytesRecorded;
            WaveBuffer wbuffer = new WaveBuffer(buffer);

            double squareSum = 0;
            for (int i = 0; i < frameCount; i++)
            {
                int temp = (int)wbuffer.ShortBuffer[i];
                float value = temp * 0.000030517578125f;
                squareSum += value * value;
                floatBuffer[i] = value;
            }
            double rms = Math.Sqrt(squareSum / frameCount);


            /*
             *  if (waveFormat.BitsPerSample == 16)
                {
                    sampleFrame[channel] = BitConverter.ToInt16(raw, offset)/32768f;
                    offset += 2;
                }
                else if (waveFormat.BitsPerSample == 24)
                {
                    sampleFrame[channel] = (((sbyte)raw[offset + 2] << 16) | (raw[offset + 1] << 8) | raw[offset]) / 8388608f;
                    offset += 3;
                }
                else if (waveFormat.BitsPerSample == 32 && waveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                {
                    sampleFrame[channel] = BitConverter.ToSingle(raw, offset);
                    offset += 4;
                }
                else if (waveFormat.BitsPerSample == 32)
                {
                    sampleFrame[channel] = BitConverter.ToInt32(raw, offset) / (Int32.MaxValue + 1f);
                    offset += 4;
                }
                else
                {
                    throw new InvalidOperationException("Unsupported bit depth");
                }
                */
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
 