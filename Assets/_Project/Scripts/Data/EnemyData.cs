using System.Collections.Generic;
using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// ScriptableObject defining an enemy's data for battle.
    /// 
    /// Created via Assets → Create → RhythmRogue → EnemyData.
    /// Place in Assets/ScriptableObjects/Enemies/.
    /// 
    /// The battle controller loads an EnemyData to configure:
    ///   - EnemyHealth (maxHP)
    ///   - Conductor (BPM × bpmModifier)
    ///   - Chart generator (chartDifficulty) — post-prototype
    ///   - Battle UI (sprite, name, description)
    ///   - Modifiers (applied at battle start) — post-prototype
    /// 
    /// GDD §6 base values:
    ///   Standard enemy: 100 HP, no modifiers
    ///   Elite enemy:    150-200 HP, stronger modifiers
    ///   Boss:           250-400 HP, unique mechanics
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnemy", menuName = "RhythmRogue/EnemyData")]
    public class EnemyData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Display name shown in battle UI.")]
        public string enemyName = "Enemy";

        [TextArea]
        [Tooltip("Flavor text shown in UI or map tooltips.")]
        public string description;

        [Header("Stats")]
        [Tooltip("Maximum HP for this enemy. Standard: 100, Elite: 150-200, Boss: 250-400.")]
        public int maxHP = 100;

        [Tooltip("Multiplier applied to the song's base BPM. 1.0 = no change, 1.2 = 20% faster.")]
        [Range(0.5f, 2.0f)]
        public float bpmModifier = 1.0f;

        [Tooltip("Difficulty tier for chart pattern selection. 1 = easy, 5 = hardest.")]
        [Range(1, 5)]
        public int chartDifficulty = 1;

        [Header("Visuals")]
        [Tooltip("Enemy sprite displayed during battle.")]
        public Sprite sprite;

        [Header("Modifiers (post-prototype)")]
        [Tooltip("Battle modifiers applied when fighting this enemy. Empty for prototype.")]
        public List<EnemyModifier> modifiers = new();

        /// <summary>Whether this enemy is a boss (HP >= 250).</summary>
        public bool IsBoss => maxHP >= 250;

        /// <summary>Whether this enemy has any active modifiers.</summary>
        public bool HasModifiers => modifiers != null && modifiers.Count > 0;
    }
}
