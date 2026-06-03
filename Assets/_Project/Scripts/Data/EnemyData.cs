using System.Collections.Generic;
using UnityEngine;

namespace RhythmRogue.Data
{
    [CreateAssetMenu(fileName = "NewEnemy", menuName = "RhythmRogue/EnemyData")]
    public class EnemyData : ScriptableObject
    {
        [Header("Song")]
        [Tooltip("Fallback clip for enemies without a SongBeatMap.")]
        public AudioClip song;

        [Header("Chart")]
        public ShapeLibrary shapeLibrary;
        public SongBeatMap songBeatMap;

        [Tooltip("Which instrument the chart follows. 'All' uses every marker (busiest). " +
                 "Drums/Bass/Melody keep only that stem's markers, so the chart locks to that " +
                 "instrument. Requires a SongBeatMap generated with stems; on an untagged/old " +
                 "beat map only 'All' produces notes. Change freely in the Inspector, no " +
                 "regeneration needed.")]
        public ChartInstrument chartInstrument = ChartInstrument.All;

        [Range(0f, 1f)]
        public float markerDifficulty = 0.5f;

        [Header("Identity")]
        public string enemyName = "Enemy";
        [TextArea] public string description;

        [Header("Stats")]
        public int maxHP = 100;
        [Range(0.5f, 2.0f)] public float bpmModifier = 1.0f;

        [Header("Visuals")]
        public Sprite sprite;

        [Header("Modifiers")]
        public List<EnemyModifier> modifiers = new();

        /// <summary>Prefers SongBeatMap.clip, falls back to song field.</summary>
        public AudioClip EffectiveSong =>
            (songBeatMap != null && songBeatMap.clip != null) ? songBeatMap.clip : song;

        public bool IsBoss => maxHP >= 250;
        public bool HasModifiers => modifiers != null && modifiers.Count > 0;
    }
}
