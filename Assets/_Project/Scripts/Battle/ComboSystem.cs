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
    /// Relic bonuses are applied additively on top of the ComboConfig values:
    ///   - ComboRateBoost: extra multiplier gain per hit
    ///   - ComboCapBoost: raises the multiplier ceiling
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
        // CONSTANTS
        // =================================================================

        /// <summary>
        /// Default multiplier cap from the GDD. Used as the base for
        /// relic cap boost calculations. If ComboConfig uses a different
        /// cap, update this or expose the cap from ComboConfig.
        /// </summary>
        private const float DefaultMultiplierCap = 3f;

        // =================================================================
        // STATE
        // =================================================================

        private int _currentCombo;
        private float _currentMultiplier = 1f;
        private int _lastMilestoneIndex = -1;
        private IEventBus _eventBus;

        // Relic bonuses — applied at battle start
        private float _relicRateBoost;
        private float _relicCapBoost;

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
        // RELIC MODIFIERS
        // =================================================================

        /// <summary>
        /// Apply relic bonuses to combo scaling.
        /// Called by BattleManager at battle start.
        /// </summary>
        /// <param name="rateBoost">
        /// Additional multiplier gain per hit. E.g. 0.05 means
        /// +0.05/hit on top of the config's base rate.
        /// </param>
        /// <param name="capBoost">
        /// Additional multiplier cap. E.g. 2.0 means the cap raises
        /// from 3.0x to 5.0x.
        /// </param>
        public void ApplyRelicModifiers(float rateBoost, float capBoost)
        {
            _relicRateBoost = rateBoost;
            _relicCapBoost = capBoost;

            if (rateBoost > 0f || capBoost > 0f)
                GameLog.Info($"[ComboSystem] Relic bonus: Rate+{rateBoost}/hit, Cap+{capBoost}");
        }

        /// <summary>Clear relic bonuses.</summary>
        public void ClearRelicModifiers()
        {
            _relicRateBoost = 0f;
            _relicCapBoost = 0f;
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

            // Base multiplier from config
            _currentMultiplier = _config != null
                ? _config.GetMultiplier(_currentCombo)
                : Mathf.Min(1f + _currentCombo * 0.1f, DefaultMultiplierCap);

            // Relic: additional multiplier per combo hit
            if (_relicRateBoost > 0f)
                _currentMultiplier += _currentCombo * _relicRateBoost;

            // Relic: enforce boosted cap
            float effectiveCap = DefaultMultiplierCap + _relicCapBoost;
            _currentMultiplier = Mathf.Min(_currentMultiplier, effectiveCap);

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
            ClearRelicModifiers();
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
