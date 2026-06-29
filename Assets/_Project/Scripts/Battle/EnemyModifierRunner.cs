using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Data;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Owns the live modifiers for one fight and drives their hooks. BattleManager calls into this
    /// at each point in the battle; the runner walks the enemy's modifier list, so BattleManager
    /// never needs to know which modifiers exist and a new modifier type plugs in with no change
    /// here.
    ///
    /// Each authored modifier is a shared ScriptableObject asset, so the runner CLONES it for the
    /// fight: per-fight state lives on the clone and the asset is never mutated. The clones are
    /// destroyed in <see cref="Dispose"/>.
    /// </summary>
    public sealed class EnemyModifierRunner
    {
        private readonly List<EnemyModifier> _live = new();

        public EnemyModifierRunner(IReadOnlyList<EnemyModifier> authored)
        {
            if (authored == null) return;
            for (int i = 0; i < authored.Count; i++)
            {
                EnemyModifier asset = authored[i];
                if (asset == null) continue;
                _live.Add(Object.Instantiate(asset));
            }
        }

        public bool HasAny => _live.Count > 0;

        public void BattleStart(IBattleContext ctx)
        {
            for (int i = 0; i < _live.Count; i++) _live[i].OnBattleStart(ctx);
        }

        public void Update(IBattleContext ctx)
        {
            for (int i = 0; i < _live.Count; i++) _live[i].OnUpdate(ctx);
        }

        /// <summary>Offer the death to each modifier; the first to consume it keeps the enemy alive
        /// and the fight continues. Returns true if the death was consumed.</summary>
        public bool NotifyEnemyWouldDie(IBattleContext ctx)
        {
            for (int i = 0; i < _live.Count; i++)
                if (_live[i].OnEnemyWouldDie(ctx)) return true;
            return false;
        }

        public void BattleEnd(IBattleContext ctx)
        {
            for (int i = 0; i < _live.Count; i++) _live[i].OnBattleEnd(ctx);
        }

        public void Dispose()
        {
            for (int i = 0; i < _live.Count; i++)
                if (_live[i] != null) Object.Destroy(_live[i]);
            _live.Clear();
        }
    }
}
