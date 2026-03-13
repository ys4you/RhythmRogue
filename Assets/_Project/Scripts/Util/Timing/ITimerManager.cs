using System;

namespace RhythmRogue.Util.Timing
{
    /// <summary>
    /// Abstraction for a timer management system.
    /// 
    /// Handles creation, scheduling, ticking, and cleanup of timers.
    /// Supports global pause for all managed timers at once
    /// (e.g. when the game pauses).
    /// </summary>
    public interface ITimerManager
    {
        /// <summary>
        /// Number of active (non-cancelled, non-completed) timers.
        /// </summary>
        int ActiveCount { get; }

        /// <summary>
        /// Whether all managed timers are globally paused.
        /// </summary>
        bool IsGloballyPaused { get; }

        /// <summary>
        /// Create and start a one-shot timer.
        /// </summary>
        /// <param name="duration">Duration in seconds.</param>
        /// <param name="onCompleted">Optional callback when finished.</param>
        /// <returns>The timer instance for further control.</returns>
        ITimer Schedule(float duration, Action onCompleted = null);

        /// <summary>
        /// Create and start a looping timer.
        /// </summary>
        /// <param name="interval">Interval between fires in seconds.</param>
        /// <param name="onInterval">Callback on each interval.</param>
        /// <returns>The timer instance for further control.</returns>
        ITimer ScheduleLooping(float interval, Action onInterval);

        /// <summary>
        /// Create and start a timer with a per-tick progress callback.
        /// Ideal for lerping, fading, or any continuous animation.
        /// </summary>
        /// <param name="duration">Duration in seconds.</param>
        /// <param name="onTick">Called each frame with progress 0.0 to 1.0.</param>
        /// <param name="onCompleted">Optional callback when finished.</param>
        /// <returns>The timer instance for further control.</returns>
        ITimer ScheduleWithProgress(float duration, Action<float> onTick, Action onCompleted = null);

        /// <summary>
        /// Register an externally created timer with the manager.
        /// The manager will tick and clean it up automatically.
        /// </summary>
        void Register(Timer timer);

        /// <summary>
        /// Pause all managed timers at once.
        /// Individual timer pause state is preserved and restored.
        /// </summary>
        void PauseAll();

        /// <summary>
        /// Resume all managed timers from a global pause.
        /// Only resumes timers that weren't individually paused
        /// before the global pause.
        /// </summary>
        void ResumeAll();

        /// <summary>
        /// Cancel all managed timers.
        /// </summary>
        void CancelAll();
    }
}
