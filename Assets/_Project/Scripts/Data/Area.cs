using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Represents one area/biome of a run. Contains the enemy pools, the boss pool, theme color,
    /// and the area's difficulty band.
    ///
    /// Difficulty model: the area is a band. Every Enemy node's chart difficulty is interpolated
    /// between <see cref="difficultyFloor"/> (the opener) and <see cref="difficultyCeil"/> (the
    /// last pre-boss node) by how deep the node sits, then nudged by the enemy's small flavour
    /// tilt and the run's tier (see DifficultyCurve). Enemies carry character, the slot sets the
    /// level. Fight length (HP) ramps the same way from <see cref="baseEnemyHP"/>.
    ///
    /// Each Area is a self-contained piece of run content. Adding a new area = adding a new
    /// ScriptableObject; a harder area just uses higher floor/ceil/HP numbers.
    /// </summary>
    [CreateAssetMenu(fileName = "Area", menuName = "RhythmRogue/Data/Area")]
    public class Area : ScriptableObject
    {
        [Header("Identity")]
        public string areaName = "The Cult Sanctum";
        [TextArea] public string flavorText;
        public Color themeColor = new(0.694f, 0.357f, 0.208f); // RustOrange default

        [Header("Enemy Pools")]
        [Tooltip("Pool used to populate Enemy nodes.")]
        public EnemyPool basicEnemies;

        [Tooltip("Pool used to populate Elite nodes. Often the same enemies as basic with stronger stats, or a dedicated elite pool.")]
        public EnemyPool eliteEnemies;

        [Tooltip("Pool of possible bosses for the area. One is picked per run.")]
        public EnemyPool bosses;

        [Header("Difficulty band (chart density, 0-1)")]
        [Tooltip("Chart difficulty at the area's opening node (shallowest). This is the first thing " +
                 "a new player meets, so for area 1 keep it low. Beginners' on-ramp.")]
        [Range(0f, 1f)] public float difficultyFloor = 0.15f;

        [Tooltip("Chart difficulty at the area's last pre-boss node (deepest). The area's ceiling.")]
        [Range(0f, 1f)] public float difficultyCeil = 0.42f;

        [Tooltip("Chart difficulty for this area's boss.")]
        [Range(0f, 1f)] public float bossDifficulty = 0.48f;

        [Header("Fight length (enemy HP)")]
        [Tooltip("Enemy HP at the area's opening node. Higher = longer fights. Short early fights " +
                 "give fast wins and reduce rage-quit, so keep area 1 low.")]
        [Min(1)] public int baseEnemyHP = 150;

        [Tooltip("Fractional HP growth from the opener to the last pre-boss node. " +
                 "0.6 = the deepest basic fight has 60% more HP than the opener.")]
        [Range(0f, 3f)] public float hpDepthGain = 0.6f;

        [Tooltip("Boss HP for this area.")]
        [Min(1)] public int bossHP = 380;
    }
}
