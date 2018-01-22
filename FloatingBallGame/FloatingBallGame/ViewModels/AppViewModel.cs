using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using FloatingBallGame.Annotations;
using FloatingBallGame.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

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

        public Settings AppSettings { get; set; }

        public JsonSerializerSettings JsonSettings { get; set; }

        public ObservableCollection<IGameWaveProvider> SampleProviders { get; set; }

        private AppViewModel()
        {
            this.Mode = AppMode.Loading;
            this.Config = new ConfigurationViewModel();
            this.Dialog = new DialogViewModel();
            this.Audio = new AudioProcessor();
            
            // Json contract resolver
            var contractResolver = new DefaultContractResolver {NamingStrategy = new SnakeCaseNamingStrategy()};
            this.JsonSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = contractResolver,
            };

            // Load the settings file
            try
            {
                this.AppSettings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText("app_settings.json"), this.JsonSettings);
            }
            catch (Exception e)
            {
                this.Dialog.ShowOkOnly("Error Loading Settings",
                    $"Error loading the application settings file 'app_settings.json', application will exit. Details: {e.Message}",
                    Application.Current.Shutdown,
                    new SolidColorBrush(Colors.LightCoral));
                return;
            }

            // Load any sample providers
            this.SampleProviders = new ObservableCollection<IGameWaveProvider>();
            foreach (var configData in this.AppSettings.SampleProviders ?? Enumerable.Empty<SampleProviderConfigData>())
            {
                try
                {
                    this.SampleProviders.Add(SampleGameWaveProvider.Create(configData));
                }
                catch (Exception e)
                {
                    this.Dialog.ShowOkOnly("Failed to load sample",
                        $"Error loading a sample wav file provider: {e.Message}",
                        null,
                        new SolidColorBrush(Colors.Yellow));
                }
            }
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