using System;

namespace RhythmRogue.Util.Timing
{
    /// <summary>
    /// Concrete timer implementation. Pure C# — no MonoBehaviour needed.
    /// 
    /// Timers don't tick themselves. They are advanced externally by
    /// TimerManager.Tick(deltaTime), which allows global pause control
    /// and avoids per-timer Update overhead.
    /// 
    /// SOLID breakdown:
    /// - S: Only tracks elapsed time and fires callbacks. No scheduling.
    /// - O: Extended via OnCompleted/OnTick events, not by modifying this class.
    /// - L: Substitutable anywhere ITimer is expected.
    /// - I: Consumers see only ITimer.
    /// - D: No Unity dependencies. Ticked by an external driver.
    /// </summary>
    public class Timer : ITimer
    {
        /// <inheritdoc/>
        public float Duration { get; private set; }

        /// <inheritdoc/>
        public float Elapsed { get; private set; }

        /// <inheritdoc/>
        public float Remaining => Math.Max(0f, Duration - Elapsed);

        /// <inheritdoc/>
        public float Progress => Duration > 0f ? Math.Min(Elapsed / Duration, 1f) : 1f;

        /// <inheritdoc/>
        public bool IsRunning { get; private set; }

        /// <inheritdoc/>
        public bool IsCompleted { get; private set; }

        /// <inheritdoc/>
        public bool IsPaused { get; private set; }

        /// <inheritdoc/>
        public bool IsLooping { get; private set; }

        /// <summary>
        /// Whether this timer has been cancelled and should be
        /// removed by the TimerManager.
        /// </summary>
        public bool IsCancelled { get; private set; }

        /// <inheritdoc/>
        public event Action OnCompleted;

        /// <inheritdoc/>
        public event Action<float> OnTick;

        /// <summary>
        /// Create a timer. Does not start automatically —
        /// call Restart() or let TimerManager.Schedule() handle it.
        /// </summary>
        /// <param name="duration">Duration in seconds.</param>
        /// <param name="looping">Whether to restart on completion.</param>
        public Timer(float duration, bool looping = false)
        {
            if (duration < 0f)
                throw new ArgumentOutOfRangeException(nameof(duration), "Must be non-negative.");

            Duration = duration;
            IsLooping = looping;
        }

        /// <summary>
        /// Advance the timer by deltaTime. Called by TimerManager.
        /// </summary>
        /// <param name="deltaTime">Time to advance in seconds.</param>
        /// <returns>True if the timer is still alive (not cancelled).</returns>
        internal bool Tick(float deltaTime)
        {
            if (IsCancelled) return false;
            if (!IsRunning || IsPaused || IsCompleted) return true;

            Elapsed += deltaTime;
            OnTick?.Invoke(Progress);

            if (Elapsed >= Duration)
            {
                if (IsLooping)
                {
                    // Preserve overflow for accurate looping
                    Elapsed -= Duration;
                    OnCompleted?.Invoke();
                }
                else
                {
                    Elapsed = Duration;
                    IsRunning = false;
                    IsCompleted = true;
                    OnCompleted?.Invoke();
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public void Pause()
        {
            if (IsRunning && !IsCompleted)
                IsPaused = true;
        }

        /// <inheritdoc/>
        public void Resume()
        {
            if (IsPaused)
                IsPaused = false;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            Elapsed = 0f;
            IsRunning = false;
            IsCompleted = false;
            IsPaused = false;
        }

        /// <inheritdoc/>
        public void Restart()
        {
            Reset();
            IsRunning = true;
        }

        /// <inheritdoc/>
        public void Cancel()
        {
            IsCancelled = true;
            IsRunning = false;
            OnCompleted = null;
            OnTick = null;
        }

        /// <summary>
        /// Start the timer. Called internally by TimerManager.Schedule().
        /// </summary>
        internal void Start()
        {
            IsRunning = true;
        }

        /// <summary>
        /// Change the duration. Useful for dynamic cooldowns
        /// modified by relics or upgrades.
        /// </summary>
        public void SetDuration(float newDuration)
        {
            if (newDuration < 0f)
                throw new ArgumentOutOfRangeException(nameof(newDuration), "Must be non-negative.");

            Duration = newDuration;
        }
    }
}
