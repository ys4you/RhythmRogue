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

        public event Action<DamageResult> OnDamageDealt;

        private PlayerHealth _playerHealth;
        private float _relicBonusPerfectDmg;
        private int _relicMissDmgReduction;

        private void Awake()
        {
            _playerHealth = PlayerHealth.Instance;
            if (_config == null) _config = Resources.Load<DamageConfig>("Configs/DefaultDamage");
            if (_config == null) GameLog.Error("[DamagePipeline] No DamageConfig found!");
        }

        private void OnEnable()
        {
            if (_judgmentSystem != null) _judgmentSystem.OnJudgment += HandleJudgment;
            if (_holdTracker != null) _holdTracker.OnHoldTick += HandleHoldTick;
        }

        private void OnDisable()
        {
            if (_judgmentSystem != null) _judgmentSystem.OnJudgment -= HandleJudgment;
            if (_holdTracker != null) _holdTracker.OnHoldTick -= HandleHoldTick;
        }

        public void ApplyRelicModifiers(float bonusPerfectDmg, int missDmgReduction)
        {
            _relicBonusPerfectDmg = bonusPerfectDmg;
            _relicMissDmgReduction = missDmgReduction;
        }

        public void ClearRelicModifiers() { _relicBonusPerfectDmg = 0f; _relicMissDmgReduction = 0; }

        private void HandleJudgment(JudgmentResult result)
        {
            if (result.Judgment == Judgment.Miss) ApplyPlayerDamage(result);
            else ApplyEnemyDamage(result);
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
        private void OnDestroy() => OnDamageDealt = null;
    }
}
