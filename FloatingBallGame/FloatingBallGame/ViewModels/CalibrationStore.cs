using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace FloatingBallGame.ViewModels
{
    public class CalibrationStore
    {
        public List<CalibrationData> Data { get; set; }


        public CalibrationStore()
        {
            Data = new List<CalibrationData>();
        }

        public void Add(CalibrationData d)
        {
            // Check if there's already one in there
            var exists = Data.FirstOrDefault(x => x.Id == d.Id && x.Type == d.Type);
            if (exists != null)
                Data.Remove(exists);
            Data.Add(d);
        }

        public void Serialize()
        {
            File.WriteAllText("calibrations.json", JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}