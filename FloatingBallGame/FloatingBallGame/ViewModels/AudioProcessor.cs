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

        /// <summary>
        /// The provider for the volume data, raises an event when data is ready
        /// </summary>
        private IGameWaveProvider _volumeProvider;

        /// <summary>
        /// The provider for the flow data, raises an event when the data is ready 
        /// </summary>
        private IGameWaveProvider _flowProvider;

        private WaveFormat _flowFormat;
        private WaveFormat _volumeFormat;

        /// <summary>
        /// Volume conversion function which takes into account the saved calibration data for the device
        /// </summary>
        private Func<double, double> _volumeConvert;

        /// <summary>
        /// Flow conversion function which takes into account the saved calibration data for the device
        /// </summary>
        private Func<double, double> _flowConvert;

        /// <summary>
        /// The moving average for flow, computed from the history window size
        /// </summary>
        private double _flowAverage;

        /// <summary>
        /// The moving average for volume, computed from the history window size
        /// </summary>
        private double _volumeAverage;

        /// <summary>
        /// Backing field for the flow, do not access directly
        /// </summary>
        private double _flow;

        /// <summary>
        /// Backing field for volume, do not access directly
        /// </summary>
        private double _volume;

        private Stopwatch _stopwatch;

        /// <summary>
        /// Backing field for the ball height; do not access directly
        /// </summary>
        private double _ball;

        private ApplicationSettings _settings;
        private double _upperGoal;
        private double _lowerGoal;
        private double _goalCenter;
        private double _goalHeight;
        private bool _isInTrial;
        private DateTime _trialStart;

        private long _lastClock;
        private double _instantaneousPhase;

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

        /// <summary>
        /// The position of the ball, updating this property updates the visual ball 
        /// position in the view.
        /// </summary>
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

        /// <summary>
        /// Currently unused 
        /// </summary>
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

        /// <summary>
        /// Currently unused
        /// </summary>
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

        /// <summary>
        /// Gets the height of the goal rectangle
        /// </summary>
        public double GoalHeight
        {
            get => _goalHeight;
            private set
            {
                if (value.Equals(_goalHeight)) return;
                _goalHeight = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets the center position of the goal rectangle
        /// </summary>
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

        /// <summary>
        /// Contains the history of samples taken during the course of the trial, written to the trial
        /// log and wiped clean at the end of each trial.
        /// </summary>
        public List<TestSample> Samples { get; set; }

        /// <summary>
        /// Gets the state flag that indicates whether or not the audio processer currently identifies as being 
        /// in an underway trial, used by the view to update itself accordingly
        /// </summary>
        public bool IsInTrial
        {
            get => _isInTrial;
            private set
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

        /// <summary>
        /// Is the last n (HistoryWindow) samples of flow 
        /// </summary>
        public FixedListContainer<double> FlowHistory;

        /// <summary>
        /// Is the last n (HistoryWindow) samples of acceleration (volume)
        /// </summary>
        public FixedListContainer<double> VolumeHistory;

        public FixedListContainer<double> TrialStartFlowHistory;

        private bool _isFlowOutOfLimits;

        public AudioProcessor(ApplicationSettings settings)
        {
            _settings = settings;
            _stopwatch = new Stopwatch();
            Samples = new List<TestSample>();
            FlowHistory = new FixedListContainer<double>(settings.HistoryWindow);
            VolumeHistory = new FixedListContainer<double>(settings.HistoryWindow);
            TrialStartFlowHistory = new FixedListContainer<double>(settings.TrialStartWindow);
        }

        public void Configure()
        {
            // Unregister the volume and flow providers from the data available handlers.  This is
            // to prevent multiple subscriptions if the providers change.
            if (_volumeProvider != null)
            {
                _volumeProvider.DataAvailable -= VolumeProviderOnDataAvailable;
            }
            if (_flowProvider != null)
            {
                _flowProvider.DataAvailable -= FlowProviderOnDataAvailable;
            }

            // Register the new volume device as the volume data provider
            _volumeProvider = AppViewModel.Global.Config.VolumeDevice.ToProvider();

            // From the calibration data, assemble the volume conversion fuction _volumeConvert
            // 20 * log10 (V_noise / V_ref) + dB_ref
            double voltageRef = AppViewModel.Global.Config.VolumeCalibration.Measured.First();
            double dbRef = AppViewModel.Global.Config.VolumeCalibration.Actual.First();
            _volumeConvert = d => 20 * Math.Log10(d / voltageRef) + dbRef;

            // Register the new flow device as the flow data provider
            _flowProvider = AppViewModel.Global.Config.FlowDevice.ToProvider();

            // From the calibration data, assemble the flow conversion fuction _flowConvert
            double[] ys = AppViewModel.Global.Config.FlowCalibration.Actual.ToArray();
            double[] xs = AppViewModel.Global.Config.FlowCalibration.Measured.ToArray();
            double m = (ys.First() - ys.Last()) / (xs.First() - xs.Last());
            // y - k = m (x - h).
            double b = m * (0 - xs.First()) + ys.First();
            _flowConvert = d => m * d + b;

            // Register the data available event handlers
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
            _lastClock = 0;
            _instantaneousPhase = 0;
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

        /// <summary>
        /// Event handler raised when flow data is made available.  Adds the flow data to the floating average window, then invokes
        /// the UpdateBallPosition method.  This method is also responsible for starting and stopping the trials when the flow raises
        /// or falls above or below trial start/stop thresholds.  
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlowProviderOnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (_flowFormat == null)
                _flowFormat = _flowProvider.WaveFormat;

            // Sample the provided data and convert to a flow value and store it in the main object flow property
            // where it is accessible to other methods and objects
            this.Flow = _flowConvert.Invoke(Processing.RmsValue(e.Buffer, _flowFormat));
            this.FlowHistory.Add(this.Flow);
            this.TrialStartFlowHistory.Add(this.Flow);

            // When the flowhistory is full we can start taking the moving average, using it to determine whether or not
            // we should start or stop a trial, and deciding whether or not to update the ball position
            if (TrialStartFlowHistory.IsFull)
            {
                // The trial start is computed off of the trial start history
                double trialStartFlow = this.TrialStartFlowHistory.Contents.Average();

                // The actual flow average value is computed off of the flow history, which may be a different 
                // length than the trial start/stop history
                _flowAverage = this.FlowHistory.Contents.Average();

                // If we're in a trial and the flow average drops below the trial start threshold we can end the current trial 
                if (IsInTrial && trialStartFlow < AppViewModel.Global.AppSettings.TrialStartThreshold)
                {
                    StopTrial();
                }
                // Otherwise if we're not in a trial but the flow average has ascended above the trial start threshold, we can begin the trial
                else if (!IsInTrial && trialStartFlow > AppViewModel.Global.AppSettings.TrialStartThreshold)
                {
                    StartTrial();
                }
            }

            this.UpdateBallPosition();
        }

        /// <summary>
        /// Event handler raised when volume data is made available.  Adds the volume data to the floating average window, then
        /// places the computed average in the private field for the update ball position method to use.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VolumeProviderOnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (_volumeFormat == null)
                _volumeFormat = _volumeProvider.WaveFormat;

            // Sample the provided data and convert it to a decibel value by the calibrated conversion function.  Then add
            // it to the floating averaging window collection.
            this.Volume = _volumeConvert.Invoke(Processing.RmsValue(e.Buffer, _volumeFormat));
            this.VolumeHistory.Add(this.Volume);

            // Compute and store the floating average.
            _volumeAverage = this.VolumeHistory.Contents.Average();
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
            // The volume and flow averages are already computed as _flowAverage and _volumeAverage
            var acc =  _volumeAverage;
            var flow = _flowAverage;

            /*
               % Ball color will be determined based on SCORE.
               % SCORE quantifies how far the flow/acc ratio is from 0.8.
               % SCORE should not go beyond the range [-15 15]
               % since this is directly used for the color look up table.
               rawSCORE = (acc - flow*0.8)/0.1*50;
               if rawSCORE>=0,
               SCORE = min([rawSCORE,15]);
               else
               SCORE = max([rawSCORE,-15]);
               end
            */
            double rawScore = (acc - flow * _settings.GoalFlow) / 0.1 * 50;
            double score = rawScore;
            if (score > 15)
                score = 15;
            if (score < -15)
                score = -15;

            double ACC = 3 * acc;

            double freq = acc * 1 / 0.036;

            // How much time has elapsed since the last update
            double elapsed = (_stopwatch.ElapsedMilliseconds - _lastClock) / 1000.0;
            _lastClock = _stopwatch.ElapsedMilliseconds;

            //double iPhase = iPhase + 2 * Math.PI * freq / 60;
            // instantaneous phase
            _instantaneousPhase += 2 * Math.PI * freq * elapsed;

            double ballCenter = 20 * flow - 1.25;
            // -1 because the graphics canvas is inverted
            this.Ball = -1 * (ballCenter * _settings.GraphicsScale); // (ballCenter + ACC * Math.Cos(_instantaneousPhase)) * _settings.GraphicsScale;

            double goalHalfHeight = 2.4 * flow;
            double goalCenter = ballCenter;

            if (flow < 0.08)
            {
                goalHalfHeight = 0.192;
                goalCenter = 1.6 - 1.25;
            }

            if (flow > 0.1)
            {
                goalHalfHeight = 0.24;
                goalCenter = 2 - 1.25;
            }

            this.GoalCenter = -1 * goalCenter * _settings.GraphicsScale;
            this.GoalHeight = (2 * goalHalfHeight) * _settings.GraphicsScale + AppViewModel.Global.AppSettings.BallSize;
            
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
 