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

        private void ProviderOnDataAvailable(object sender, WaveInEventArgs waveInEventArgs)
        {
            throw new NotImplementedException();
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}