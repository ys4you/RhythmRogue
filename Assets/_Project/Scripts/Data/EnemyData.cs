using System.Collections.Generic;
using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// ScriptableObject defining an enemy's data for battle.
    /// 
    /// Created via Assets > Create > RhythmRogue > EnemyData.
    /// 
    /// Chart generation priority (auto-detected by BattleManager):
    ///   1. songBeatMap assigned -> ShapeAssembler uses beat map markers
    ///   2. AudioClip available -> RuntimeBeatAnalyzer -> ShapeAssembler
    ///   3. Legacy JSON fallback
    /// 
    /// Both paths use the ShapeLibrary for lane placement.
    /// The seed controls which shapes are selected per phrase,
    /// so the same song produces different charts per run.
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
        [Tooltip("Shape library for lane placement. The assembler picks shapes " +
                 "from this library based on difficulty and seed. If null, " +
                 "BattleManager uses the default library.")]
        public ShapeLibrary shapeLibrary;

        [Tooltip("Beat map for this enemy's song. If assigned, the assembler " +
                 "uses these hand-authored timing markers instead of auto-detecting " +
                 "from audio. This produces the best results for curated songs.")]
        public SongBeatMap songBeatMap;

        [Tooltip("Base difficulty (0.0 = easy, 1.0 = hardest). " +
                 "Controls how many markers become notes and which shapes are available. " +
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