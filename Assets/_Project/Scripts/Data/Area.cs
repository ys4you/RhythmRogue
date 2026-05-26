using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Represents one area/biome of a run. Contains the enemy pools,
    /// the boss pool, theme color, and any area-wide difficulty knobs.
    ///
    /// Each Area is a self-contained piece of run content. Adding a new
    /// area = adding a new ScriptableObject. MapScreen takes an Area
    /// reference instead of individual EnemyData references.
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

        [Header("Difficulty")]
        [Tooltip("Applied to enemy HP for this area (1.0 = baseline).")]
        [Range(0.5f, 3f)] public float hpMultiplier = 1f;

        [Tooltip("Applied to chart difficulty for this area (0.0 - 1.0 range bias).")]
        [Range(-0.3f, 0.3f)] public float difficultyBias = 0f;
    }
}
