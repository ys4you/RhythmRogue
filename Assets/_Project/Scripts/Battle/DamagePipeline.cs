using System;
using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Util;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Connects judgment, combo, and health into a unified damage pipeline.
    /// 
    /// REFACTORED (PROTO-025):
    ///   Before: 5 serialized ints for damage values inline.
    ///   After:  1 DamageConfig ScriptableObject reference.
    ///   Why:    Designers tune damage in the SO asset, not per-scene.
    ///           Enemy-specific damage overrides become trivial (assign different SO).
    /// </summary>
    [DisallowMultipleComponent]
    public class DamagePipeline : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("Config")]
        [Tooltip("Damage configuration. Create via Assets → Create → RhythmRogue → DamageConfig.")]
        [SerializeField] private DamageConfig _config;

        [Header("References")]
        [SerializeField] private JudgmentSystem _judgmentSystem;
        [SerializeField] private ComboSystem _comboSystem;
        [SerializeField] private EnemyHealth _enemyHealth;
        [SerializeField] private HoldTracker _holdTracker;

        // =================================================================
        // EVENTS
        // =================================================================

        /// <summary>
        /// Fired after damage is applied. UI subscribes for feedback.
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

            if (_config == null)
                GameLog.Error("[DamagePipeline] No DamageConfig assigned!");
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
                ApplyPlayerDamage(result);
            else
                ApplyEnemyDamage(result);
        }

        private void ApplyPlayerDamage(JudgmentResult result)
        {
            if (_playerHealth == null || !_playerHealth.IsAlive || _config == null) return;

            _playerHealth.TakeDamage(_config.missDamage);

            OnDamageDealt?.Invoke(new DamageResult(
                amount: _config.missDamage,
                judgment: Judgment.Miss,
                isPlayerDamage: true,
                multiplier: 1f,
                lane: result.Lane));
        }

        private void ApplyEnemyDamage(JudgmentResult result)
        {
            if (_enemyHealth == null || !_enemyHealth.IsAlive || _config == null) return;

            int baseDamage = _config.GetEnemyDamage((int)result.Judgment);
            float multiplier = _comboSystem != null ? _comboSystem.Multiplier : 1f;
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));

            _enemyHealth.TakeDamage(finalDamage);

            OnDamageDealt?.Invoke(new DamageResult(
                amount: finalDamage,
                judgment: result.Judgment,
                isPlayerDamage: false,
                multiplier: multiplier,
                lane: result.Lane));
        }

        // =================================================================
        // HOLD TICK DAMAGE
        // =================================================================

        private void HandleHoldTick(HoldState state)
        {
            if (_enemyHealth == null || !_enemyHealth.IsAlive || _config == null) return;

            float multiplier = _comboSystem != null ? _comboSystem.Multiplier : 1f;
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(_config.holdTickDamage * multiplier));

            _enemyHealth.TakeDamage(finalDamage);

            OnDamageDealt?.Invoke(new DamageResult(
                amount: finalDamage,
                judgment: Judgment.Perfect,
                isPlayerDamage: false,
                multiplier: multiplier,
                lane: state.Lane));
        }

        // =================================================================
        // PUBLIC — runtime config swap (for relics/enemies)
        // =================================================================

        /// <summary>Swap damage config at runtime.</summary>
        public void SetConfig(DamageConfig config) => _config = config;

        /// <summary>Set the enemy health reference for a new battle.</summary>
        public void SetEnemyHealth(EnemyHealth enemy) => _enemyHealth = enemy;

        // =================================================================
        // CLEANUP
        // =================================================================

        private void OnDestroy()
        {
            OnDamageDealt = null;
        }
    }
}