using System;
using System.Collections.Generic;

namespace RhythmRogue.Util.Timing
{
    /// <summary>
    /// Manages a collection of timers, ticking them each frame
    /// and cleaning up cancelled or completed timers.
    /// 
    /// Pure C# — no MonoBehaviour. Ticked externally via Tick(deltaTime),
    /// which gives you control over the time source (Time.deltaTime,
    /// Time.unscaledDeltaTime, or a custom clock).
    /// 
    /// Global pause suspends all timers while preserving individual
    /// pause states — pausing the game won't break a timer that was
    /// already individually paused.
    /// </summary>
    public class TimerManager : ITimerManager
    {
        private readonly List<Timer> _timers = new();
        private readonly List<Timer> _pendingAdd = new();
        private readonly HashSet<Timer> _wasPausedBeforeGlobal = new();
        private bool _isTicking;

        /// <inheritdoc/>
        public int ActiveCount => _timers.Count;

        /// <inheritdoc/>
        public bool IsGloballyPaused { get; private set; }

        /// <summary>
        /// Advance all active timers by deltaTime.
        /// Call this once per frame from a MonoBehaviour.
        /// 
        /// Use Time.deltaTime for gameplay timers (affected by Time.timeScale).
        /// Use Time.unscaledDeltaTime for UI timers (ignoring pause).
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (IsGloballyPaused) return;

            _isTicking = true;

            // Tick all timers, remove dead ones
            for (int i = _timers.Count - 1; i >= 0; i--)
            {
                bool alive = _timers[i].Tick(deltaTime);

                if (!alive)
                {
                    _timers.RemoveAt(i);
                }
            }

            _isTicking = false;

            // Add any timers scheduled during tick
            if (_pendingAdd.Count > 0)
            {
                _timers.AddRange(_pendingAdd);
                _pendingAdd.Clear();
            }
        }

        /// <summary>
        /// Remove completed (non-looping) timers.
        /// Called automatically during Tick, but can be called
        /// manually for immediate cleanup.
        /// </summary>
        public void CleanUp()
        {
            for (int i = _timers.Count - 1; i >= 0; i--)
            {
                if (_timers[i].IsCancelled || (!_timers[i].IsLooping && _timers[i].IsCompleted))
                {
                    _timers.RemoveAt(i);
                }
            }
        }

        /// <inheritdoc/>
        public ITimer Schedule(float duration, Action onCompleted = null)
        {
            var timer = new Timer(duration);

            if (onCompleted != null)
                timer.OnCompleted += onCompleted;

            AddTimer(timer);
            timer.Start();
            return timer;
        }

        /// <inheritdoc/>
        public ITimer ScheduleLooping(float interval, Action onInterval)
        {
            if (onInterval == null)
                throw new ArgumentNullException(nameof(onInterval));

            var timer = new Timer(interval, looping: true);
            timer.OnCompleted += onInterval;

            AddTimer(timer);
            timer.Start();
            return timer;
        }

        /// <inheritdoc/>
        public ITimer ScheduleWithProgress(float duration, Action<float> onTick, Action onCompleted = null)
        {
            var timer = new Timer(duration);

            if (onTick != null)
                timer.OnTick += onTick;
            if (onCompleted != null)
                timer.OnCompleted += onCompleted;

            AddTimer(timer);
            timer.Start();
            return timer;
        }

        /// <inheritdoc/>
        public void Register(Timer timer)
        {
            if (timer == null)
                throw new ArgumentNullException(nameof(timer));

            AddTimer(timer);
        }

        /// <inheritdoc/>
        public void PauseAll()
        {
            if (IsGloballyPaused) return;
            IsGloballyPaused = true;

            _wasPausedBeforeGlobal.Clear();

            foreach (var timer in _timers)
            {
                if (timer.IsPaused)
                {
                    // Remember this timer was already paused individually
                    _wasPausedBeforeGlobal.Add(timer);
                }
                else
                {
                    timer.Pause();
                }
            }
        }

        /// <inheritdoc/>
        public void ResumeAll()
        {
            if (!IsGloballyPaused) return;
            IsGloballyPaused = false;

            foreach (var timer in _timers)
            {
                // Only resume timers that weren't individually paused before
                if (!_wasPausedBeforeGlobal.Contains(timer))
                {
                    timer.Resume();
                }
            }

            _wasPausedBeforeGlobal.Clear();
        }

        /// <inheritdoc/>
        public void CancelAll()
        {
            foreach (var timer in _timers)
            {
                timer.Cancel();
            }

            _timers.Clear();
            _pendingAdd.Clear();
            _wasPausedBeforeGlobal.Clear();
            IsGloballyPaused = false;
        }

        private void AddTimer(Timer timer)
        {
            if (_isTicking)
                _pendingAdd.Add(timer);
            else
                _timers.Add(timer);
        }
    }
}
