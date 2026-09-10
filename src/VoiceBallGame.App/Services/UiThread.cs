using System;
using Avalonia.Threading;

namespace VoiceBallGame.App.Services;

/// <summary>
/// Moves observable notifications onto the UI thread.
/// </summary>
/// <remarks>
/// The engine and its signal sources use System.Reactive, while ReactiveUI 24 and Avalonia 12
/// each use scheduling abstractions that do not interoperate with it. The single point where
/// notifications cross threads posts them directly to Avalonia's dispatcher.
/// </remarks>
public static class UiThread
{
    public static IDisposable SubscribeOnUiThread<T>(this IObservable<T> source, Action<T> onNext)
    {
        return source.Subscribe(new UiObserver<T>(onNext));
    }

    private sealed class UiObserver<T> : IObserver<T>
    {
        private readonly Action<T> _onNext;

        public UiObserver(Action<T> onNext) => _onNext = onNext;

        public void OnNext(T value)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                _onNext(value);
                return;
            }

            // Posted rather than invoked so a slow frame on the UI thread never blocks the game
            // loop, which has a sensor and a microphone waiting on it.
            Dispatcher.UIThread.Post(() => _onNext(value), DispatcherPriority.Normal);
        }

        public void OnError(Exception error) { }

        public void OnCompleted() { }
    }
}
