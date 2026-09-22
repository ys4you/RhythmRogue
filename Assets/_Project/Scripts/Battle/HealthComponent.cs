using System;
using UnityEngine;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Pure C# HP pool with damage, healing, clamping, and death events.
    /// Not a MonoBehaviour. PlayerHealth and EnemyHealth wrap this.
    /// </summary>
    public class HealthComponent
    {
        private int _currentHP;
        private int _maxHP;
        private bool _isDead;

        public int CurrentHP => _currentHP;
        public int MaxHP => _maxHP;
        public bool IsAlive => _currentHP > 0 && !_isDead;
        public float HPPercent => _maxHP > 0 ? _currentHP / (float)_maxHP : 0f;

        public event Action<int, int> OnDamaged;
        public event Action<int, int> OnHealed;
        public event Action OnDeath;
        public event Action<int, int> OnHPChanged;

        public HealthComponent(int maxHP)
        {
            _maxHP = Mathf.Max(1, maxHP);
            _currentHP = _maxHP;
        }

        public void TakeDamage(int amount)
        {
            if (_isDead || amount <= 0) return;
            _currentHP = Mathf.Max(0, _currentHP - amount);
            OnDamaged?.Invoke(amount, _currentHP);
            OnHPChanged?.Invoke(_currentHP, _maxHP);
            if (_currentHP <= 0) { _isDead = true; OnDeath?.Invoke(); }
        }

        public void Heal(int amount)
        {
            if (_isDead || !IsAlive || amount <= 0) return;
            int before = _currentHP;
            _currentHP = Mathf.Min(_maxHP, _currentHP + amount);
            int actual = _currentHP - before;
            if (actual <= 0) return;
            OnHealed?.Invoke(actual, _currentHP);
            OnHPChanged?.Invoke(_currentHP, _maxHP);
        }

        /// <summary>
        /// Bring a dead pool back at the given HP. This is a deliberate, controlled revival (a
        /// last-stand modifier refusing a boss's first death), kept separate from <see cref="Heal"/>
        /// on purpose: Heal refuses to touch a dead pool so a stray heal can never quietly undo a
        /// death. Clears the death latch so TakeDamage works again. No-op when the pool is still
        /// alive or hp is not positive, so calling it defensively is safe.
        /// </summary>
        public void Revive(int hp)
        {
            if (!_isDead || hp <= 0) return;
            _isDead = false;
            _currentHP = Mathf.Clamp(hp, 1, _maxHP);
            // Reported as a heal from zero so HP bars and the event bus see the refill.
            OnHealed?.Invoke(_currentHP, _currentHP);
            OnHPChanged?.Invoke(_currentHP, _maxHP);
        }

        public void SetMaxHP(int newMax, bool fillToMax = false)
        {
            _maxHP = Mathf.Max(1, newMax);
            _currentHP = fillToMax ? _maxHP : Mathf.Min(_currentHP, _maxHP);
            // Keep the death latch consistent with HP: you cannot be dead with positive HP.
            // Without this, a pool that died last run (the player) stays flagged dead after being
            // refilled here, so IsAlive is false and TakeDamage no-ops forever. That was the
            // "no damage after retry" bug: full HP bar, total invincibility on the next run.
            if (_currentHP > 0) _isDead = false;
            OnHPChanged?.Invoke(_currentHP, _maxHP);
        }

        public void SetCurrentHP(int hp)
        {
            _currentHP = Mathf.Clamp(hp, 0, _maxHP);
            OnHPChanged?.Invoke(_currentHP, _maxHP);
            if (_currentHP <= 0) OnDeath?.Invoke();
        }

        public void ResetToFull()
        {
            _currentHP = _maxHP;
            _isDead = false;
            OnHPChanged?.Invoke(_currentHP, _maxHP);
        }

        public void ClearEvents() { OnDamaged = null; OnHealed = null; OnDeath = null; OnHPChanged = null; }
    }
}
