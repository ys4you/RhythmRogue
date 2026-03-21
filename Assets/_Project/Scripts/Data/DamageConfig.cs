using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Damage value configuration for the battle damage pipeline.
    /// 
    /// ScriptableObject so designers can:
    ///   - Balance damage numbers without touching code
    ///   - Create difficulty presets (Casual: lower miss damage, Brutal: higher)
    ///   - Override per-enemy via EnemyData (post-prototype)
    /// 
    /// GDD §3.3 defaults: Perfect=5, Good=3, Bad=1 to enemy; Miss=5 to player.
    /// 
    /// Create via: Assets → Create → RhythmRogue → DamageConfig
    /// </summary>
    [CreateAssetMenu(fileName = "DamageConfig", menuName = "RhythmRogue/DamageConfig")]
    public class DamageConfig : ScriptableObject
    {
        [Header("Damage to Enemy (per judgment)")]
        [Tooltip("Base damage dealt to enemy on Perfect hit. GDD default: 5.")]
        [Min(0)] public int perfectDamage = 5;

        [Tooltip("Base damage dealt to enemy on Good hit. GDD default: 3.")]
        [Min(0)] public int goodDamage = 3;

        [Tooltip("Base damage dealt to enemy on Bad hit. GDD default: 1.")]
        [Min(0)] public int badDamage = 1;

        [Header("Damage to Player")]
        [Tooltip("Flat damage dealt to player on Miss. Not affected by combo. GDD default: 5.")]
        [Min(0)] public int missDamage = 5;

        [Header("Hold Note Damage")]
        [Tooltip("Damage per hold tick. Multiplied by combo multiplier.")]
        [Min(0)] public int holdTickDamage = 1;

        /// <summary>
        /// Look up base enemy damage for a judgment tier.
        /// </summary>
        public int GetEnemyDamage(int judgment)
        {
            return judgment switch
            {
                0 => perfectDamage, // Judgment.Perfect
                1 => goodDamage,    // Judgment.Good
                2 => badDamage,     // Judgment.Bad
                _ => 0
            };
        }
    }
}
