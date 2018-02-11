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
            get => _mode;
            set
            {
                if (value == _mode) return;
                _mode = value;
                OnPropertyChanged();
            }
        }

        public CalibrationViewModel Calibration { get; }

        public AudioProcessor Audio { get; set; }
        public ProviderSelectionViewModel Config { get; set; }
        public DialogViewModel Dialog { get; set; }

        public ApplicationSettings AppSettings { get; set; }

        public JsonSerializerSettings JsonSettings { get; set; }

        public CalibrationStore SavedCalibrations { get; private set; }

        public ObservableCollection<IGameWaveProvider> SampleProviders { get; set; }

        private AppViewModel()
        {
            try
            {
                this.SavedCalibrations =
                    JsonConvert.DeserializeObject<CalibrationStore>(File.ReadAllText("calibrations.json"));
            }
            catch (Exception e)
            {
                this.SavedCalibrations = new CalibrationStore();
            }
            
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
                this.AppSettings = JsonConvert.DeserializeObject<ApplicationSettings>(File.ReadAllText("app_settings.json"), this.JsonSettings);
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
                    this.SampleProviders.Add(new SampleGameWaveProvider(configData, this.AppSettings));
                }
                catch (Exception e)
                {
                    this.Dialog.ShowOkOnly("Failed to load sample",
                        $"Error loading a sample wav file provider: {e.Message}",
                        null,
                        new SolidColorBrush(Colors.Yellow));
                }
            }

            this.Mode = AppMode.Loading;
            this.Config = new ProviderSelectionViewModel();
            this.Dialog = new DialogViewModel();
            this.Audio = new AudioProcessor(this.AppSettings);
            this.Calibration = new CalibrationViewModel();
        }

        public void ConfigureAndStart()
        {
            try
            {
                this.Mode = AppMode.Playing;
                this.Audio.Configure();
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

        public void CalibrateDevice(IGameWavePrecursor precursor, MeasurementType deviceType)
        {
            this.Mode = AppMode.Calibrating;
            this.Calibration.SetPrecursor(precursor, deviceType);
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}