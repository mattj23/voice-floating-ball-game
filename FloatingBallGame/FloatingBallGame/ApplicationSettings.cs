using System.Collections.Generic;
using System.Windows.Documents;
using System.Windows.Media;
using FloatingBallGame.Audio;

namespace FloatingBallGame
{
    public class ApplicationSettings
    {
        public List<SampleProviderConfigData> SampleProviders { get; set; }

        public int BufferMs { get; set; }

        public int SampleRate { get; set; }

        /// <summary>
        /// Frequency scaling factor, larger makes the ball oscilate faster
        /// </summary>
        public double Frequency { get; set; }

        public double UpperFlowLimit { get; set; }
        public double LowerFlowLimit { get; set; }

        public double GoalRatio { get; set; }

        public double BallSize { get; set; }

        public double TrialStartThreshold { get; set; }
        public int TrialStartWindow { get; set; }

        public int HistoryWindow { get; set; }

        public double GraphicsScale { get; set; }

        public ColorScaleKeypoint[] BallColorScale { get; set; }
    }

    public class ColorScaleKeypoint
    {
        /// <summary>
        /// The fraction of the goal ratio that this keypoint's colors apply at
        /// </summary>
        public double Ratio { get; set; }

        /// <summary>
        /// The RGB value for this keypoint
        /// </summary>
        public double[] Rgb { get; set; }

        public Color MakeColor()
        {
            return Color.FromRgb((byte) (Rgb[0] * 255), (byte) (Rgb[1] * 255), (byte) (Rgb[2] * 255));
        }
    }
}