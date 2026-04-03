using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Configuration for elite enemy scaling.
    /// 
    /// A single asset defines all elite modifications. Applied at battle
    /// init time by BattleManager when the selected node is NodeType.Elite.
    /// The base EnemyData is never mutated — scaling is runtime-only.
    /// 
    /// Design rationale: one config for the entire game means elite
    /// difficulty is consistent and tunable from a single place. When
    /// you add 10 enemy types, you don't need 10 elite variants —
    /// any enemy becomes elite automatically.
    /// 
    /// GDD §6: "Elite variants of each enemy type apply stronger versions
    /// of their base modifiers."
    /// 
    /// Create via: Assets → Create → RhythmRogue → Data → Elite Config
    /// </summary>
    [CreateAssetMenu(
        fileName = "EliteConfig",
        menuName = "RhythmRogue/Data/Elite Config",
        order = 30)]
    public class EliteConfig : ScriptableObject
    {
        [Header("Health")]
        [Tooltip("Multiplier applied to the base enemy's maxHP. GDD: 1.5–2.0×.")]
        [Range(1f, 3f)]
        public float hpMultiplier = 1.75f;

        [Header("Rhythm")]
        [Tooltip("Added to the enemy's bpmModifier. E.g. 0.1 = 10% faster tempo.")]
        [Range(0f, 0.5f)]
        public float bpmModifierBoost = 0.1f;

        [Tooltip("Added to the chart template's max difficulty per section. " +
                 "Higher = denser, harder patterns selected.")]
        [Range(0, 3)]
        public int difficultyBoost = 1;

        [Header("Damage")]
        [Tooltip("Extra damage the player takes on Miss (added to base miss damage).")]
        [Min(0)]
        public int extraMissDamage = 2;

        [Header("Rewards")]
        [Tooltip("Number of relic options offered after an elite victory (normal = 3).")]
        [Range(2, 5)]
        public int rewardOptionCount = 3;

        [Tooltip("Bonus Beats currency multiplier for elite victories. 1.5 = +50%.")]
        [Range(1f, 3f)]
        public float currencyMultiplier = 1.5f;

        // =================================================================
        // QUERIES — used by BattleManager at init time
        // =================================================================

        /// <summary>
        /// Calculate elite HP from a base enemy's maxHP.
        /// </summary>
        public int ScaleHP(int baseHP)
        {
            return Mathf.RoundToInt(baseHP * hpMultiplier);
        }

        /// <summary>
        /// Calculate elite BPM modifier from a base enemy's bpmModifier.
        /// </summary>
        public float ScaleBPMModifier(float baseModifier)
        {
            return baseModifier + bpmModifierBoost;
        }
    }
}
