using System;
using UnityEngine;
using RhythmRogue.Util.Events;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Evaluates timing offsets and assigns hit judgments.
    /// 
    /// The bridge between input detection and combat. Receives raw
    /// timing deltas from NoteMatcher, applies calibration offset,
    /// classifies into Perfect/Good/Bad/Miss using the GDD timing
    /// windows, and fires events for all downstream systems.
    /// 
    /// Two event layers (matching the established pattern):
    ///   1. C# event OnJudgment — for direct subscribers in the battle scene
    ///   2. EventBus NoteJudgedEvent — for decoupled systems (UI, analytics)
    /// 
    /// Timing windows from GDD §3.3:
    ///   Perfect: ±35ms
    ///   Good:    ±70ms
    ///   Bad:     ±110ms
    ///   Miss:    beyond ±110ms
    /// 
    /// Calibration offset:
    ///   Positive offset = player is hitting early (notes judged later)
    ///   Negative offset = player is hitting late (notes judged earlier)
    ///   adjustedDelta = rawDelta - calibrationOffset
    /// 
    /// SOLID breakdown:
    /// - S: Only evaluates timing and fires events. No damage, no combo.
    /// - O: New judgment tiers added by extending windows, not modifying logic.
    /// - L: Consumers see JudgmentResult regardless of how evaluation works.
    /// - I: One input (timing delta), one output (JudgmentResult event).
    /// - D: Depends on NoteMatcher/NoteHighway abstractions.
    /// </summary>
    public class JudgmentSystem : MonoBehaviour
    {
        // =================================================================
        // TIMING WINDOWS — GDD §3.3, exposed for inspector tuning
        // =================================================================

        [Header("Timing Windows (ms)")]
        [Tooltip("±35ms — tightest window, highest reward.")]
        [SerializeField] private float _perfectWindowMs = 35f;

        [Tooltip("±70ms — moderate window.")]
        [SerializeField] private float _goodWindowMs = 70f;

        [Tooltip("±110ms — widest hit window. Beyond this is a Miss.")]
        [SerializeField] private float _badWindowMs = 110f;

        [Header("Calibration")]
        [Tooltip("Audio offset in milliseconds. Loaded from PlayerPrefs. " +
                 "Positive = player is early, negative = player is late.")]
        [SerializeField] private float _calibrationOffsetMs = 0f;

        [Header("References")]
        [SerializeField] private NoteMatcher _noteMatcher;
        [SerializeField] private NoteHighway _highway;

        // =================================================================
        // EVENTS
        // =================================================================

        /// <summary>
        /// Fired for every judgment — player hits AND auto-misses.
        /// Direct subscribers: combo system, damage pipeline, hit feedback.
        /// </summary>
        public event Action<JudgmentResult> OnJudgment;

        // =================================================================
        // STATE
        // =================================================================

        private IEventBus _eventBus;

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Awake()
        {
            // Load saved calibration offset
            _calibrationOffsetMs = PlayerPrefs.GetFloat("audioOffset", 0f);

            // Get EventBus for broadcasting NoteJudgedEvent
            if (EventBusProvider.Instance != null)
                _eventBus = EventBusProvider.Instance.Bus;
        }

        private void OnEnable()
        {
            if (_noteMatcher != null)
                _noteMatcher.OnNoteHit += HandleNoteHit;

            if (_highway != null)
                _highway.OnNoteMissedEvent += AutoMiss;
        }

        private void OnDisable()
        {
            if (_noteMatcher != null)
                _noteMatcher.OnNoteHit -= HandleNoteHit;

            if (_highway != null)
                _highway.OnNoteMissedEvent -= AutoMiss;
        }

        // =================================================================
        // CORE JUDGMENT — stateless evaluation
        // =================================================================

        /// <summary>
        /// Evaluate a raw timing delta and return a judgment.
        /// 
        /// Applies calibration offset, then checks against timing windows
        /// using absolute value (symmetric early/late windows).
        /// 
        /// This method is stateless — same input always gives same output.
        /// </summary>
        /// <param name="rawDeltaMs">
        /// Raw timing offset in milliseconds.
        /// Negative = early, positive = late.
        /// </param>
        /// <returns>Judgment classification.</returns>
        public Judgment Judge(float rawDeltaMs)
        {
            float adjusted = Mathf.Abs(rawDeltaMs - _calibrationOffsetMs);

            if (adjusted <= _perfectWindowMs) return Judgment.Perfect;
            if (adjusted <= _goodWindowMs) return Judgment.Good;
            if (adjusted <= _badWindowMs) return Judgment.Bad;
            return Judgment.Miss;
        }

        // =================================================================
        // PLAYER HIT — from NoteMatcher
        // =================================================================

        /// <summary>
        /// Called when NoteMatcher matches a player input to a note.
        /// Evaluates the timing, builds a JudgmentResult, and fires events.
        /// </summary>
        private void HandleNoteHit(NoteMatchResult match)
        {
            float rawMs = match.OffsetMs;
            float adjustedMs = rawMs - _calibrationOffsetMs;
            Judgment judgment = Judge(rawMs);

            var result = new JudgmentResult(
                judgment: judgment,
                adjustedOffsetMs: adjustedMs,
                rawOffsetMs: rawMs,
                lane: match.Lane,
                isAutoMiss: false,
                note: match.Note);

            FireJudgment(result);
        }

        // =================================================================
        // AUTO-MISS — called by NoteHighway for unplayed notes
        // =================================================================

        /// <summary>
        /// Judge a note as an auto-miss. Called when a note passes the
        /// despawn window without being hit.
        /// 
        /// Call this from NoteHighway.OnNoteMissed or wire it up externally.
        /// Fires the same event chain as a player-triggered judgment so
        /// combo, damage, and UI all react identically.
        /// </summary>
        /// <param name="note">The note that was missed.</param>
        public void AutoMiss(NoteView note)
        {
            var result = new JudgmentResult(
                judgment: Judgment.Miss,
                adjustedOffsetMs: 0f,
                rawOffsetMs: 0f,
                lane: note.Data.Lane,
                isAutoMiss: true,
                note: note);

            FireJudgment(result);
        }

        // =================================================================
        // EVENT DISPATCH — both C# event and EventBus
        // =================================================================

        /// <summary>
        /// Fire judgment to all listeners via both event layers.
        /// </summary>
        private void FireJudgment(JudgmentResult result)
        {
            // Layer 1: C# event for direct battle scene subscribers
            OnJudgment?.Invoke(result);

            // Layer 2: EventBus for decoupled systems (UI, analytics)
            _eventBus?.Publish(new NoteJudgedEvent
            {
                Judgment = (int)result.Judgment,
                Lane = result.Lane,
                OffsetMs = result.AdjustedOffsetMs
            });
        }

        // =================================================================
        // CALIBRATION
        // =================================================================

        /// <summary>
        /// Update the calibration offset at runtime (from settings UI).
        /// Persists to PlayerPrefs immediately.
        /// </summary>
        /// <param name="offsetMs">New offset in milliseconds.</param>
        public void SetCalibrationOffset(float offsetMs)
        {
            _calibrationOffsetMs = offsetMs;
            PlayerPrefs.SetFloat("audioOffset", offsetMs);
            PlayerPrefs.Save();

            Debug.Log($"[JudgmentSystem] Calibration offset set to {offsetMs:F1}ms");
        }

        /// <summary>
        /// Current calibration offset in milliseconds.
        /// </summary>
        public float CalibrationOffsetMs => _calibrationOffsetMs;

        // =================================================================
        // PUBLIC QUERIES — for debug/UI
        // =================================================================

        /// <summary>Perfect window in ms.</summary>
        public float PerfectWindowMs => _perfectWindowMs;

        /// <summary>Good window in ms.</summary>
        public float GoodWindowMs => _goodWindowMs;

        /// <summary>Bad window in ms.</summary>
        public float BadWindowMs => _badWindowMs;

        // =================================================================
        // CLEANUP
        // =================================================================

        private void OnDestroy()
        {
            OnJudgment = null;
        }
    }
}
