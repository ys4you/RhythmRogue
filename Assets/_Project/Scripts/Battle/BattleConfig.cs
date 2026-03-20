using RhythmRogue.Data;
using UnityEngine;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Static data carrier for passing battle configuration across scenes.
    /// 
    /// Set before loading the battle scene, read by BattleManager on Awake.
    /// Static because ScriptableObject references survive scene loads but
    /// we need a simple way to say "fight THIS enemy with THIS chart"
    /// without a persistent manager yet.
    /// 
    /// Post-prototype: replace with a proper RunState system.
    /// </summary>
    public static class BattleConfig
    {
        /// <summary>Enemy to fight. Set before loading battle scene.</summary>
        public static EnemyData Enemy { get; set; }

        /// <summary>Chart to play. Set before loading battle scene.</summary>
        public static TextAsset ChartAsset { get; set; }

        /// <summary>Whether this is a boss fight (affects UI).</summary>
        public static bool IsBoss => Enemy != null && Enemy.IsBoss;

        /// <summary>Clear after battle ends.</summary>
        public static void Clear()
        {
            Enemy = null;
            ChartAsset = null;
        }
    }
}
