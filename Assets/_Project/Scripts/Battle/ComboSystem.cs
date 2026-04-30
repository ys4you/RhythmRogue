using System;
using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Util;
using RhythmRogue.Util.Events;

namespace RhythmRogue.Battle
{
    [DisallowMultipleComponent]
    public class ComboSystem : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private ComboConfig _config;
        [Header("References")]
        [SerializeField] private JudgmentSystem _judgmentSystem;

        private const float DefaultMultiplierCap = 3f;

        private int _currentCombo;
        private float _currentMultiplier = 1f;
        private int _lastMilestoneIndex = -1;
        private IEventBus _eventBus;
        private float _relicRateBoost;
        private float _relicCapBoost;

        public int CurrentCombo => _currentCombo;
        public float Multiplier => _currentMultiplier;
        public int MaxCombo { get; private set; }
        public int TotalResets { get; private set; }

        public event Action<int, float> OnComboChanged;
        public event Action<int> OnComboReset;
        public event Action<int> OnComboMilestone;

        private void Awake()
        {
            if (_config == null) _config = Resources.Load<ComboConfig>("Configs/DefaultCombo");
            if (_config == null) GameLog.Error("[ComboSystem] No ComboConfig found!");
            if (EventBusProvider.Instance != null) _eventBus = EventBusProvider.Instance.Bus;
        }

        private void OnEnable() { if (_judgmentSystem != null) _judgmentSystem.OnJudgment += HandleJudgment; }
        private void OnDisable() { if (_judgmentSystem != null) _judgmentSystem.OnJudgment -= HandleJudgment; }

        public void ApplyRelicModifiers(float rateBoost, float capBoost)
        {
            _relicRateBoost = rateBoost;
            _relicCapBoost = capBoost;
        }

        public void ClearRelicModifiers() { _relicRateBoost = 0f; _relicCapBoost = 0f; }

        private void HandleJudgment(JudgmentResult result)
        {
            if (result.Judgment == Judgment.Miss) ResetCombo();
            else IncrementCombo();
        }

        private void IncrementCombo()
        {
            _currentCombo++;

            _currentMultiplier = _config != null
                ? _config.GetMultiplier(_currentCombo)
                : Mathf.Min(1f + _currentCombo * 0.1f, DefaultMultiplierCap);

            if (_relicRateBoost > 0f) _currentMultiplier += _currentCombo * _relicRateBoost;
            _currentMultiplier = Mathf.Min(_currentMultiplier, DefaultMultiplierCap + _relicCapBoost);

            if (_currentCombo > MaxCombo) MaxCombo = _currentCombo;
            OnComboChanged?.Invoke(_currentCombo, _currentMultiplier);
            _eventBus?.Publish(new ComboChangedEvent { CurrentCombo = _currentCombo, Multiplier = _currentMultiplier });
            CheckMilestones();
        }

        private void ResetCombo()
        {
            int lost = _currentCombo;
            if (lost > 0) TotalResets++;
            _currentCombo = 0;
            _currentMultiplier = 1f;
            _lastMilestoneIndex = -1;
            OnComboReset?.Invoke(lost);
            _eventBus?.Publish(new ComboResetEvent { LostCombo = lost });
        }

        private void CheckMilestones()
        {
            int[] thresholds = _config?.milestoneThresholds;
            if (thresholds == null) return;
            for (int i = 0; i < thresholds.Length; i++)
            {
                if (i <= _lastMilestoneIndex) continue;
                if (_currentCombo >= thresholds[i])
                {
                    _lastMilestoneIndex = i;
                    OnComboMilestone?.Invoke(thresholds[i]);
                }
            }
        }

        public void ResetAll()
        {
            _currentCombo = 0;
            _currentMultiplier = 1f;
            _lastMilestoneIndex = -1;
            MaxCombo = 0;
            TotalResets = 0;
            ClearRelicModifiers();
        }

        public void SetConfig(ComboConfig config) => _config = config;

        private void OnDestroy() { OnComboChanged = null; OnComboReset = null; OnComboMilestone = null; }
    }
}
