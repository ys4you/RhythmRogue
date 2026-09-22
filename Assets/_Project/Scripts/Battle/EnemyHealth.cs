using UnityEngine;
using RhythmRogue.Util.Events;
using RhythmRogue.Util;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Enemy health for the current battle. Resets each fight via InitForBattle.
    /// </summary>
    public class EnemyHealth : MonoBehaviour
    {
        [Header("Enemy HP")]
        [SerializeField] private int _maxHP = 100;

        public HealthComponent Health { get; private set; }
        private IEventBus _eventBus;

        private void Awake()
        {
            if (EventBusProvider.Instance != null) _eventBus = EventBusProvider.Instance.Bus;
        }

        public int CurrentHP => Health != null ? Health.CurrentHP : 0;
        public int MaxHP => Health != null ? Health.MaxHP : _maxHP;
        public bool IsAlive => Health != null && Health.IsAlive;
        public float HPPercent => Health != null ? Health.HPPercent : 0f;

        public void TakeDamage(int amount) => Health?.TakeDamage(amount);
        public void Heal(int amount) => Health?.Heal(amount);

        /// <summary>Revive the enemy at the given HP after a death was consumed by a modifier.
        /// See <see cref="HealthComponent.Revive"/>; Heal deliberately will not do this.</summary>
        public void Revive(int hp) => Health?.Revive(hp);

        public void InitForBattle(int maxHP = -1)
        {
            if (maxHP > 0) _maxHP = maxHP;
            Health?.ClearEvents();
            Health = new HealthComponent(_maxHP);
            Health.OnDamaged += (amount, _) => PublishHPChanged(-amount);
            Health.OnHealed += (amount, _) => PublishHPChanged(amount);
            GameLog.Info($"[EnemyHealth] Initialized: {_maxHP} HP");
        }

        private void PublishHPChanged(int delta)
        {
            _eventBus?.Publish(new EnemyHpChangedEvent { CurrentHp = Health.CurrentHP, MaxHp = Health.MaxHP, Delta = delta });
        }

        private void OnDestroy() => Health?.ClearEvents();
    }
}
