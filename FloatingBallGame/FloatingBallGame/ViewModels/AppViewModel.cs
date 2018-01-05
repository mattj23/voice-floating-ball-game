using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using FloatingBallGame.Annotations;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace FloatingBallGame.ViewModels
{
    public class AppViewModel : INotifyPropertyChanged
    {
        private static AppViewModel _global;
        private AppMode _mode;

        public static AppViewModel Global => _global ?? (_global = new AppViewModel());

        public event PropertyChangedEventHandler PropertyChanged;

        public AppMode Mode
        {
            get { return _mode; }
            set
            {
                if (value == _mode) return;
                _mode = value;
                OnPropertyChanged();
            }
        }

        public AudioProcessor Audio { get; set; }
        public ConfigurationViewModel Config { get; set; }
        public DialogViewModel Dialog { get; set; }

        private AppViewModel()
        {
            this.Mode = AppMode.Loading;
            this.Config = new ConfigurationViewModel();
            this.Dialog = new DialogViewModel();
            this.Audio = new AudioProcessor();
            
        }

        public void ConfigureAndStart()
        {
            try
            {
                this.Mode = AppMode.Playing;
                this.Audio.Configure(this.Config.VolumeDevice, this.Config.FlowDevice);
            }
            catch (Exception e)
            {
                this.Dialog.ShowOkOnly("Error on configuration", $"An error occured on device configuration: {e.Message}", 
                    () =>
                    {
                        AppViewModel.Global.Mode = AppMode.Loading;
                    },
                    new SolidColorBrush(Colors.LightCoral));
            }
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}