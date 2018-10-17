using System.Collections.Generic;
using System.Windows.Documents;
using FloatingBallGame.Audio;

namespace FloatingBallGame
{
    public class ApplicationSettings
    {
        public List<SampleProviderConfigData> SampleProviders { get; set; }

        public int BufferMs { get; set; }

        public int SampleRate { get; set; }

        public double FlowScale { get; set; }
        public double FlowOffset { get; set; }
        public double Frequency { get; set; }
        public double Amplitude { get; set; }

        public double UpperFlowLimit { get; set; }
        public double LowerFlowLimit { get; set; }

        public double ErrorBarRatio { get; set; }

        public double BallSize { get; set; }

        public double TrialStartThreshold { get; set; }
        public int TrialStartWindow { get; set; }

        public int HistoryWindow { get; set; }

        public double GoalFlow { get; set; }

        public double GraphicsScale { get; set; }
    }
}