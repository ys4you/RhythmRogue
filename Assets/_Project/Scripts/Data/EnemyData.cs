using System.Collections.Generic;
using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// ScriptableObject defining an enemy's data for battle.
    /// 
    /// Created via Assets > Create > RhythmRogue > EnemyData.
    /// 
    /// Song assignment priority:
    ///   1. songBeatMap.clip (curated songs with beat data)
    ///   2. song field (imported songs without beat data, auto-detect path)
    /// 
    /// Chart generation priority (auto-detected by BattleManager):
    ///   1. songBeatMap assigned -> ShapeAssembler uses beat map markers
    ///   2. AudioClip available -> RuntimeBeatAnalyzer -> ShapeAssembler
    ///   3. Legacy JSON fallback
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnemy", menuName = "RhythmRogue/EnemyData")]
    public class EnemyData : ScriptableObject
    {
        [Header("Song")]
        [Tooltip("Fallback audio clip. Only needed if this enemy has no SongBeatMap. " +
                 "When a SongBeatMap is assigned, the clip is read from there instead.")]
        public AudioClip song;

        [Header("Chart")]
        [Tooltip("Shape library for lane placement. If null, BattleManager uses the default.")]
        public ShapeLibrary shapeLibrary;

        [Tooltip("Beat map for this enemy's song. If assigned, the assembler uses " +
                 "these markers and the beat map's AudioClip for timing.")]
        public SongBeatMap songBeatMap;

        [Tooltip("Base difficulty (0.0 = easy, 1.0 = hardest).")]
        [Range(0f, 1f)]
        public float markerDifficulty = 0.5f;

        [Header("Identity")]
        public string enemyName = "Enemy";

        [TextArea]
        public string description;

        [Header("Stats")]
        public int maxHP = 100;

        [Range(0.5f, 2.0f)]
        public float bpmModifier = 1.0f;

        [Header("Visuals")]
        public Sprite sprite;

        [Header("Modifiers")]
        public List<EnemyModifier> modifiers = new();

        /// <summary>
        /// The effective AudioClip for this enemy.
        /// Prefers SongBeatMap.clip, falls back to the song field.
        /// </summary>
        public AudioClip EffectiveSong =>
            (songBeatMap != null && songBeatMap.clip != null)
                ? songBeatMap.clip
                : song;

        public bool IsBoss => maxHP >= 250;
        public bool HasModifiers => modifiers != null && modifiers.Count > 0;
    }
}
