using System;

namespace FloatingBallGame.Audio
{
    public class TestSample
    {
        /// <summary>
        /// Gets or sets the time of this sample, measured in seconds, from the first frame of the trial
        /// </summary>
        public double Time { get; set; }

        public double Volume { get; set; }
        public double Flow { get; set; }
        public double BallCenter { get; set; }

        public double GoalUpper { get; set; }

        public double GoalLower { get; set; }

        public byte? BallRed { get; set; }
        public byte? BallGreen { get; set; }

        public byte? BallBlue { get; set; }

        public double Error{ get; set; }

        public double RatioFraction { get; set; }
    }
}