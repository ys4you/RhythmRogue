using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Base class for enemy battle modifiers.
    /// 
    /// Each enemy can carry a list of modifiers that alter the
    /// battle when applied. Examples (post-prototype):
    ///   - LaneMirrorModifier: flip the chart horizontally
    ///   - BPMRampModifier: increase BPM over the course of the song
    ///   - PatternSwapModifier: swap note patterns mid-song
    ///   - ChaosMixModifier: random chart modifiers each phase
    /// 
    /// GDD §6 enemy types:
    ///   Slow Enemy:  no modifier
    ///   Speed Enemy: shorter reaction time
    ///   Trick Enemy: lane mirroring, pattern swaps
    ///   Chaos Enemy: random modifiers each phase
    /// 
    /// For the prototype, this class exists only as architecture.
    /// No concrete modifiers are implemented yet.
    /// </summary>
    public abstract class EnemyModifier : ScriptableObject
    {
        [Tooltip("Display name for UI (e.g. 'Lane Mirror').")]
        public string modifierName;

        [TextArea]
        [Tooltip("Description shown to the player.")]
        public string description;

        /// <summary>
        /// Apply this modifier to the current battle.
        /// Called by the battle controller after loading the enemy.
        /// 
        /// Post-prototype: receives a BattleContext with references
        /// to the Conductor, NoteHighway, chart data, etc.
        /// </summary>
        public abstract void Apply();

        /// <summary>
        /// Remove this modifier when the battle ends.
        /// </summary>
        public abstract void Remove();
    }
}
