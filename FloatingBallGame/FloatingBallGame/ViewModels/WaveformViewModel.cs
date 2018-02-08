using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FloatingBallGame.Annotations;
using LiveCharts;

namespace FloatingBallGame.ViewModels
{

    public class WaveformViewModel : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;

        public double MaxValue { get; private set; }
        public double MaxTime { get; private set; }

        public ChartValues<double> Values { get; }
        
        public WaveformViewModel()
        {
            this.Values = new ChartValues<double>();
        }

        public void AddSample(int time, double value)
        {
            if (value > this.MaxValue)
                this.MaxValue = value;
            this.MaxTime = time;
            this.Values.Add(value);
        }

        public void Clear()
        {
            this.Values.Clear();
            this.MaxTime = 0;
            this.MaxValue = 0;
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}