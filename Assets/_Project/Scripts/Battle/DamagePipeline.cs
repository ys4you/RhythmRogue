using System;
using UnityEngine;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Connects judgment, combo, and health into a unified damage pipeline.
    /// 
    /// On every judgment:
    ///   - Perfect/Good/Bad → deal baseDamage × comboMultiplier to enemy
    ///   - Miss → deal flat damage to player (no multiplier)
    /// 
    /// On hold ticks:
    ///   - Each tick → deal tickDamage × comboMultiplier to enemy
    /// 
    /// GDD §3.3 base damage values:
    ///   Perfect: 5, Good: 3, Bad: 1, Miss: 5 (to player)
    /// 
    /// GDD §3.5 damage formula:
    ///   Base Judgment Damage × Combo Multiplier
    /// 
    /// All damage values are serialized for Inspector tuning.
    /// Relic modifiers can override them post-prototype.
    /// 
    /// SOLID breakdown:
    /// - S: Only calculates and applies damage. No combo tracking, no HP logic.
    /// - O: Relic modifiers override serialized values, not this class.
    /// - L: Consumers see DamageResult events regardless of calculation internals.
    /// - I: One event out (OnDamageDealt).
    /// - D: Depends on JudgmentSystem, ComboSystem, PlayerHealth, EnemyHealth.
    /// </summary>
    public class DamagePipeline : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR — base damage values (GDD §3.3)
        // =================================================================

        [Header("Enemy Damage (per judgment)")]
        [Tooltip("Base damage dealt to enemy on Perfect hit.")]
        [SerializeField] private int _perfectDamage = 5;

        [Tooltip("Base damage dealt to enemy on Good hit.")]
        [SerializeField] private int _goodDamage = 3;

        [Tooltip("Base damage dealt to enemy on Bad hit.")]
        [SerializeField] private int _badDamage = 1;

        [Header("Player Damage")]
        [Tooltip("Flat damage dealt to player on Miss. Not affected by combo.")]
        [SerializeField] private int _missDamage = 5;

        [Header("Hold Note Damage")]
        [Tooltip("Damage per hold tick. Multiplied by combo multiplier.")]
        [SerializeField] private int _holdTickDamage = 1;

        [Header("References")]
        [SerializeField] private JudgmentSystem _judgmentSystem;
        [SerializeField] private ComboSystem _comboSystem;
        [SerializeField] private EnemyHealth _enemyHealth;
        [SerializeField] private HoldTracker _holdTracker;

        // =================================================================
        // EVENTS
        // =================================================================

        /// <summary>
        /// Fired after damage is applied. UI subscribes to show
        /// floating damage numbers, HP bar flashes, screen shake.
        /// </summary>
        public event Action<DamageResult> OnDamageDealt;

        // =================================================================
        // STATE
        // =================================================================

        private PlayerHealth _playerHealth;

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Awake()
        {
            _playerHealth = PlayerHealth.Instance;
        }

        private void OnEnable()
        {
            if (_judgmentSystem != null)
                _judgmentSystem.OnJudgment += HandleJudgment;

            if (_holdTracker != null)
                _holdTracker.OnHoldTick += HandleHoldTick;
        }

        private void OnDisable()
        {
            if (_judgmentSystem != null)
                _judgmentSystem.OnJudgment -= HandleJudgment;

            if (_holdTracker != null)
                _holdTracker.OnHoldTick -= HandleHoldTick;
        }

        // =================================================================
        // JUDGMENT DAMAGE
        // =================================================================

        private void HandleJudgment(JudgmentResult result)
        {
            if (result.Judgment == Judgment.Miss)
            {
                ApplyPlayerDamage(result);
            }
            else
            {
                ApplyEnemyDamage(result);
            }
        }

        /// <summary>
        /// Miss → flat damage to player. No multiplier.
        /// </summary>
        private void ApplyPlayerDamage(JudgmentResult result)
        {
            if (_playerHealth == null || !_playerHealth.IsAlive) return;

            _playerHealth.TakeDamage(_missDamage);

            var damageResult = new DamageResult(
                amount: _missDamage,
                judgment: Judgment.Miss,
                isPlayerDamage: true,
                multiplier: 1f,
                lane: result.Lane);

            OnDamageDealt?.Invoke(damageResult);
        }

        /// <summary>
        /// Perfect/Good/Bad → base damage × combo multiplier to enemy.
        /// </summary>
        private void ApplyEnemyDamage(JudgmentResult result)
        {
            if (_enemyHealth == null || !_enemyHealth.IsAlive) return;

            int baseDamage = GetBaseDamage(result.Judgment);
            float multiplier = _comboSystem != null ? _comboSystem.Multiplier : 1f;
            int finalDamage = Mathf.RoundToInt(baseDamage * multiplier);

            // Minimum 1 damage on any successful hit
            finalDamage = Mathf.Max(1, finalDamage);

            _enemyHealth.TakeDamage(finalDamage);

            var damageResult = new DamageResult(
                amount: finalDamage,
                judgment: result.Judgment,
                isPlayerDamage: false,
                multiplier: multiplier,
                lane: result.Lane);

            OnDamageDealt?.Invoke(damageResult);
        }

        // =================================================================
        // HOLD TICK DAMAGE
        // =================================================================

        /// <summary>
        /// Each hold tick → tickDamage × combo multiplier to enemy.
        /// </summary>
        private void HandleHoldTick(HoldState state)
        {
            if (_enemyHealth == null || !_enemyHealth.IsAlive) return;

            float multiplier = _comboSystem != null ? _comboSystem.Multiplier : 1f;
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(_holdTickDamage * multiplier));

            _enemyHealth.TakeDamage(finalDamage);

            var damageResult = new DamageResult(
                amount: finalDamage,
                judgment: Judgment.Perfect, // Ticks are rewarded like perfects
                isPlayerDamage: false,
                multiplier: multiplier,
                lane: state.Lane);

            OnDamageDealt?.Invoke(damageResult);
        }

        // =================================================================
        // BASE DAMAGE LOOKUP
        // =================================================================

        private int GetBaseDamage(Judgment judgment)
        {
            return judgment switch
            {
                Judgment.Perfect => _perfectDamage,
                Judgment.Good => _goodDamage,
                Judgment.Bad => _badDamage,
                _ => 0
            };
        }

        // =================================================================
        // PUBLIC — relic modifier hooks
        // =================================================================

        /// <summary>Override base damage for a judgment tier.</summary>
        public void SetBaseDamage(Judgment judgment, int damage)
        {
            switch (judgment)
            {
                case Judgment.Perfect: _perfectDamage = damage; break;
                case Judgment.Good:    _goodDamage = damage; break;
                case Judgment.Bad:     _badDamage = damage; break;
                case Judgment.Miss:    _missDamage = damage; break;
            }
        }

        /// <summary>Override hold tick damage.</summary>
        public void SetHoldTickDamage(int damage)
        {
            _holdTickDamage = Mathf.Max(0, damage);
        }

        /// <summary>
        /// Set the enemy health reference. Call when loading a new battle
        /// with a different enemy.
        /// </summary>
        public void SetEnemyHealth(EnemyHealth enemy)
        {
            _enemyHealth = enemy;
        }

        // =================================================================
        // CLEANUP
        // =================================================================

        private void OnDestroy()
        {
            OnDamageDealt = null;
        }
    }
}
