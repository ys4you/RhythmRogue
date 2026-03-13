using System;

namespace RhythmRogue.Util.Timing
{
    /// <summary>
    /// Abstraction for a single timer instance.
    /// 
    /// Timers tick toward a target duration, can be paused/resumed,
    /// and fire a callback on completion. They are independent of
    /// coroutines and survive scene transitions when managed by
    /// a TimerManager.
    /// </summary>
    public interface ITimer
    {
        /// <summary>
        /// Total duration in seconds.
        /// </summary>
        float Duration { get; }

        /// <summary>
        /// Elapsed time in seconds since the timer started.
        /// </summary>
        float Elapsed { get; }

        /// <summary>
        /// Remaining time in seconds (Duration - Elapsed).
        /// </summary>
        float Remaining { get; }

        /// <summary>
        /// Progress from 0.0 (just started) to 1.0 (completed).
        /// Useful for lerping UI elements, fading effects, etc.
        /// </summary>
        float Progress { get; }

        /// <summary>
        /// Whether the timer is currently ticking.
        /// False when paused, completed, or not yet started.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Whether the timer has reached its duration.
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// Whether the timer is paused.
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// Whether the timer loops back to zero on completion.
        /// </summary>
        bool IsLooping { get; }

        /// <summary>
        /// Pause the timer. Elapsed time is preserved.
        /// </summary>
        void Pause();

        /// <summary>
        /// Resume a paused timer.
        /// </summary>
        void Resume();

        /// <summary>
        /// Reset the timer to zero and stop it.
        /// Does not fire the completion callback.
        /// </summary>
        void Reset();

        /// <summary>
        /// Reset and immediately start ticking again.
        /// </summary>
        void Restart();

        /// <summary>
        /// Cancel the timer entirely. Marks it for removal
        /// by the TimerManager. Cannot be restarted after cancel.
        /// </summary>
        void Cancel();

        /// <summary>
        /// Fired when the timer reaches its duration.
        /// For looping timers, fires every cycle.
        /// </summary>
        event Action OnCompleted;

        /// <summary>
        /// Fired every tick with the current progress (0.0 to 1.0).
        /// Useful for continuous updates like fade animations.
        /// </summary>
        event Action<float> OnTick;
    }
}
