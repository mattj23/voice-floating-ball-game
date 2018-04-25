using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using FloatingBallGame.Annotations;
using FloatingBallGame.Audio;
using FloatingBallGame.Tools;
using NAudio.Wave;
using Newtonsoft.Json;

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

        private ApplicationSettings _settings;
        private double _upperGoal;
        private double _lowerGoal;
        private double _goalCenter;
        private double _goalHeight;
        private bool _isInTrial;
        private DateTime _trialStart;

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
                OnPropertyChanged(nameof(IsFlowOutOfLimits));
            }
        }

        public bool IsFlowOutOfLimits
        {
            get => _isFlowOutOfLimits;
            set
            {
                if (value == _isFlowOutOfLimits) return;
                _isFlowOutOfLimits = value;
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

        public double UpperGoal
        {
            get => _upperGoal;
            set
            {
                if (value.Equals(_upperGoal)) return;
                _upperGoal = value;
                OnPropertyChanged();
            }
        }

        public double LowerGoal
        {
            get => _lowerGoal;
            set
            {
                if (value.Equals(_lowerGoal)) return;
                _lowerGoal = value;

                OnPropertyChanged();
            }
        }

        public double GoalHeight
        {
            get => _goalHeight;
            set
            {
                if (value.Equals(_goalHeight)) return;
                _goalHeight = value;
                OnPropertyChanged();
            }
        }

        public double GoalCenter
        {
            get => _goalCenter;
            set
            {
                if (value.Equals(_goalCenter)) return;
                _goalCenter = value;
                OnPropertyChanged();
            }
        }

        public List<TestSample> Samples { get; set; }

        public bool IsInTrial
        {
            get => _isInTrial;
            set
            {
                if (value == _isInTrial) return;
                _isInTrial = value;
                OnPropertyChanged();
            }
        }

        public DateTime TrialStart
        {
            get => _trialStart;
            set
            {
                if (value.Equals(_trialStart)) return;
                _trialStart = value;
                OnPropertyChanged();
            }
        }

        public FixedListContainer<double> FlowHistory;
        private bool _isFlowOutOfLimits;

        public AudioProcessor(ApplicationSettings settings)
        {
            _settings = settings;
            _stopwatch = new Stopwatch();
            Samples = new List<TestSample>();
            FlowHistory = new FixedListContainer<double>(5);
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

        private void StartTrial()
        {
            IsInTrial = true;
            TrialStart = DateTime.Now;
            Samples.Clear();
        }

        private void StopTrial()
        {
            IsInTrial = false;
            File.WriteAllText($"trial {TrialStart:yyyy-MM-dd-hh-mm-ss}.json", JsonConvert.SerializeObject(Samples, Formatting.Indented));
            Samples.Clear();
        }

        private void FlowProviderOnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (_flowFormat == null)
                _flowFormat = _flowProvider.WaveFormat;

            this.Flow = _flowConvert.Invoke(Processing.RmsValue(e.Buffer, _flowFormat));
            this.FlowHistory.Add(this.Flow);

            if (FlowHistory.IsFull)
            {
                var contentAverage = this.FlowHistory.Contents.Average();
                if (IsInTrial && contentAverage < AppViewModel.Global.AppSettings.TrialStartThreshold)
                {
                    StopTrial();
                }
                else if (!IsInTrial && contentAverage > AppViewModel.Global.AppSettings.TrialStartThreshold)
                {
                    StartTrial();
                }

            }

            this.UpdateBallPosition();
        }

        private void VolumeProviderOnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (_volumeFormat == null)
                _volumeFormat = _volumeProvider.WaveFormat;

            this.Volume = _volumeConvert.Invoke(Processing.RmsValue(e.Buffer, _volumeFormat));
            // this.UpdateBallPosition();
        }

        private double Ln(double x) => Math.Log(x, Math.E);
        private double Sq(double x) => x * x;

        private double LimitFlow(double flow)
        {
            if (flow < _settings.LowerFlowLimit)
            {
                IsFlowOutOfLimits = true;
                return _settings.LowerFlowLimit;
            }

            if (flow > _settings.UpperFlowLimit)
            {
                IsFlowOutOfLimits = true;
                return _settings.UpperFlowLimit;
            }

            IsFlowOutOfLimits = false;
            return flow;
        }

        private void UpdateBallPosition()
        {
            /*
             * Updated 4/9/2018
             * F = 20*(0.1-flow);
               noise = 1/10*1./(F)*exp(-1*(log((F))-log(0.27)).^2./(2*0.7.^2))...
               *exp(-1*(acc-0.8*flow).^2./(2*0.01.^2));
               
               
               centerbar = 10*(flow-0.03);
               
               goalbar = 3*flow*0.8;
               
               upperbar = centerbar + goalbar;
               lowerbar = centerbar - goalbar;
               
               
               flow = mean(data(i-11:i,1));
               acc = mean(data(i-11:i,2));
               
               
               ACC = 3*acc*(1+(0.5-noise));
               
               ball(i) = 10*(flow-0.03) + ACC*cos(2*pi*2.0*i/60);
               
             */

            /* Noise stuff 
            var F = 20 * (_settings.UpperFlowLimit - this.Flow);
            var noise = 1 / 10 * 1/F * Math.Exp(-1 * Sq(Ln(F) - Ln(0.27)) / (2 * Sq(0.7))) * Math.Exp(-1 * Sq(this.Volume - 0.8 * this.Flow) / (2 * 0.01 * 0.01));

            // Ball position
            var ACC = _settings.Amplitude * this.Volume * (1 + (0.5 - noise)); */

            var ACC = _settings.Amplitude * this.Volume; // Non-noise version
            double i = _stopwatch.ElapsedMilliseconds / 1000.0;
            this.Ball = _settings.FlowScale * (this.Flow + _settings.FlowOffset) + ACC * Math.Cos(_settings.Frequency * Math.PI * i);

            // Error bar position
            // centerbar = 10 * (flow - 0.03);
            // goalbar = 3 * flow * 0.8;
            // upperbar = centerbar + goalbar;
            // lowerbar = centerbar - goalbar;
            double cappedFlow = LimitFlow(this.Flow);

            double center = _settings.FlowScale * (cappedFlow + _settings.FlowOffset);
            GoalCenter = center;
            double goal = _settings.Amplitude * cappedFlow * _settings.ErrorBarRatio;
            if (goal < 0)
                goal = 0;
            this.UpperGoal = center + goal;
            this.LowerGoal = center - goal;
            this.GoalHeight = 2 * goal + AppViewModel.Global.AppSettings.BallSize;

            if (i >= 1.0)
            {
                _stopwatch.Restart();
            }

            if (IsInTrial)
            {
                var sample = new TestSample
                {
                    Volume = this.Volume,
                    Flow = this.Flow,
                    Time = (DateTime.Now - TrialStart).TotalSeconds
                };

                this.Samples.Add(sample);
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