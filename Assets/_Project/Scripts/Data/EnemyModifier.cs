using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// A battle modifier carried by an enemy. Authored as a ScriptableObject asset and dropped into
    /// <see cref="EnemyData.modifiers"/>; an enemy can carry several and they stack. The battle
    /// clones each one per fight (so the shared asset is never mutated and per-fight state is safe),
    /// then calls these hooks at the matching points. Every hook is a no-op by default, so a
    /// concrete modifier overrides only the moments it cares about and stays small.
    ///
    /// Modifiers act only through <see cref="IBattleContext"/>, never through BattleManager, which
    /// is what keeps them modular: a new modifier type is a new subclass asset and needs no change
    /// to the battle code. Built for, among others, a counter-attack (enemy plays notes that bite an
    /// exposed guard) via <see cref="OnBattleStart"/>/<see cref="OnUpdate"/>, and a last stand (the
    /// boss refuses its first death and revives) via <see cref="OnEnemyWouldDie"/>.
    /// </summary>
    public abstract class EnemyModifier : ScriptableObject
    {
        [Tooltip("Display name for UI (e.g. 'Last Stand').")]
        public string modifierName;

        [TextArea]
        [Tooltip("Description shown to the player.")]
        public string description;

        /// <summary>Once, after the chart is loaded and the fight is set up, just before the song
        /// starts. Initialise per-fight state and any up-front scheduling here.</summary>
        public virtual void OnBattleStart(IBattleContext context) { }

        /// <summary>Every frame while the song plays. Key work off <see cref="IBattleContext.SongBeat"/>
        /// so it pauses with the song.</summary>
        public virtual void OnUpdate(IBattleContext context) { }

        /// <summary>The moment the enemy would die. Return true to CONSUME the death (you must keep
        /// the enemy alive, e.g. via <see cref="IBattleContext.HealEnemy"/>) so the fight continues,
        /// or false to let it die normally. The first modifier to consume the death wins.</summary>
        public virtual bool OnEnemyWouldDie(IBattleContext context) => false;

        /// <summary>Once when the fight ends, win or lose, before the clone is destroyed. Undo any
        /// global change here.</summary>
        public virtual void OnBattleEnd(IBattleContext context) { }
    }
}
