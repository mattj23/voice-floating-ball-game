using PortAudioSharp;

namespace VoiceBallGame.Hardware;

/// <summary>
/// Brings PortAudio up and down around the parts of the application that need it.
/// </summary>
/// <remarks>
/// PortAudio's initialize and terminate calls are global and must be balanced. Device enumeration
/// on the setup screen and audio capture during a session both need the library running, and they
/// overlap, so use is counted rather than tied to any one object's lifetime.
/// </remarks>
public static class PortAudioRuntime
{
    private static readonly object Gate = new();
    private static int _users;

    public static void Acquire()
    {
        lock (Gate)
        {
            if (_users == 0) PortAudio.Initialize();
            _users++;
        }
    }

    public static void Release()
    {
        lock (Gate)
        {
            if (_users == 0) return;

            _users--;
            if (_users == 0) PortAudio.Terminate();
        }
    }

    /// <summary>Runs an action with PortAudio initialized and releases it afterward.</summary>
    public static T Use<T>(Func<T> action)
    {
        Acquire();
        try
        {
            return action();
        }
        finally
        {
            Release();
        }
    }
}
