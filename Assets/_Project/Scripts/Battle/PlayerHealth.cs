using UnityEngine;
using RhythmRogue.Util;
using RhythmRogue.Util.Events;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Player health that persists across battles within a run.
    /// 
    /// Extends Singleton so it survives scene transitions via
    /// DontDestroyOnLoad. Wraps a HealthComponent and bridges
    /// its C# events to the EventBus as PlayerHpChangedEvent.
    /// 
    /// Lifecycle:
    ///   - Created at run start with max HP (default 100, GDD §3.5)
    ///   - Damage taken during battles carries over to the next battle
    ///   - Rest nodes call Heal() to restore a percentage
    ///   - On player death, fires OnDeath — battle controller handles run end
    ///   - On new run, call ResetForNewRun() to restore full HP
    /// </summary>
    public class PlayerHealth : Singleton<PlayerHealth>
    {
        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("Player HP")]
        [Tooltip("Starting/maximum HP for the player. GDD default: 100.")]
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

        protected override void Awake()
        {
            base.Awake();

            Health = new HealthComponent(_maxHP);

            if (EventBusProvider.Instance != null)
                _eventBus = EventBusProvider.Instance.Bus;

            // Bridge HealthComponent events to EventBus
            Health.OnDamaged += (amount, current) => PublishHPChanged(-amount);
            Health.OnHealed += (amount, current) => PublishHPChanged(amount);
        }

        // =================================================================
        // PUBLIC — convenience pass-through
        // =================================================================

        public int CurrentHP => Health.CurrentHP;
        public int MaxHP => Health.MaxHP;
        public bool IsAlive => Health.IsAlive;
        public float HPPercent => Health.HPPercent;

        public void TakeDamage(int amount) => Health.TakeDamage(amount);
        public void Heal(int amount) => Health.Heal(amount);

        /// <summary>
        /// Reset HP to full for a new run.
        /// Call from run manager when starting a fresh run.
        /// </summary>
        public void ResetForNewRun()
        {
            Health.SetMaxHP(_maxHP, fillToMax: true);
        }

        // =================================================================
        // EVENTBUS BRIDGE
        // =================================================================

        private void PublishHPChanged(int delta)
        {
            _eventBus?.Publish(new PlayerHpChangedEvent
            {
                CurrentHp = Health.CurrentHP,
                MaxHp = Health.MaxHP,
                Delta = delta
            });
        }

        // =================================================================
        // CLEANUP
        // =================================================================

        protected override void OnDestroy()
        {
            Health?.ClearEvents();
            base.OnDestroy();
        }
    }
}
