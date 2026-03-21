using System;
using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Util;
using RhythmRogue.Util.Events;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Tracks consecutive non-Miss hits and calculates a combo multiplier.
    /// 
    /// REFACTORED (PROTO-025):
    ///   Before: serialized rate, cap, milestones array inline.
    ///   After:  1 ComboConfig ScriptableObject reference.
    ///   Why:    Designers tune combo scaling in the SO asset.
    ///           Relics swap the config reference (e.g. "Combo Crown" = higher cap).
    /// </summary>
    [DisallowMultipleComponent]
    public class ComboSystem : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("Config")]
        [Tooltip("Combo configuration. Create via Assets → Create → RhythmRogue → ComboConfig.")]
        [SerializeField] private ComboConfig _config;

        [Header("References")]
        [SerializeField] private JudgmentSystem _judgmentSystem;

        // =================================================================
        // STATE
        // =================================================================

        private int _currentCombo;
        private float _currentMultiplier = 1f;
        private int _lastMilestoneIndex = -1;
        private IEventBus _eventBus;

        // =================================================================
        // PUBLIC PROPERTIES
        // =================================================================

        public int CurrentCombo => _currentCombo;
        public float Multiplier => _currentMultiplier;
        public int MaxCombo { get; private set; }
        public int TotalResets { get; private set; }

        // =================================================================
        // EVENTS
        // =================================================================

        /// <summary>Fired on every non-Miss hit. Params: combo, multiplier.</summary>
        public event Action<int, float> OnComboChanged;

        /// <summary>Fired when combo resets. Param: lost combo count.</summary>
        public event Action<int> OnComboReset;

        /// <summary>Fired at milestone thresholds. Param: milestone value.</summary>
        public event Action<int> OnComboMilestone;

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Awake()
        {
            if (_config == null)
                _config = Resources.Load<ComboConfig>("Configs/DefaultCombo");

            if (_config == null)
                GameLog.Error("[ComboSystem] No ComboConfig found! Assign in Inspector or place in Resources/DefaultCombo.");

            if (EventBusProvider.Instance != null)
                _eventBus = EventBusProvider.Instance.Bus;
        }

        private void OnEnable()
        {
            if (_judgmentSystem != null)
                _judgmentSystem.OnJudgment += HandleJudgment;
        }

        private void OnDisable()
        {
            if (_judgmentSystem != null)
                _judgmentSystem.OnJudgment -= HandleJudgment;
        }

        // =================================================================
        // CORE LOGIC
        // =================================================================

        private void HandleJudgment(JudgmentResult result)
        {
            if (result.Judgment == Judgment.Miss)
                ResetCombo();
            else
                IncrementCombo();
        }

        private void IncrementCombo()
        {
            _currentCombo++;
            _currentMultiplier = _config != null
                ? _config.GetMultiplier(_currentCombo)
                : Mathf.Min(1f + _currentCombo * 0.1f, 3f);

            if (_currentCombo > MaxCombo)
                MaxCombo = _currentCombo;

            OnComboChanged?.Invoke(_currentCombo, _currentMultiplier);

            _eventBus?.Publish(new ComboChangedEvent
            {
                CurrentCombo = _currentCombo,
                Multiplier = _currentMultiplier
            });

            CheckMilestones();
        }

        private void ResetCombo()
        {
            int lostCombo = _currentCombo;

            if (lostCombo > 0)
                TotalResets++;

            _currentCombo = 0;
            _currentMultiplier = 1f;
            _lastMilestoneIndex = -1;

            OnComboReset?.Invoke(lostCombo);

            _eventBus?.Publish(new ComboResetEvent { LostCombo = lostCombo });
        }

        private void CheckMilestones()
        {
            int[] thresholds = _config != null ? _config.milestoneThresholds : null;
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

        // =================================================================
        // PUBLIC
        // =================================================================

        /// <summary>Reset all state for a new battle.</summary>
        public void ResetAll()
        {
            _currentCombo = 0;
            _currentMultiplier = 1f;
            _lastMilestoneIndex = -1;
            MaxCombo = 0;
            TotalResets = 0;
        }

        /// <summary>Swap config at runtime (for relics).</summary>
        public void SetConfig(ComboConfig config) => _config = config;

        // =================================================================
        // CLEANUP
        // =================================================================

        private void OnDestroy()
        {
            OnComboChanged = null;
            OnComboReset = null;
            OnComboMilestone = null;
        }
    }
}