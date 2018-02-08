using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using FloatingBallGame.Annotations;
using FloatingBallGame.Audio;
using LiveCharts;

namespace FloatingBallGame.ViewModels
{

    public class WaveformViewModel : INotifyPropertyChanged
    {
        private double _mode1;
        private double _mode2;
        private double _mode3;

        public event PropertyChangedEventHandler PropertyChanged;

        public double MaxValue { get; private set; }
        public double MaxTime { get; private set; }

        public ChartValues<double> Values { get; }

        public double Mode1
        {
            get { return _mode1; }
            private set
            {
                if (value.Equals(_mode1)) return;
                _mode1 = value;
                OnPropertyChanged();
            }
        }

        public double Mode2
        {
            get { return _mode2; }
            private set
            {
                if (value.Equals(_mode2)) return;
                _mode2 = value;
                OnPropertyChanged();
            }
        }

        public double Mode3
        {
            get { return _mode3; }
            private set
            {
                if (value.Equals(_mode3)) return;
                _mode3 = value;
                OnPropertyChanged();
            }
        }

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


        public void Process(MeasurementType measurement)
        {
            var values = this.Values.ToList();
            values.Sort();

            int steps = 100;
            Tuple<double, int>[] density = new Tuple<double, int>[steps];
            double span = values.Max() / steps;
            for (int i = 0; i < steps; i++)
            {
                double center = span * i;
                int count = values.Count(x => Math.Abs(x - center) < span);
                density[i] = Tuple.Create(center, count);
            }

            foreach (var tuple in density)
            {
                Debug.WriteLine($"{tuple.Item1}\t{tuple.Item2}");
            }
            
            // Now we'll sweep through the density curve, finding places where the curve jumps from below 50% to above 50%, then
            // back down to below 50%.
            var peaks = new List<double>();
            bool isAbove = false;
            double risingEdge = Double.NaN;
            int thresh = density.Select(x => x.Item2).Max() / 2;
            for (int i = 1; i < density.Length; i++)
            {
                int count = density[i].Item2;

                if (!isAbove && count > thresh)
                {
                    risingEdge = density[i].Item1;
                    isAbove = true;
                }

                if (isAbove && count < thresh)
                {
                    var fallingEdge = density[i].Item1;
                    peaks.Add((risingEdge + fallingEdge) / 2.0);
                    isAbove = false;
                }
            }

            if (isAbove)
                peaks.Add(risingEdge);

            var sortedPeaks = peaks.OrderByDescending(x => x).ToList();

            /*
            this.Values.Clear();
            foreach (var tuple in density)
            {
                this.Values.Add(tuple.Item2);
            }*/

            this.Mode1 = sortedPeaks[0];
            this.Mode2 = sortedPeaks[1];
           // this.Mode3 = sortedPeaks[2];
        }


        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}