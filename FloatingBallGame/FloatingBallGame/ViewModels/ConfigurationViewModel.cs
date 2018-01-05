using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using FloatingBallGame.Annotations;
using NAudio.Wave;

namespace FloatingBallGame.ViewModels
{
    public class ConfigurationViewModel : INotifyPropertyChanged
    {
        private WaveInCapabilities _volumeDevice;
        private WaveInCapabilities _flowDevice;

        public WaveInCapabilities VolumeDevice
        {
            get => _volumeDevice;
            set
            {
                if (value.Equals(_volumeDevice)) return;
                _volumeDevice = value;
                OnPropertyChanged();
            }
        }

        public WaveInCapabilities FlowDevice
        {
            get => _flowDevice;
            set
            {
                if (value.Equals(_flowDevice)) return;
                _flowDevice = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<WaveInCapabilities> Devices { get; set; }

        public ConfigurationViewModel()
        {
            this.Devices = new ObservableCollection<WaveInCapabilities>();

            var checkTimer = new DispatcherTimer {Interval = TimeSpan.FromSeconds(1)};
            checkTimer.Tick += CheckTimerOnTick;
            checkTimer.Start();
        }

        private void CheckTimerOnTick(object sender, EventArgs eventArgs)
        {
            if (AppViewModel.Global.Mode != AppMode.Loading)
                return;

            var newDevices = new List<WaveInCapabilities>();

            // Get all attached devices
            int waveInDevices = WaveIn.DeviceCount;
            for (int waveInDevice = 0; waveInDevice < waveInDevices; waveInDevice++)
            {
                newDevices.Add(WaveIn.GetCapabilities(waveInDevice));
            }

            // Add any that aren't already present
            foreach (var device in newDevices)
            {
                if (this.Devices.All(x => x.ProductGuid != device.ProductGuid))
                    this.Devices.Add(device);
            }

            // Remove any that are missing
            var removeList = new List<WaveInCapabilities>();
            foreach (var device in this.Devices)
            {
                if (newDevices.All(x => x.ProductGuid != device.ProductGuid))
                    removeList.Add(device);
            }
            foreach (var device in removeList)
            {
                this.Devices.Remove(device);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}