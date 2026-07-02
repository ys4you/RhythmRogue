using System.Collections.Generic;
using UnityEngine;

namespace RhythmRogue.Data
{
    [CreateAssetMenu(fileName = "NewEnemy", menuName = "RhythmRogue/EnemyData")]
    public class EnemyData : ScriptableObject
    {
        [Header("Chart")]
        [Tooltip("Fragment library for the human-feel PatternAssembler. When set (or a default is " +
                 "set on ChartProvider), the chart is built from authored rhythm fragments placed " +
                 "on a bar grid. Leave null with a shapeLibrary assigned to use the older " +
                 "lane-shape system instead.")]
        public NotePatternLibrary patternLibrary;
        public ShapeLibrary shapeLibrary;
        public SongBeatMap songBeatMap;

        [Tooltip("Which instrument the chart follows. 'All' uses every marker (busiest). " +
                 "Drums/Bass/Melody keep only that stem's markers, so the chart locks to that " +
                 "instrument. Requires a SongBeatMap generated with stems; on an untagged/old " +
                 "beat map only 'All' produces notes. Change freely in the Inspector, no " +
                 "regeneration needed.")]
        public ChartInstrument chartInstrument = ChartInstrument.All;

        [Header("Difficulty flavour (relative to the slot)")]
        [Tooltip("Small tilt added to the position-driven chart difficulty. + = a bit denser than " +
                 "the node's baseline, - = a bit sparser. Keep it small (roughly -0.15..0.15): the " +
                 "Area and node depth set the actual difficulty, this only gives the enemy character. " +
                 "The same enemy is gentle at an area's opener and tough deep in a later area.")]
        [Range(-0.3f, 0.3f)] public float difficultyFlavor = 0f;

        [Tooltip("Relative fight length. 1 = the area's normal HP for this depth; >1 tankier, " +
                 "<1 quicker to kill.")]
        [Range(0.25f, 3f)] public float hpFlavor = 1f;

        [Tooltip("Earliest normalized map depth (0 = opener, 1 = pre-boss) at which this enemy may " +
                 "appear on a node. Gate harder-feeling enemies to later layers so the opener can't " +
                 "roll them. 0 = can appear anywhere. (Difficulty is depth-driven anyway, so this is " +
                 "mostly for controlling where each enemy's flavour shows up.)")]
        [Range(0f, 1f)] public float minDepthT = 0f;

        [Header("Identity")]
        public string enemyName = "Enemy";
        [TextArea] public string description;

        [Tooltip("Marks this enemy as a boss (boss label + boss reward routing). Set this explicitly; " +
                 "it is NOT inferred from HP. In a real run the map's Boss node type is authoritative; " +
                 "this flag is the fallback when a battle is launched directly without a node.")]
        public bool isBoss = false;

        [Range(0.5f, 2.0f)]
        [Tooltip("Enemy tempo character: multiplies the song BPM. Higher floods more notes per second " +
                 "(reaction time is fixed by scroll speed). Keep area-1 enemies at or below ~1.0 so " +
                 "the opening fights stay readable for new players.")]
        public float bpmModifier = 1.0f;

        [Header("Fallback (used only with NO area/run context)")]
        [Tooltip("Chart difficulty used ONLY when there is no Area context, e.g. launching the Battle " +
                 "scene directly to test. In a real run the Area + node depth drive difficulty via " +
                 "DifficultyCurve and this is ignored. Set difficultyFlavor above for run behaviour.")]
        [Range(0f, 1f)] public float markerDifficulty = 0.4f;

        [Tooltip("Enemy HP used ONLY when there is no Area context (direct battle-scene launch). In a " +
                 "real run the Area + node depth drive HP and this is ignored. Set hpFlavor above for " +
                 "run behaviour.")]
        public int maxHP = 150;

        [Header("Visuals")]
        public Sprite sprite;

        [Tooltip("Optional directional character art (idle + per-lane sing poses). Leave null and the " +
                 "enemy poses the single sprite above (bob + lean, no extra art). Assign a Character " +
                 "Visual to give this enemy real directional frames; any pose you have not drawn yet " +
                 "falls back to posing the sprite above, so a partial config is safe.")]
        public CharacterVisualConfig visual;

        [Header("Modifiers")]
        public List<EnemyModifier> modifiers = new();

        /// <summary>The battle audio. Lives on the SongBeatMap so the beat map and its song are one
        /// swappable unit; null if no beat map is assigned.</summary>
        public AudioClip EffectiveSong => songBeatMap != null ? songBeatMap.clip : null;

        public bool IsBoss => isBoss;
        public bool HasModifiers => modifiers != null && modifiers.Count > 0;
    }
}
