using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Timing window configuration for hit judgment.
    /// 
    /// ScriptableObject so designers can:
    ///   - Tune windows in the Inspector without touching code
    ///   - Create presets (Easy: wider windows, Hard: tighter)
    ///   - Swap configs at runtime via relics (e.g. "Perfect Lens" widens Perfect)
    /// 
    /// GDD §3.3 defaults: Perfect ±35ms, Good ±70ms, Bad ±110ms.
    /// 
    /// Create via: Assets → Create → RhythmRogue → JudgmentConfig
    /// </summary>
    [CreateAssetMenu(fileName = "JudgmentConfig", menuName = "RhythmRogue/JudgmentConfig")]
    public class JudgmentConfig : ScriptableObject
    {
        [Header("Timing Windows (ms)")]
        [Tooltip("±ms for Perfect judgment. GDD default: 35.")]
        [Range(10f, 60f)]
        public float perfectWindowMs = 35f;

        [Tooltip("±ms for Good judgment. GDD default: 70.")]
        [Range(30f, 100f)]
        public float goodWindowMs = 70f;

        [Tooltip("±ms for Bad judgment. Beyond this is a Miss. GDD default: 110.")]
        [Range(50f, 150f)]
        public float badWindowMs = 110f;

        /// <summary>
        /// Validate that windows are ordered correctly.
        /// Called by Unity when values change in Inspector.
        /// </summary>
        private void OnValidate()
        {
            if (goodWindowMs <= perfectWindowMs)
                goodWindowMs = perfectWindowMs + 1f;

            if (badWindowMs <= goodWindowMs)
                badWindowMs = goodWindowMs + 1f;
        }
    }
}
