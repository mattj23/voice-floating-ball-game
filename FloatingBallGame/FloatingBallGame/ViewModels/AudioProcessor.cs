using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FloatingBallGame.Annotations;
using NAudio.Wave;

namespace FloatingBallGame.ViewModels
{
    public class AudioProcessor : INotifyPropertyChanged
    {
        private WaveIn _volumeWaveIn;
        private WaveIn _flowWaveIn;
        private double _flow;
        private double _volume;

        public double Volume
        {
            get { return _volume; }
            set
            {
                if (value.Equals(_volume)) return;
                _volume = value;
                OnPropertyChanged();
            }
        }

        public double Flow
        {
            get { return _flow; }
            set
            {
                if (value.Equals(_flow)) return;
                _flow = value;
                OnPropertyChanged();
            }
        }

        public AudioProcessor()
        {
            
        }

        public void Configure(WaveInCapabilities volumeDevice, WaveInCapabilities flowDevice)
        {
            _volumeWaveIn = new WaveIn
            {
                DeviceNumber = GetDeviceNumber(volumeDevice),
                WaveFormat = new WaveFormat(22050, 1),
                BufferMilliseconds = 50
            };

            _flowWaveIn = new WaveIn
            {
                DeviceNumber = GetDeviceNumber(flowDevice),
                WaveFormat = new WaveFormat(22050, 1),
                BufferMilliseconds = 100
            };

            _volumeWaveIn.DataAvailable += VolumeWaveInOnDataAvailable;
            _flowWaveIn.DataAvailable += FlowWaveInOnDataAvailable;

            _volumeWaveIn.StartRecording();
            _flowWaveIn.StartRecording();
        }

        private void FlowWaveInOnDataAvailable(object sender, WaveInEventArgs e)
        {
            byte[] buffer = e.Buffer;
            int frameCount = buffer.Length / 2;
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

            this.Flow = rms; // rms * 100;
        }

        private void VolumeWaveInOnDataAvailable(object sender, WaveInEventArgs e)
        {
            byte[] buffer = e.Buffer;
            int frameCount = buffer.Length / 2;
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

            this.Volume = rms;
            // this.Volume = 20 * Math.Log10(Volume);
        }

        private int GetDeviceNumber(WaveInCapabilities device)
        {
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                if (WaveIn.GetCapabilities(i).ProductGuid == device.ProductGuid)
                    return i;
            }
            throw new ArgumentException($"Device {device.ProductName} ({device.ProductGuid.ToString()}) not found in the current device list.");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}