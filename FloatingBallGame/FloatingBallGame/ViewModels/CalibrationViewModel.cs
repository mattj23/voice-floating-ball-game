using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;
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
        private double _mode1Value;
        private double _mode2Value;
        private double _mode3Value;
        public event PropertyChangedEventHandler PropertyChanged;

        public MeasurementType DeviceType { get; private set; }
        public IGameWavePrecursor Precursor { get; private set; }
        public IGameWaveProvider Provider { get; private set; }
        public WaveformViewModel WaveForm { get; }

        public double Mode1Value
        {
            get { return _mode1Value; }
            set
            {
                if (value.Equals(_mode1Value)) return;
                _mode1Value = value;
                OnPropertyChanged();
            }
        }

        public double Mode2Value
        {
            get { return _mode2Value; }
            set
            {
                if (value.Equals(_mode2Value)) return;
                _mode2Value = value;
                OnPropertyChanged();
            }
        }

        public double Mode3Value
        {
            get { return _mode3Value; }
            set
            {
                if (value.Equals(_mode3Value)) return;
                _mode3Value = value;
                OnPropertyChanged();
            }
        }

        public DelegateCommand ToggleRecordCommand { get; }
        public DelegateCommand AcceptCalibrationCommand { get; }
        public DelegateCommand CancelCalibrationCommand { get; }

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
            this.CancelCalibrationCommand = new DelegateCommand(CancelCalibration);
            this.AcceptCalibrationCommand = new DelegateCommand(AcceptCalibration);

            this.IsRecording = false;
            this.IsProcessing = false;
            this.WaveForm = new WaveformViewModel();
        }

        private void AcceptCalibration(object o)
        {
            var calibration = new CalibrationData
            {
                Id = Precursor.Id,
                Type = DeviceType,
                Measured = new List<double> { WaveForm.Mode1, WaveForm.Mode2, WaveForm.Mode3},
                Actual = new List<double> { this.Mode1Value, this.Mode2Value, this.Mode3Value},
                Created = DateTime.Now
            };

            AppViewModel.Global.SavedCalibrations.Add(calibration);
            AppViewModel.Global.SavedCalibrations.Serialize();
            
            this.Clear();
            AppViewModel.Global.Mode = AppMode.Loading;
        }

        private void CancelCalibration(object o)
        {
            this.Clear();
            AppViewModel.Global.Mode = AppMode.Loading;
        }

        private void Clear()
        {
            if (this.Provider != null)
            {
                this.Provider.RecordingStopped -= ProviderOnRecordingStopped;
                this.Provider.DataAvailable -= ProviderOnDataAvailable;
                this.Provider = null;
            }
            this.Precursor = null;
            this.IsProcessed = false;
            this.IsRecording = false;
            this.WaveForm.Clear();
        }

        private void ToggleRecording(object o)
        {
            this.IsProcessed = false;
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

            var sampleValue = Processing.RmsValue(e.Buffer, _waveFormat);
            this.WaveForm.AddSample(_tick, sampleValue);
        }

        private void ProcessData()
        {
            try
            {
                this.IsProcessing = true;
                this.WaveForm.Process(this.DeviceType);
                this.IsProcessed = true;
            }
            catch (Exception e)
            {
                AppViewModel.Global.Dialog.ShowOkOnly("Error processing waveform", "Error extracting calibration signals from waveform, try again please.", null, new SolidColorBrush(Colors.LightCoral));
            }
            finally
            {
                this.IsProcessing = false;
            }
            
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
 