using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}