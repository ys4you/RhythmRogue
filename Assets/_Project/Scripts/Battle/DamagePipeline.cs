using System;
using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Util;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Connects judgment, combo, and health into damage calculations.
    /// Relic bonuses applied at battle start via ApplyRelicModifiers.
    /// </summary>
    [DisallowMultipleComponent]
    public class DamagePipeline : MonoBehaviour
    {
#if UNITY_EDITOR
        [Header("Debug (editor only)")]
        [SerializeField] private bool _godMode = false;
#endif

        [Header("Config")]
        [SerializeField] private DamageConfig _config;

        [Header("References")]
        [SerializeField] private JudgmentSystem _judgmentSystem;
        [SerializeField] private ComboSystem _comboSystem;
        [SerializeField] private EnemyHealth _enemyHealth;
        [SerializeField] private HoldTracker _holdTracker;
        [Tooltip("The enemy's auto-playing highway. Its notes damage the player whenever the " +
                 "guard is down. Wire the scene's EnemyHighway here.")]
        [SerializeField] private EnemyHighway _enemyHighway;

        public event Action<DamageResult> OnDamageDealt;

        /// <summary>
        /// Raised whenever the player's guard flips. True = guarded (enemy notes glance off),
        /// false = exposed (enemy notes bite). Starts true every battle. The combo counter is the
        /// visible proxy for this today; a dedicated HUD guard indicator can subscribe here.
        /// </summary>
        public event Action<bool> OnGuardChanged;

        /// <summary>
        /// True while the player is guarded. The guard starts up, drops on a Miss, and is restored
        /// by the next successful hit. While up, enemy notes deal no damage.
        /// </summary>
        public bool GuardUp => _guardUp;

        private PlayerHealth _playerHealth;
        private float _relicBonusPerfectDmg;
        private int _relicMissDmgReduction;
        private bool _guardUp = true;

        private void Awake()
        {
            _playerHealth = PlayerHealth.Instance;
            if (_config == null) _config = Resources.Load<DamageConfig>("Configs/DefaultDamage");
            if (_config == null) GameLog.Error("[DamagePipeline] No DamageConfig found!");
        }

        private void OnEnable()
        {
            // Every battle begins with the player guarded, so the opening enemy notes always
            // glance off. The guard only opens once the player actually misses (HandleJudgment).
            _guardUp = true;

            if (_judgmentSystem != null) _judgmentSystem.OnJudgment += HandleJudgment;
            if (_holdTracker != null) _holdTracker.OnHoldTick += HandleHoldTick;
            if (_enemyHighway != null) _enemyHighway.OnAutoHit += HandleEnemyAutoHit;
            else GameLog.Warn("[DamagePipeline] No EnemyHighway wired. Enemy notes will deal no " +
                              "damage; assign it on the DamagePipeline in the BattleScene.");
        }

        private void OnDisable()
        {
            if (_judgmentSystem != null) _judgmentSystem.OnJudgment -= HandleJudgment;
            if (_holdTracker != null) _holdTracker.OnHoldTick -= HandleHoldTick;
            if (_enemyHighway != null) _enemyHighway.OnAutoHit -= HandleEnemyAutoHit;
        }

        public void ApplyRelicModifiers(float bonusPerfectDmg, int missDmgReduction)
        {
            _relicBonusPerfectDmg = bonusPerfectDmg;
            _relicMissDmgReduction = missDmgReduction;
        }

        public void ClearRelicModifiers() { _relicBonusPerfectDmg = 0f; _relicMissDmgReduction = 0; }

        private void HandleJudgment(JudgmentResult result)
        {
            if (result.Judgment == Judgment.Miss)
            {
                // A real miss drops the guard. Until the next successful hit lands, every enemy
                // note that reaches its receptor deals damage (see HandleEnemyAutoHit).
                SetGuard(false);
                ApplyPlayerDamage(result);
            }
            else
            {
                // Any successful hit (Bad, Good or Perfect) restores the guard immediately, so a
                // single recovery note closes the window the miss opened.
                SetGuard(true);
                ApplyEnemyDamage(result);
            }
        }

        private void SetGuard(bool up)
        {
            if (_guardUp == up) return;
            _guardUp = up;
            OnGuardChanged?.Invoke(_guardUp);
        }

        /// <summary>
        /// Fired by EnemyHighway when one of its auto-played notes reaches the receptor. While the
        /// guard is up the note glances off harmlessly; while it is down the note bites the player.
        /// This is the enemy's only source of damage, and it is always telegraphed (the note
        /// scrolled in) and always escapable (hit your next note to raise the guard again).
        /// </summary>
        private void HandleEnemyAutoHit(int lane)
        {
            if (_guardUp) return;
            if (_playerHealth == null || !_playerHealth.IsAlive || _config == null) return;

            int dmg = Mathf.Max(0, _config.enemyNoteDamage);
            if (dmg <= 0) return;

#if UNITY_EDITOR
            if (!_godMode)
#endif
                _playerHealth.TakeDamage(dmg);

            OnDamageDealt?.Invoke(new DamageResult(dmg, Judgment.Miss, true, 1f, lane));
        }

        private void ApplyPlayerDamage(JudgmentResult result)
        {
            if (_playerHealth == null || !_playerHealth.IsAlive || _config == null) return;
            int missDamage = Mathf.Max(1, _config.missDamage - _relicMissDmgReduction);

#if UNITY_EDITOR
            if (!_godMode)
#endif
                _playerHealth.TakeDamage(missDamage);

            OnDamageDealt?.Invoke(new DamageResult(missDamage, Judgment.Miss, true, 1f, result.Lane));
        }

        private void ApplyEnemyDamage(JudgmentResult result)
        {
            if (_enemyHealth == null || !_enemyHealth.IsAlive || _config == null) return;

            int baseDamage = _config.GetEnemyDamage((int)result.Judgment);
            if (result.Judgment == Judgment.Perfect && _relicBonusPerfectDmg > 0f)
                baseDamage += Mathf.RoundToInt(_relicBonusPerfectDmg);

            float multiplier = _comboSystem != null ? _comboSystem.Multiplier : 1f;
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));
            _enemyHealth.TakeDamage(finalDamage);

            OnDamageDealt?.Invoke(new DamageResult(finalDamage, result.Judgment, false, multiplier, result.Lane));
        }

        private void HandleHoldTick(HoldState state)
        {
            if (_enemyHealth == null || !_enemyHealth.IsAlive || _config == null) return;
            float multiplier = _comboSystem != null ? _comboSystem.Multiplier : 1f;
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(_config.holdTickDamage * multiplier));
            _enemyHealth.TakeDamage(finalDamage);
            OnDamageDealt?.Invoke(new DamageResult(finalDamage, Judgment.Perfect, false, multiplier, state.Lane));
        }

        public void SetConfig(DamageConfig config) => _config = config;
        public void SetEnemyHealth(EnemyHealth enemy) => _enemyHealth = enemy;

        private void OnDestroy()
        {
            OnDamageDealt = null;
            OnGuardChanged = null;
        }
    }
}
