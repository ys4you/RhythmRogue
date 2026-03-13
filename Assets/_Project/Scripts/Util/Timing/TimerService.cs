using System;
using UnityEngine;

namespace RhythmRogue.Util.Timing
{
    /// <summary>
    /// Singleton MonoBehaviour that drives the TimerManager.
    /// 
    /// Provides two timer managers:
    /// - Scaled: uses Time.deltaTime, affected by Time.timeScale (gameplay)
    /// - Unscaled: uses Time.unscaledDeltaTime, ignores pause (UI, audio)
    /// 
    /// Access via TimerService.Instance, or use the static helpers
    /// for quick one-liners.
    /// </summary>
    public class TimerService : Singleton<TimerService>
    {
        private TimerManager _scaled;
        private TimerManager _unscaled;

        /// <summary>
        /// Timer manager affected by Time.timeScale.
        /// Use for gameplay timers: hit effect lifetimes, battle countdowns,
        /// cooldowns, rest heal delays.
        /// </summary>
        public ITimerManager Scaled => _scaled;

        /// <summary>
        /// Timer manager NOT affected by Time.timeScale.
        /// Use for UI timers: menu transitions, notifications,
        /// anything that should run during pause.
        /// </summary>
        public ITimerManager Unscaled => _unscaled;

        protected override void Awake()
        {
            base.Awake();
            _scaled = new TimerManager();
            _unscaled = new TimerManager();
        }

        private void Update()
        {
            _scaled.Tick(Time.deltaTime);
            _unscaled.Tick(Time.unscaledDeltaTime);
        }

        protected override void OnDestroy()
        {
            _scaled?.CancelAll();
            _unscaled?.CancelAll();
            base.OnDestroy();
        }

        // ===================================================================
        // STATIC HELPERS — one-liners for common cases
        // ===================================================================

        /// <summary>
        /// Schedule a one-shot gameplay timer.
        /// 
        ///   TimerService.Delay(2f, () => ReturnToPool(hitEffect));
        /// </summary>
        public static ITimer Delay(float seconds, Action onCompleted)
        {
            return Instance._scaled.Schedule(seconds, onCompleted);
        }

        /// <summary>
        /// Schedule a one-shot UI timer (ignores Time.timeScale).
        /// 
        ///   TimerService.DelayUnscaled(0.5f, () => FadeOutPanel());
        /// </summary>
        public static ITimer DelayUnscaled(float seconds, Action onCompleted)
        {
            return Instance._unscaled.Schedule(seconds, onCompleted);
        }

        /// <summary>
        /// Schedule a looping gameplay timer.
        /// 
        ///   TimerService.Every(1f, () => RegenerateHP(1));
        /// </summary>
        public static ITimer Every(float interval, Action onInterval)
        {
            return Instance._scaled.ScheduleLooping(interval, onInterval);
        }

        /// <summary>
        /// Schedule a timer with per-frame progress (0→1) updates.
        /// 
        ///   TimerService.Lerp(0.3f,
        ///       t => canvasGroup.alpha = 1f - t,
        ///       () => panel.SetActive(false));
        /// </summary>
        public static ITimer Lerp(float duration, Action<float> onTick, Action onCompleted = null)
        {
            return Instance._scaled.ScheduleWithProgress(duration, onTick, onCompleted);
        }

        /// <summary>
        /// Same as Lerp but unscaled (ignores Time.timeScale).
        /// </summary>
        public static ITimer LerpUnscaled(float duration, Action<float> onTick, Action onCompleted = null)
        {
            return Instance._unscaled.ScheduleWithProgress(duration, onTick, onCompleted);
        }
    }
}
