using System;

namespace RhythmRogue.Util.Timing
{
    /// <summary>
    /// Reusable cooldown tracker.
    /// 
    /// Manages the "ready → use → on cooldown → ready" cycle.
    /// Simpler than a full Timer for cases where you just need to
    /// know "can I do this yet?" — ability cooldowns, spawn intervals,
    /// input debouncing, etc.
    /// 
    /// Tick-based: call Update(deltaTime) each frame, or let a
    /// TimerManager drive it via Register().
    /// </summary>
    public class Cooldown
    {
        /// <summary>
        /// Cooldown duration in seconds.
        /// </summary>
        public float Duration { get; private set; }

        /// <summary>
        /// Time remaining before ready.
        /// </summary>
        public float Remaining { get; private set; }

        /// <summary>
        /// Progress from 0.0 (just used) to 1.0 (ready).
        /// Useful for UI cooldown fill indicators.
        /// </summary>
        public float Progress => Duration > 0f ? 1f - Math.Min(Remaining / Duration, 1f) : 1f;

        /// <summary>
        /// Whether the cooldown has elapsed and is ready to use.
        /// </summary>
        public bool IsReady => Remaining <= 0f;

        /// <summary>
        /// Fired when the cooldown finishes and becomes ready.
        /// </summary>
        public event Action OnReady;

        /// <summary>
        /// Create a cooldown. Starts ready.
        /// </summary>
        /// <param name="duration">Cooldown duration in seconds.</param>
        public Cooldown(float duration)
        {
            Duration = duration;
            Remaining = 0f;
        }

        /// <summary>
        /// Try to use the cooldown. Returns true and starts the
        /// cooldown if ready, false if still on cooldown.
        /// 
        ///   if (dashCooldown.TryUse())
        ///       PerformDash();
        /// </summary>
        public bool TryUse()
        {
            if (!IsReady) return false;

            Remaining = Duration;
            return true;
        }

        /// <summary>
        /// Force the cooldown to start, even if already on cooldown.
        /// Resets the remaining time to full duration.
        /// </summary>
        public void Use()
        {
            Remaining = Duration;
        }

        /// <summary>
        /// Force the cooldown to be ready immediately.
        /// </summary>
        public void ForceReady()
        {
            Remaining = 0f;
        }

        /// <summary>
        /// Reduce remaining cooldown by a flat amount.
        /// Useful for relics that reduce cooldowns.
        /// </summary>
        public void ReduceBy(float seconds)
        {
            Remaining = Math.Max(0f, Remaining - seconds);
        }

        /// <summary>
        /// Change the cooldown duration. Does not affect current remaining time.
        /// </summary>
        public void SetDuration(float newDuration)
        {
            Duration = Math.Max(0f, newDuration);
        }

        /// <summary>
        /// Advance the cooldown by deltaTime.
        /// Call each frame, or let TimerManager handle it.
        /// </summary>
        public void Update(float deltaTime)
        {
            if (IsReady) return;

            Remaining -= deltaTime;

            if (Remaining <= 0f)
            {
                Remaining = 0f;
                OnReady?.Invoke();
            }
        }
    }
}
