using System;
using UnityEngine;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Manages a single HP pool with damage, healing, clamping, and death.
    /// 
    /// Pure C# class — not a MonoBehaviour. This keeps health logic
    /// testable and reusable without requiring a GameObject. The player
    /// and enemy each own an instance, wrapped in thin MonoBehaviours
    /// that handle persistence and EventBus publishing.
    /// 
    /// All HP changes go through TakeDamage/Heal which enforce:
    ///   - HP never below 0
    ///   - HP never above MaxHP
    ///   - No damage/healing after death
    ///   - Events fire after the value changes (not before)
    /// 
    /// SOLID breakdown:
    /// - S: Only manages HP state and fires events. No damage calculation.
    /// - O: Extended by wrapping, not by modifying (PlayerHealth, EnemyHealth).
    /// - L: Any consumer of HP events works with any HealthComponent.
    /// - I: Focused events per change type.
    /// - D: No dependencies on other systems.
    /// </summary>
    public class HealthComponent
    {
        // =================================================================
        // STATE
        // =================================================================

        private int _currentHP;
        private int _maxHP;

        // =================================================================
        // PROPERTIES
        // =================================================================

        /// <summary>Current hit points.</summary>
        public int CurrentHP => _currentHP;

        /// <summary>Maximum hit points.</summary>
        public int MaxHP => _maxHP;

        private bool _isDead;


        /// <summary>Whether this entity is alive (HP > 0).</summary>
        public bool IsAlive => _currentHP > 0 && !_isDead;

        /// <summary>HP as a 0.0–1.0 fraction for UI bars.</summary>
        public float HPPercent => _maxHP > 0 ? _currentHP / (float)_maxHP : 0f;

        // =================================================================
        // EVENTS
        // =================================================================

        /// <summary>Fired after taking damage. Parameters: amount dealt, current HP after.</summary>
        public event Action<int, int> OnDamaged;

        /// <summary>Fired after healing. Parameters: amount healed, current HP after.</summary>
        public event Action<int, int> OnHealed;

        /// <summary>Fired when HP reaches 0. Fires once — subsequent damage is ignored.</summary>
        public event Action OnDeath;

        /// <summary>
        /// Fired on any HP change (damage, heal, or max HP change).
        /// Parameters: current HP, max HP.
        /// Generic event for UI binding.
        /// </summary>
        public event Action<int, int> OnHPChanged;

        // =================================================================
        // CONSTRUCTOR
        // =================================================================

        /// <summary>
        /// Create a health component with the given max HP, starting at full.
        /// </summary>
        /// <param name="maxHP">Maximum hit points.</param>
        public HealthComponent(int maxHP)
        {
            _maxHP = Mathf.Max(1, maxHP);
            _currentHP = _maxHP;
        }

        // =================================================================
        // DAMAGE
        // =================================================================

        /// <summary>
        /// Reduce HP by the given amount. Clamps to 0.
        /// Does nothing if already dead.
        /// </summary>
        /// <param name="amount">Damage to deal (positive).</param>
        public void TakeDamage(int amount)
        {
            if (_isDead) return;
            if (amount <= 0) return;

            _currentHP = Mathf.Max(0, _currentHP - amount);
            OnDamaged?.Invoke(amount, _currentHP);
            OnHPChanged?.Invoke(_currentHP, _maxHP);

            if (_currentHP <= 0)
            {
                _isDead = true;
                OnDeath?.Invoke();
            }
        }

        // =================================================================
        // HEALING
        // =================================================================

        /// <summary>
        /// Increase HP by the given amount. Clamps to max.
        /// Does nothing if dead (cannot heal from 0).
        /// </summary>
        /// <param name="amount">Amount to heal (positive).</param>
        public void Heal(int amount)
        {
            if (_isDead) 
                return;
            if (!IsAlive) 
                return;
            if (amount <= 0) 
                return;

            int before = _currentHP;
            _currentHP = Mathf.Min(_maxHP, _currentHP + amount);

            int actualHeal = _currentHP - before;
            if (actualHeal <= 0) return;

            OnHealed?.Invoke(actualHeal, _currentHP);
            OnHPChanged?.Invoke(_currentHP, _maxHP);
        }

        // =================================================================
        // MAX HP
        // =================================================================

        /// <summary>
        /// Set the maximum HP. Optionally fills to the new max.
        /// </summary>
        /// <param name="newMax">New maximum HP (minimum 1).</param>
        /// <param name="fillToMax">If true, current HP is set to the new max.</param>
        public void SetMaxHP(int newMax, bool fillToMax = false)
        {
            _maxHP = Mathf.Max(1, newMax);

            if (fillToMax)
            {
                _currentHP = _maxHP;
            }
            else
            {
                // Clamp current to new max if it exceeds
                _currentHP = Mathf.Min(_currentHP, _maxHP);
            }

            OnHPChanged?.Invoke(_currentHP, _maxHP);
        }

        /// <summary>
        /// Set current HP directly. Use for loading saved state.
        /// Clamps between 0 and max.
        /// </summary>
        /// <param name="hp">HP to set.</param>
        public void SetCurrentHP(int hp)
        {
            _currentHP = Mathf.Clamp(hp, 0, _maxHP);
            OnHPChanged?.Invoke(_currentHP, _maxHP);

            if (_currentHP <= 0)
                OnDeath?.Invoke();
        }

        /// <summary>
        /// Reset to full HP. Used at run start.
        /// </summary>
        public void ResetToFull()
        {
            _currentHP = _maxHP;
            OnHPChanged?.Invoke(_currentHP, _maxHP);
        }

        // =================================================================
        // CLEANUP
        // =================================================================

        /// <summary>
        /// Clear all event subscribers.
        /// Call when the owning entity is destroyed.
        /// </summary>
        public void ClearEvents()
        {
            OnDamaged = null;
            OnHealed = null;
            OnDeath = null;
            OnHPChanged = null;
        }
    }
}
