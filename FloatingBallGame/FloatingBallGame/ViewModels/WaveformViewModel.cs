using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FloatingBallGame.Annotations;

namespace FloatingBallGame.ViewModels
{
    public class Sample
    {
        public int Time { get; }
        public float Value { get; }

        public Sample(int time, float value)
        {
            this.Time = time;
            this.Value = value;
        }
    }

    public class Pair
    {
        public Sample First { get; }
        public Sample Second { get; }

        public Pair(Sample first, Sample second)
        {
            this.First = first;
            this.Second = second;
        }
    }

    public class WaveformViewModel : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<Pair> Pairs { get; }
        public double MaxValue { get; private set; }
        public double MaxTime { get; private set; }

        private Sample _lastSample = new Sample(0, 0);
        
        public WaveformViewModel()
        {
            this.Pairs = new ObservableCollection<Pair>();
        }

        public void AddSample(Sample next)
        {
            if (next.Value > this.MaxValue)
                this.MaxValue = next.Value;
            this.MaxTime = next.Time;

            Pairs.Add(new Pair(_lastSample, next));
            _lastSample = next;
        }

        public void Clear()
        {
            this.Pairs.Clear();
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