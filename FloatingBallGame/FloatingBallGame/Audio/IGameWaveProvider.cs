using NAudio.Wave;

namespace FloatingBallGame.Audio
{

    /// <summary>
    /// An IGameWaveProvider hides the means of providing wave data from both a direct WaveIn device and 
    /// a pair of pre-recorded sample files behind a common interface. 
    /// </summary>
    public interface IGameWaveProvider : IWaveIn
    {
        string Name { get; }

        WaveMode Mode { get; set; }

    }
}