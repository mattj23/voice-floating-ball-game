using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Windows.Threading;
using FloatingBallGame.Annotations;
using FloatingBallGame.Audio;
using NAudio.Wave;

namespace FloatingBallGame.ViewModels
{
    public class ProviderSelectionViewModel : INotifyPropertyChanged
    {
        private IGameWavePrecursor _volumeDevice;
        private IGameWavePrecursor _flowDevice;
        private CalibrationData _volumeCalibration;
        private CalibrationData _flowCalibration;

        public IGameWavePrecursor VolumeDevice
        {
            get => _volumeDevice;
            set
            {
                if (value.Equals(_volumeDevice)) return;
                _volumeDevice = value;
                OnPropertyChanged();
            }
        }

        public IGameWavePrecursor FlowDevice
        {
            get => _flowDevice;
            set
            {
                if (value.Equals(_flowDevice)) return;
                _flowDevice = value;
                OnPropertyChanged();
            }
        }

        public CalibrationData VolumeCalibration
        {
            get { return _volumeCalibration; }
            private set
            {
                if (Equals(value, _volumeCalibration)) return;
                _volumeCalibration = value;
                OnPropertyChanged();
            }
        }

        public CalibrationData FlowCalibration
        {
            get { return _flowCalibration; }
            private set
            {
                if (Equals(value, _flowCalibration)) return;
                _flowCalibration = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<IGameWavePrecursor> Devices { get; set; }

        public ProviderSelectionViewModel()
        {
            this.Devices = new ObservableCollection<IGameWavePrecursor>();

            var checkTimer = new DispatcherTimer {Interval = TimeSpan.FromSeconds(1)};
            checkTimer.Tick += CheckTimerOnTick;
            checkTimer.Start();
        }

        /// <summary>
        /// Detect WaveIn devices and wrap the in a precursor object
        /// </summary>
        /// <returns></returns>
        private List<IGameWavePrecursor> DetectDevices()
        {
            var detectedDevices = new List<IGameWavePrecursor>();
            
            int waveInDevices = WaveIn.DeviceCount;
            for (int waveInDevice = 0; waveInDevice < waveInDevices; waveInDevice++)
            {
                detectedDevices.Add(new WaveInPrecursor(WaveIn.GetCapabilities(waveInDevice)));
            }
            return detectedDevices;
        }

        private void CheckTimerOnTick(object sender, EventArgs eventArgs)
        {
            if (AppViewModel.Global.Mode != AppMode.Loading)
                return;

            var available = this.DetectDevices();
            available.AddRange(AppViewModel.Global.SampleProviders.Select(x => x as IGameWavePrecursor));

            // Add any that aren't already present
            foreach (var device in available)
            {
                if (this.Devices.All(x => x.Id != device.Id))
                    this.Devices.Add(device);
            }

            // Remove any that are missing
            var removeList = new List<IGameWavePrecursor>();
            foreach (var device in this.Devices)
            {
                if (available.All(x => x.Id != device.Id))
                    removeList.Add(device);
            }
            foreach (var device in removeList)
            {
                this.Devices.Remove(device);
            }

            // Assign devices
            if (this.VolumeDevice == null && this.Devices.Any())
                this.VolumeDevice = this.Devices.First();
            if (this.FlowDevice == null && this.Devices.Any())
                this.FlowDevice = this.Devices.First();

            if (this.VolumeDevice != null)
                this.VolumeCalibration = CheckForCalibration(VolumeDevice, MeasurementType.Volume);
            else
                this.VolumeCalibration = null;

            if (this.FlowDevice != null)
                this.FlowCalibration = CheckForCalibration(FlowDevice, MeasurementType.Flow);
            else
                this.FlowCalibration = null;
        }


        private CalibrationData CheckForCalibration(IGameWavePrecursor precursor, MeasurementType measurement)
        {
            var calibration =
                AppViewModel.Global.SavedCalibrations.Data.FirstOrDefault(x =>
                    x.Id == precursor.Id && x.Type == measurement);
            return calibration;
        }


        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}