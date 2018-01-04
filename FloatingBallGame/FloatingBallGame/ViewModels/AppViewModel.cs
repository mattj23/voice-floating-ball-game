using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FloatingBallGame.Annotations;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace FloatingBallGame.ViewModels
{
    public class AppViewModel : INotifyPropertyChanged
    {
        private static AppViewModel _global;

        public static AppViewModel Global => _global ?? (_global = new AppViewModel());

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<string> DeviceNames { get; set; }

        private AppViewModel()
        {
            this.DeviceNames = new ObservableCollection<string>();
            int waveInDevices = WaveIn.DeviceCount;
            for (int waveInDevice = 0; waveInDevice < waveInDevices; waveInDevice++)
            {
                WaveInCapabilities deviceInfo = WaveIn.GetCapabilities(waveInDevice);
                this.DeviceNames.Add($"Device {waveInDevice}: {deviceInfo.ProductName}, {deviceInfo.Channels}");
            }

            return;
            MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.All))
            {
                this.DeviceNames.Add($"{device.FriendlyName}, {device.State}");
            }
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}