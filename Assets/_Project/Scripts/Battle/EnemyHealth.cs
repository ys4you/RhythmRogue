using System;
using UnityEngine;
using RhythmRogue.Util.Events;
using RhythmRogue.Util;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Enemy health for the current battle. Resets at the start
    /// of each fight — does NOT persist across battles.
    /// 
    /// HP is loaded from enemy data (PROTO-013 will provide EnemyData
    /// ScriptableObject). For prototype, the HP value is serialized
    /// in the Inspector.
    /// 
    /// GDD §3.5 base values:
    ///   Standard enemy: 100 HP
    ///   Elite:          150–200 HP (post-prototype)
    ///   Boss:           250–400 HP
    /// </summary>
    public class EnemyHealth : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("Enemy HP")]
        [Tooltip("Maximum HP for this enemy. Set per-enemy or from EnemyData.")]
        [SerializeField] private int _maxHP = 100;

        // =================================================================
        // HEALTH COMPONENT
        // =================================================================

        /// <summary>The underlying HP pool.</summary>
        public HealthComponent Health { get; private set; }

        // =================================================================
        // STATE
        // =================================================================

        private IEventBus _eventBus;

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Awake()
        {
            if (EventBusProvider.Instance != null)
                _eventBus = EventBusProvider.Instance.Bus;
        }

        // =================================================================
        // PUBLIC — convenience pass-through
        // =================================================================

        public int CurrentHP => Health != null ? Health.CurrentHP : 0;
        public int MaxHP => Health != null ? Health.MaxHP : _maxHP;
        public bool IsAlive => Health != null && Health.IsAlive;
        public float HPPercent => Health != null ? Health.HPPercent : 0f;

        public void TakeDamage(int amount) => Health?.TakeDamage(amount);
        public void Heal(int amount) => Health?.Heal(amount);

        // =================================================================
        // BATTLE SETUP
        // =================================================================

        /// <summary>
        /// Initialize enemy health at the start of a battle.
        /// Call from battle controller before gameplay begins.
        /// </summary>
        /// <param name="maxHP">
        /// Enemy's max HP. Pass from EnemyData ScriptableObject,
        /// or omit to use the serialized Inspector value.
        /// </param>
        public void InitForBattle(int maxHP = -1)
        {
            if (maxHP > 0)
                _maxHP = maxHP;

            // Create fresh health component (old one is discarded)
            Health?.ClearEvents();
            Health = new HealthComponent(_maxHP);

            // Bridge events to EventBus
            Health.OnDamaged += (amount, current) => PublishHPChanged(-amount);
            Health.OnHealed += (amount, current) => PublishHPChanged(amount);

            GameLog.Info($"[EnemyHealth] Initialized: {_maxHP} HP");
        }

        // =================================================================
        // EVENTBUS BRIDGE
        // =================================================================

        private void PublishHPChanged(int delta)
        {
            _eventBus?.Publish(new EnemyHpChangedEvent
            {
                CurrentHp = Health.CurrentHP,
                MaxHp = Health.MaxHP,
                Delta = delta
            });
        }

        // =================================================================
        // CLEANUP
        // =================================================================

        private void OnDestroy()
        {
            Health?.ClearEvents();
        }
    }
}
