using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Util;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Handles event-driven relic effects during a battle.
    /// 
    /// Some relic effects can't be expressed as static numeric bonuses —
    /// they trigger on specific gameplay events. This MonoBehaviour
    /// subscribes to those events and applies the effects.
    /// 
    /// Current event-driven effects:
    ///   - HealOnComboMilestone: heals player when ComboSystem fires OnComboMilestone
    /// 
    /// Future event-driven effects would go here:
    ///   - "Heal on Perfect streak of 10"
    ///   - "Deal bonus damage on combo reset"
    ///   - "Gain shield at battle start"
    /// 
    /// Initialized by BattleManager after relic aggregation.
    /// Does nothing if HealOnMilestoneHP is 0 (no relevant relics).
    /// 
    /// SOLID:
    ///   S — Only applies event-driven relic effects. No aggregation, no UI.
    ///   O — New event-driven effects add methods, not modify existing ones.
    ///   D — Depends on ComboSystem and PlayerHealth abstractions.
    /// </summary>
    public class RelicEffectHandler : MonoBehaviour
    {
        // =================================================================
        // REFERENCES — set by BattleManager or inspector
        // =================================================================

        [Header("References")]
        [SerializeField] private ComboSystem _comboSystem;

        // =================================================================
        // STATE
        // =================================================================

        private RelicModifiers _modifiers;
        private PlayerHealth _playerHealth;
        private bool _initialized;

        // =================================================================
        // INITIALIZATION
        // =================================================================

        /// <summary>
        /// Initialize with computed relic modifiers.
        /// Called by BattleManager.InitializeBattle() after aggregation.
        /// </summary>
        public void Initialize(RelicModifiers modifiers)
        {
            Cleanup();

            _modifiers = modifiers;
            _playerHealth = PlayerHealth.Instance;

            if (!_modifiers.HasAnyEffect)
            {
                _initialized = false;
                return;
            }

            // Subscribe to event-driven effects
            if (_modifiers.HealOnMilestoneHP > 0 && _comboSystem != null)
            {
                _comboSystem.OnComboMilestone += HandleComboMilestone;
            }

            _initialized = true;

            GameLog.Info($"[RelicEffectHandler] Initialized: {_modifiers}");
        }

        // =================================================================
        // EVENT HANDLERS
        // =================================================================

        /// <summary>
        /// Heal the player when a combo milestone is reached.
        /// </summary>
        private void HandleComboMilestone(int milestoneValue)
        {
            if (_playerHealth == null || !_playerHealth.IsAlive) return;
            if (_modifiers.HealOnMilestoneHP <= 0) return;

            _playerHealth.Heal(_modifiers.HealOnMilestoneHP);

            GameLog.Info($"[RelicEffectHandler] Combo milestone {milestoneValue} — " +
                      $"healed {_modifiers.HealOnMilestoneHP} HP");
        }

        // =================================================================
        // QUERIES — for UI/debug
        // =================================================================

        /// <summary>Current active modifiers. Read by debug overlays.</summary>
        public RelicModifiers ActiveModifiers => _modifiers;

        /// <summary>Whether any relic effects are active this battle.</summary>
        public bool HasActiveEffects => _initialized && _modifiers.HasAnyEffect;

        // =================================================================
        // CLEANUP
        // =================================================================

        private void Cleanup()
        {
            if (_comboSystem != null)
                _comboSystem.OnComboMilestone -= HandleComboMilestone;

            _initialized = false;
            _modifiers = RelicModifiers.None;
        }

        private void OnDisable()
        {
            Cleanup();
        }

        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
