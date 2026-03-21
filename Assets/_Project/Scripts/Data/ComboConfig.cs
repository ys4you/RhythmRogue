using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Combo system configuration.
    /// 
    /// ScriptableObject so designers can:
    ///   - Tune combo scaling without touching code
    ///   - Create relic variants (e.g. "Combo Crown" uses a config with higher cap)
    ///   - Adjust milestone thresholds for feedback pacing
    /// 
    /// GDD §3.4 defaults: +0.1x per hit, capped at 3.0x, milestones at 10/25/50/100.
    /// 
    /// Create via: Assets → Create → RhythmRogue → ComboConfig
    /// </summary>
    [CreateAssetMenu(fileName = "ComboConfig", menuName = "RhythmRogue/ComboConfig")]
    public class ComboConfig : ScriptableObject
    {
        [Header("Multiplier")]
        [Tooltip("Multiplier increase per consecutive hit. GDD default: 0.1 = +10% per hit.")]
        [Range(0.01f, 0.5f)]
        public float multiplierPerHit = 0.1f;

        [Tooltip("Maximum multiplier cap. GDD default: 3.0 = reached at 20 consecutive hits.")]
        [Range(1f, 10f)]
        public float maxMultiplier = 3.0f;

        [Header("Milestones")]
        [Tooltip("Combo thresholds that trigger milestone events (for UI effects, relic triggers).")]
        public int[] milestoneThresholds = { 10, 25, 50, 100 };

        /// <summary>
        /// Calculate multiplier for a given combo count.
        /// </summary>
        public float GetMultiplier(int combo)
        {
            return Mathf.Min(1f + combo * multiplierPerHit, maxMultiplier);
        }
    }
}
