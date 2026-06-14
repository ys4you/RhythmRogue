using System;
using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Util;
using RhythmRogue.Util.Events;

namespace RhythmRogue.Battle
{
    [DisallowMultipleComponent]
    public class JudgmentSystem : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private JudgmentConfig _config;
        [Header("References")]
        [SerializeField] private NoteMatcher _noteMatcher;
        [SerializeField] private NoteHighway _highway;

        public event Action<JudgmentResult> OnJudgment;

        private IEventBus _eventBus;
        private float _calibrationOffsetMs;
        private float _relicBonusPerfectMs;
        // Run forgiveness tier: multiplies every hit window. Relaxed widens (>1), Hard tightens
        // (<1), Normal leaves it at 1. Set once at battle start from RunState.Tier.
        private float _tierWindowScale = 1f;

        private void Awake()
        {
            _calibrationOffsetMs = PlayerPrefs.GetFloat("audioOffset", 0f);
            if (_config == null) _config = Resources.Load<JudgmentConfig>("Configs/DefaultJudgment");
            if (_config == null) GameLog.Error("[JudgmentSystem] No JudgmentConfig found!");
            if (EventBusProvider.Instance != null) _eventBus = EventBusProvider.Instance.Bus;
        }

        private void OnEnable()
        {
            if (_noteMatcher != null) _noteMatcher.OnNoteHit += HandleNoteHit;
            if (_highway != null) _highway.OnNoteMissedEvent += AutoMiss;
        }

        private void OnDisable()
        {
            if (_noteMatcher != null) _noteMatcher.OnNoteHit -= HandleNoteHit;
            if (_highway != null) _highway.OnNoteMissedEvent -= AutoMiss;
        }

        public void ApplyRelicModifiers(float bonusPerfectMs) => _relicBonusPerfectMs = bonusPerfectMs;
        public void ClearRelicModifiers() => _relicBonusPerfectMs = 0f;

        /// <summary>Apply the run's forgiveness tier as a multiplier on all hit windows.</summary>
        public void ApplyTier(DifficultyTier tier) => _tierWindowScale = DifficultyTierConfig.WindowScale(tier);

        public Judgment Judge(float rawDeltaMs)
        {
            float adjusted = Mathf.Abs(rawDeltaMs - _calibrationOffsetMs);
            if (_config == null) return Judgment.Miss;
            if (adjusted <= (_config.perfectWindowMs + _relicBonusPerfectMs) * _tierWindowScale) return Judgment.Perfect;
            if (adjusted <= _config.goodWindowMs * _tierWindowScale) return Judgment.Good;
            if (adjusted <= _config.badWindowMs * _tierWindowScale) return Judgment.Bad;
            return Judgment.Miss;
        }

        private void HandleNoteHit(NoteMatchResult match)
        {
            float adjustedMs = match.OffsetMs - _calibrationOffsetMs;
            Judgment judgment = Judge(match.OffsetMs);
            FireJudgment(new JudgmentResult(judgment, adjustedMs, match.OffsetMs, match.Lane, false, match.Note));
        }

        public void AutoMiss(NoteView note)
        {
            FireJudgment(new JudgmentResult(Judgment.Miss, 0f, 0f, note.Data.Lane, true, note));
        }

        private void FireJudgment(JudgmentResult result)
        {
            OnJudgment?.Invoke(result);
            _eventBus?.Publish(new NoteJudgedEvent { Judgment = (int)result.Judgment, Lane = result.Lane, OffsetMs = result.AdjustedOffsetMs });
        }

        public void SetCalibrationOffset(float offsetMs)
        {
            _calibrationOffsetMs = offsetMs;
            PlayerPrefs.SetFloat("audioOffset", offsetMs);
            PlayerPrefs.Save();
        }

        public float CalibrationOffsetMs => _calibrationOffsetMs;
        public JudgmentConfig Config => _config;
        public void SetConfig(JudgmentConfig config) => _config = config;
        public float PerfectWindowMs => ((_config != null ? _config.perfectWindowMs : 35f) + _relicBonusPerfectMs) * _tierWindowScale;
        public float GoodWindowMs => (_config != null ? _config.goodWindowMs : 70f) * _tierWindowScale;
        public float BadWindowMs => (_config != null ? _config.badWindowMs : 110f) * _tierWindowScale;

        private void OnDestroy() => OnJudgment = null;
    }
}
