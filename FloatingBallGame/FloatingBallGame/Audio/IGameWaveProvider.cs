using NAudio.Wave;

namespace FloatingBallGame.Audio
{
    public interface IGameWaveProvider : IWaveIn
    {
        string Name { get; set; }

        WaveMode Mode { get; set; }

    }
}