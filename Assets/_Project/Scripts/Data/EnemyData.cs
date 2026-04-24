using System.Collections.Generic;
using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// ScriptableObject defining an enemy's data for battle.
    /// 
    /// Created via Assets > Create > RhythmRogue > EnemyData.
    /// Place in Assets/ScriptableObjects/Enemies/.
    /// 
    /// The battle controller loads an EnemyData to configure:
    ///   - EnemyHealth (maxHP)
    ///   - Conductor (BPM x bpmModifier)
    ///   - Chart system (hybrid or marker-driven)
    ///   - Battle UI (sprite, name, description)
    ///   - Modifiers (applied at battle start)
    /// 
    /// Chart mode priority (auto-detected by BattleManager):
    ///   1. songBeatMap assigned -> marker-driven assembly
    ///   2. AudioClip + patternLibrary -> hybrid assembly
    ///   3. AudioClip only -> pure algorithmic fallback
    ///   4. Legacy JSON fallback
    /// 
    /// GDD section 6 base values:
    ///   Standard enemy: 100 HP, no modifiers
    ///   Elite enemy:    150-200 HP, stronger modifiers
    ///   Boss:           250-400 HP, unique mechanics
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnemy", menuName = "RhythmRogue/EnemyData")]
    public class EnemyData : ScriptableObject
    {
        [Header("Chart")]
        [Tooltip("Pattern library for hybrid chart assembly. " +
                 "Human-crafted patterns placed algorithmically based on audio analysis.")]
        public PatternLibrary patternLibrary;

        [Header("Marker-Driven Chart (optional)")]
        [Tooltip("Beat map for this enemy's song. If assigned, marker-driven assembly " +
                 "is used instead of hybrid. Takes priority over audio analysis.")]
        public SongBeatMap songBeatMap;

        [Tooltip("Base difficulty for charts (0.0 = easy, 1.0 = hardest). " +
                 "Controls which markers become notes and which patterns are available. " +
                 "Elite scaling adds on top.")]
        [Range(0f, 1f)]
        public float markerDifficulty = 0.5f;

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

        [Tooltip("Difficulty tier for pattern selection. 1 = easy, 5 = hardest.")]
        [Range(1, 5)]
        public int chartDifficulty = 1;

        [Header("Visuals")]
        [Tooltip("Enemy sprite displayed during battle.")]
        public Sprite sprite;

        [Header("Modifiers")]
        [Tooltip("Battle modifiers applied when fighting this enemy.")]
        public List<EnemyModifier> modifiers = new();

        /// <summary>Whether this enemy is a boss (HP >= 250).</summary>
        public bool IsBoss => maxHP >= 250;

        /// <summary>Whether this enemy has any active modifiers.</summary>
        public bool HasModifiers => modifiers != null && modifiers.Count > 0;
    }
}