using UnityEngine;
using RhythmRogue.Util;
using RhythmRogue.Util.Events;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Player health persisting across battles within a run. Singleton via DontDestroyOnLoad.
    /// </summary>
    public class PlayerHealth : Singleton<PlayerHealth>
    {
        [Header("Player HP")]
        [SerializeField] private int _maxHP = 100;

        public HealthComponent Health { get; private set; }
        private IEventBus _eventBus;

        protected override void Awake()
        {
            base.Awake();
            Health = new HealthComponent(_maxHP);
            if (EventBusProvider.Instance != null) _eventBus = EventBusProvider.Instance.Bus;
            Health.OnDamaged += (amount, _) => PublishHPChanged(-amount);
            Health.OnHealed += (amount, _) => PublishHPChanged(amount);
        }

        public int CurrentHP => Health.CurrentHP;
        public int MaxHP => Health.MaxHP;
        public bool IsAlive => Health.IsAlive;
        public float HPPercent => Health.HPPercent;

        public void TakeDamage(int amount) => Health.TakeDamage(amount);
        public void Heal(int amount) => Health.Heal(amount);

        public void IncreaseMaxHP(int amount)
        {
            if (amount <= 0 || Health == null) return;
            Health.SetMaxHP(Health.MaxHP + amount, fillToMax: false);
            Health.Heal(amount);
        }

        // Refill to full AND revive. SetMaxHP clears the death latch when HP ends above 0, so a
        // fresh run starts alive and able to take damage even if the previous run ended in death.
        public void ResetForNewRun() => Health.SetMaxHP(_maxHP, fillToMax: true);

        private void PublishHPChanged(int delta)
        {
            _eventBus?.Publish(new PlayerHpChangedEvent { CurrentHp = Health.CurrentHP, MaxHp = Health.MaxHP, Delta = delta });
        }

        protected override void OnDestroy() { Health?.ClearEvents(); base.OnDestroy(); }
    }
}
