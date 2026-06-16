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

        [Tooltip("Damage dealt to the player by each enemy note that lands while their guard is " +
                 "down (after a Miss, until the guard is recovered). Kept small on purpose: it " +
                 "stacks across a dense enemy phrase while you are exposed. Set to 0 to disable " +
                 "enemy attacks.")]
        [Min(0)] public int enemyNoteDamage = 2;

        [Tooltip("How many successful hits it takes to bring the guard back up after a Miss. " +
                 "1 = the guard returns on your very next hit (very forgiving); higher keeps you " +
                 "exposed across several notes so the enemy's phrase can actually land and sting. " +
                 "The exposure window resets on each new Miss.")]
        [Min(1)] public int guardRecoveryHits = 3;

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
