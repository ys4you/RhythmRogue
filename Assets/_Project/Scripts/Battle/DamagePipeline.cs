using System;
using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Util;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Connects judgment, combo, and health into a unified damage pipeline.
    /// 
    /// Relic bonuses:
    ///   - BonusPerfectDamage: flat damage added to Perfect hits (before multiplier)
    ///   - ReduceMissDamage: flat reduction to miss damage (minimum 1)
    /// </summary>
    [DisallowMultipleComponent]
    public class DamagePipeline : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR
        // =================================================================
        [Header("Debug")]
        [SerializeField] private bool _godMode = false;

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

        // Relic bonuses — applied at battle start
        private float _relicBonusPerfectDmg;
        private int _relicMissDmgReduction;

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Awake()
        {
            _playerHealth = PlayerHealth.Instance;

            if (_config == null)
                _config = Resources.Load<DamageConfig>("Configs/DefaultDamage");

            if (_config == null)
                GameLog.Error("[DamagePipeline] No DamageConfig found! Assign in Inspector or place in Resources/DefaultDamage.");
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
        // RELIC MODIFIERS
        // =================================================================

        /// <summary>
        /// Apply relic bonuses to the damage pipeline.
        /// Called by BattleManager at battle start.
        /// </summary>
        /// <param name="bonusPerfectDmg">
        /// Flat bonus damage added to Perfect hits before combo multiplier.
        /// </param>
        /// <param name="missDmgReduction">
        /// Flat damage reduction on Miss. Final miss damage is
        /// max(1, baseMissDamage - reduction).
        /// </param>
        public void ApplyRelicModifiers(float bonusPerfectDmg, int missDmgReduction)
        {
            _relicBonusPerfectDmg = bonusPerfectDmg;
            _relicMissDmgReduction = missDmgReduction;

            if (bonusPerfectDmg > 0f || missDmgReduction > 0)
                GameLog.Info($"[DamagePipeline] Relic bonus: PerfDmg+{bonusPerfectDmg}, MissRed-{missDmgReduction}");
        }

        /// <summary>Clear relic bonuses.</summary>
        public void ClearRelicModifiers()
        {
            _relicBonusPerfectDmg = 0f;
            _relicMissDmgReduction = 0;
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

            // Apply miss damage with relic reduction (minimum 1)
            int missDamage = Mathf.Max(1, _config.missDamage - _relicMissDmgReduction);

            if (!_godMode)
                _playerHealth.TakeDamage(missDamage);

            OnDamageDealt?.Invoke(new DamageResult(
                amount: missDamage,
                judgment: Judgment.Miss,
                isPlayerDamage: true,
                multiplier: 1f,
                lane: result.Lane));
        }

        private void ApplyEnemyDamage(JudgmentResult result)
        {
            if (_enemyHealth == null || !_enemyHealth.IsAlive || _config == null) return;

            int baseDamage = _config.GetEnemyDamage((int)result.Judgment);

            // Apply relic bonus on Perfect hits
            if (result.Judgment == Judgment.Perfect && _relicBonusPerfectDmg > 0f)
                baseDamage += Mathf.RoundToInt(_relicBonusPerfectDmg);

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
