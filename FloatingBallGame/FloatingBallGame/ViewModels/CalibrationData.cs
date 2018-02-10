using System;
using System.Collections.Generic;
using FloatingBallGame.Audio;
using Newtonsoft.Json;

namespace FloatingBallGame.ViewModels
{
    public class CalibrationData
    {
        [JsonIgnore]
        public string ShortId => Id.ToString().Substring(0, 8);

        public Guid Id { get; set; }
        public MeasurementType Type { get; set; }

        public List<double> Measured{ get; set; }

        public List<double> Actual{ get; set; }

        public DateTime Created { get; set; }
        public CalibrationData()
        {
            this.Measured = new List<double>();
            this.Actual = new List<double>();
        }
    }
}