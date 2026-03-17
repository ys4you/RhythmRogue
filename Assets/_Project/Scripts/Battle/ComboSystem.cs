using System;
using UnityEngine;
using RhythmRogue.Util.Events;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Tracks consecutive non-Miss hits and calculates a combo multiplier.
    /// 
    /// Forgiving reset model (GDD §3.4): combo resets ONLY on Miss.
    /// Bad hits still increment combo, keeping the flow going but
    /// dealing reduced damage through the judgment system.
    /// 
    /// Multiplier formula: 1.0 + (combo × multiplierPerHit), capped.
    /// At default settings (0.1 per hit, 3.0 cap), 20 consecutive
    /// hits reach the maximum multiplier.
    /// 
    /// Two event layers (established pattern):
    ///   1. C# events for direct battle scene subscribers
    ///   2. EventBus ComboChangedEvent/ComboResetEvent for decoupled systems
    /// 
    /// SOLID breakdown:
    /// - S: Only tracks combo state and fires events. No damage, no UI.
    /// - O: Relic modifiers change serialized fields, not this class.
    /// - L: Consumers read CurrentCombo/Multiplier without knowing internals.
    /// - I: Focused events: changed, reset, milestone.
    /// - D: Depends on JudgmentSystem abstraction via OnJudgment event.
    /// </summary>
    public class ComboSystem : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR — tunable, overridable by relics post-prototype
        // =================================================================

        [Header("Multiplier")]
        [Tooltip("Multiplier increase per consecutive hit. Default 0.1 = +10% per hit.")]
        [SerializeField] private float _multiplierPerHit = 0.1f;

        [Tooltip("Maximum multiplier cap. Default 3.0 = reached at 20 consecutive hits.")]
        [SerializeField] private float _maxMultiplier = 3.0f;

        [Header("Milestones")]
        [Tooltip("Combo thresholds that trigger milestone events (for UI effects, relic triggers).")]
        [SerializeField] private int[] _milestoneThresholds = { 10, 25, 50, 100 };

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

        /// <summary>Current consecutive hit count.</summary>
        public int CurrentCombo => _currentCombo;

        /// <summary>Current damage multiplier (1.0 to maxMultiplier).</summary>
        public float Multiplier => _currentMultiplier;

        /// <summary>Highest combo achieved this battle.</summary>
        public int MaxCombo { get; private set; }

        /// <summary>Number of times the combo was reset to 0.</summary>
        public int TotalResets { get; private set; }

        // =================================================================
        // EVENTS
        // =================================================================

        /// <summary>
        /// Fired on every non-Miss hit.
        /// Parameters: new combo count, new multiplier.
        /// </summary>
        public event Action<int, float> OnComboChanged;

        /// <summary>
        /// Fired when combo resets to 0 on a Miss.
        /// Parameter: the combo that was lost.
        /// </summary>
        public event Action<int> OnComboReset;

        /// <summary>
        /// Fired when combo crosses a milestone threshold.
        /// Parameter: the milestone value (10, 25, 50, 100).
        /// </summary>
        public event Action<int> OnComboMilestone;

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Awake()
        {
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
            {
                ResetCombo();
            }
            else
            {
                IncrementCombo();
            }
        }

        private void IncrementCombo()
        {
            _currentCombo++;
            _currentMultiplier = Mathf.Min(
                1f + _currentCombo * _multiplierPerHit,
                _maxMultiplier);

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

            _eventBus?.Publish(new ComboResetEvent
            {
                LostCombo = lostCombo
            });
        }

        private void CheckMilestones()
        {
            if (_milestoneThresholds == null) return;

            for (int i = 0; i < _milestoneThresholds.Length; i++)
            {
                if (i <= _lastMilestoneIndex) continue;

                if (_currentCombo >= _milestoneThresholds[i])
                {
                    _lastMilestoneIndex = i;
                    OnComboMilestone?.Invoke(_milestoneThresholds[i]);
                }
            }
        }

        // =================================================================
        // PUBLIC — reset for new battle
        // =================================================================

        /// <summary>
        /// Reset all combo state. Call at battle start.
        /// </summary>
        public void ResetAll()
        {
            _currentCombo = 0;
            _currentMultiplier = 1f;
            _lastMilestoneIndex = -1;
            MaxCombo = 0;
            TotalResets = 0;
        }

        // =================================================================
        // PUBLIC — relic modifier hooks
        // =================================================================

        /// <summary>
        /// Override the multiplier rate. Called by relic system post-prototype.
        /// </summary>
        public void SetMultiplierRate(float rate)
        {
            _multiplierPerHit = Mathf.Max(0f, rate);
        }

        /// <summary>
        /// Override the multiplier cap. Called by relic system post-prototype.
        /// </summary>
        public void SetMaxMultiplier(float max)
        {
            _maxMultiplier = Mathf.Max(1f, max);
        }

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
