using System;
using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Util;
using RhythmRogue.Util.Events;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Evaluates timing offsets and assigns hit judgments.
    /// 
    /// REFACTORED (PROTO-025):
    ///   Before: 3 serialized floats for timing windows inline.
    ///   After:  1 JudgmentConfig ScriptableObject reference.
    ///   Why:    Designers tune timing in the SO asset, not per-scene.
    ///           Relics can swap the config reference at runtime.
    ///           Multiple presets (Easy/Normal/Hard) are just different SO assets.
    /// 
    /// Two event layers (matching the established pattern):
    ///   1. C# event OnJudgment — for direct subscribers in the battle scene
    ///   2. EventBus NoteJudgedEvent — for decoupled systems (UI, analytics)
    /// </summary>
    [DisallowMultipleComponent]
    public class JudgmentSystem : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("Config")]
        [Tooltip("Timing window configuration. Create via Assets → Create → RhythmRogue → JudgmentConfig.")]
        [SerializeField] private JudgmentConfig _config;

        [Header("References")]
        [SerializeField] private NoteMatcher _noteMatcher;
        [SerializeField] private NoteHighway _highway;

        // =================================================================
        // EVENTS
        // =================================================================

        /// <summary>
        /// Fired for every judgment — player hits AND auto-misses.
        /// </summary>
        public event Action<JudgmentResult> OnJudgment;

        // =================================================================
        // STATE
        // =================================================================

        private IEventBus _eventBus;
        private float _calibrationOffsetMs;

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Awake()
        {
            _calibrationOffsetMs = PlayerPrefs.GetFloat("audioOffset", 0f);

            if (_config == null)
                _config = Resources.Load<JudgmentConfig>("Configs/DefaultJudgment");

            if (_config == null)
                GameLog.Error("[JudgmentSystem] No JudgmentConfig found! Assign in Inspector or place in Resources/Configs/DefaultJudgment.");

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
        /// Reads windows from the assigned JudgmentConfig SO.
        /// </summary>
        public Judgment Judge(float rawDeltaMs)
        {
            float adjusted = Mathf.Abs(rawDeltaMs - _calibrationOffsetMs);

            if (_config == null) return Judgment.Miss;

            if (adjusted <= _config.perfectWindowMs) return Judgment.Perfect;
            if (adjusted <= _config.goodWindowMs) return Judgment.Good;
            if (adjusted <= _config.badWindowMs) return Judgment.Bad;
            return Judgment.Miss;
        }

        // =================================================================
        // PLAYER HIT
        // =================================================================

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
        // AUTO-MISS
        // =================================================================

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
        // EVENT DISPATCH
        // =================================================================

        private void FireJudgment(JudgmentResult result)
        {
            OnJudgment?.Invoke(result);

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

        public void SetCalibrationOffset(float offsetMs)
        {
            _calibrationOffsetMs = offsetMs;
            PlayerPrefs.SetFloat("audioOffset", offsetMs);
            PlayerPrefs.Save();
        }

        public float CalibrationOffsetMs => _calibrationOffsetMs;

        // =================================================================
        // CONFIG ACCESS — for debug overlays and relic system
        // =================================================================

        /// <summary>Current config reference. Relics can swap this at runtime.</summary>
        public JudgmentConfig Config => _config;

        /// <summary>Swap the timing config at runtime (for relics).</summary>
        public void SetConfig(JudgmentConfig config) => _config = config;

        public float PerfectWindowMs => _config != null ? _config.perfectWindowMs : 35f;
        public float GoodWindowMs => _config != null ? _config.goodWindowMs : 70f;
        public float BadWindowMs => _config != null ? _config.badWindowMs : 110f;

        // =================================================================
        // CLEANUP
        // =================================================================

        private void OnDestroy()
        {
            OnJudgment = null;
        }
    }
}