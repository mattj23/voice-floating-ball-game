﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using FloatingBallGame.Annotations;
using FloatingBallGame.Audio;
using FloatingBallGame.Tools;
using NAudio.Wave;

namespace FloatingBallGame.ViewModels
{
    public class AudioProcessor : INotifyPropertyChanged
    {
        private IGameWaveProvider _volumeProvider;
        private IGameWaveProvider _flowProvider;
        private WaveFormat _flowFormat;
        private WaveFormat _volumeFormat;

        private Func<double, double> _volumeConvert;
        private Func<double, double> _flowConvert;

        private double _flow;
        private double _volume;

        private Stopwatch _stopwatch;
        private double _ball;

        public double Volume
        {
            get => _volume;
            private set
            {
                if (value.Equals(_volume)) return;
                _volume = value;
                OnPropertyChanged();
            }
        }

        public double Flow
        {
            get => _flow;
            private set
            {
                if (value.Equals(_flow)) return;
                _flow = value;
                OnPropertyChanged();
            }
        }

        public double Ball
        {
            get => _ball;
            private set
            {
                if (value.Equals(_ball)) return;
                _ball = value;
                OnPropertyChanged();
            }
        }

        public AudioProcessor()
        {
            _stopwatch = new Stopwatch();
        }

        public void Configure()
        {
            if (_volumeProvider != null)
            {
                _volumeProvider.DataAvailable -= VolumeProviderOnDataAvailable;
            }
            if (_flowProvider != null)
            {
                _flowProvider.DataAvailable -= FlowProviderOnDataAvailable;
            }

            _volumeProvider = AppViewModel.Global.Config.VolumeDevice.ToProvider();

            // 20 * log10 (V_noise / V_ref) + dB_ref
            double voltageRef = AppViewModel.Global.Config.VolumeCalibration.Measured.First();
            double dbRef = AppViewModel.Global.Config.VolumeCalibration.Actual.First();
            _volumeConvert = d => 20 * Math.Log10(d / voltageRef) + dbRef;

            _flowProvider = AppViewModel.Global.Config.FlowDevice.ToProvider();

            double[] ys = AppViewModel.Global.Config.FlowCalibration.Actual.ToArray();
            double[] xs = AppViewModel.Global.Config.FlowCalibration.Measured.ToArray();
            double m = (ys.First() - ys.Last()) / (xs.First() - xs.Last());
            // y - k = m (x - h).
            double b = m * (0 - xs.First()) + ys.First();
            _flowConvert = d => m * d + b;

            _volumeProvider.DataAvailable += VolumeProviderOnDataAvailable;
            _flowProvider.DataAvailable += FlowProviderOnDataAvailable;

            _flowFormat = null;
            _volumeFormat = null;

            _volumeProvider.SetMode(WaveMode.Playing);
            _flowProvider.SetMode(WaveMode.Playing);

            _volumeProvider.StartRecording();
            _flowProvider.StartRecording();
            _stopwatch.Reset();
            _stopwatch.Start();

        }

        private void FlowProviderOnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (_flowFormat == null)
                _flowFormat = _flowProvider.WaveFormat;

            this.Flow = _flowConvert.Invoke(Processing.RmsValue(e.Buffer, _flowFormat));
            this.UpdateBallPosition();
        }

        private void VolumeProviderOnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (_volumeFormat == null)
                _volumeFormat = _volumeProvider.WaveFormat;

            this.Volume = _volumeConvert.Invoke(Processing.RmsValue(e.Buffer, _volumeFormat));
            this.UpdateBallPosition();
        }

        private void UpdateBallPosition()
        {
            // 10*(flow-0.03) + 3*acc*cos(2*pi*2.0*i/60);

            double i = _stopwatch.ElapsedMilliseconds / 1000.0;
            this.Ball = 100 * Math.Cos(2.0 * Math.PI * i);

            if (i >= 1.0)
            {
                _stopwatch.Restart();
            }





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