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
    }
}