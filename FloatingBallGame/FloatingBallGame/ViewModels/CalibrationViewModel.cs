using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FloatingBallGame.Annotations;
using FloatingBallGame.Audio;
using NAudio.Wave;

namespace FloatingBallGame.ViewModels
{
    public class CalibrationViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public IGameWavePrecursor Precursor { get; private set; }
        public IGameWaveProvider Provider { get; private set; }

        public CalibrationViewModel()
        {
            
        }

        public void SetPrecursor(IGameWavePrecursor precursor)
        {
            // Unsubscribe
            if (this.Provider != null)
                this.Provider.DataAvailable -= ProviderOnDataAvailable;

            this.Precursor = precursor;
            this.Provider = precursor.ToProvider();
            this.Provider.DataAvailable += ProviderOnDataAvailable;
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