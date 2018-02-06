using System;

namespace FloatingBallGame.Audio
{
    /// <summary>
    /// An IGameWavePrecursor is a model used for device selection.
    /// </summary>
    public interface IGameWavePrecursor
    {
        string Name { get; }

        Guid Id { get; }

        IGameWaveProvider ToProvider();

    }
}