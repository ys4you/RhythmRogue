using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Run-level forgiveness tier, chosen once per run. Shifts how hard the whole run plays
    /// without changing the act-by-act ramp. Normal is the intended baseline; Relaxed is the
    /// accessibility option for players new to rhythm games; Hard is for veterans.
    ///
    /// Normal is value 0 so a defaulted <see cref="DifficultyContext"/> reads as Normal.
    /// </summary>
    public enum DifficultyTier { Normal = 0, Relaxed = 1, Hard = 2 }

    /// <summary>Per-tier numbers. Kept in code (not an asset) since there are only three.</summary>
    public static class DifficultyTierConfig
    {
        /// <summary>Added to chart difficulty (0-1). Relaxed thins charts, Hard densifies them.</summary>
        public static float DifficultyOffset(DifficultyTier tier) => tier switch
        {
            DifficultyTier.Relaxed => -0.06f,
            DifficultyTier.Hard => 0.10f,
            _ => 0f
        };

        /// <summary>
        /// Multiplier on hit-window widths. Relaxed widens (more forgiving), Hard tightens.
        /// Not yet applied to JudgmentSystem; reserved for the timing-window pass.
        /// </summary>
        public static float WindowScale(DifficultyTier tier) => tier switch
        {
            DifficultyTier.Relaxed => 1.35f,
            DifficultyTier.Hard => 0.85f,
            _ => 1f
        };
    }

    /// <summary>
    /// Everything the difficulty formula needs that does not live on the enemy: which area the
    /// fight is in, how deep the node sits (0 = the area's opener, 1 = its last pre-boss node),
    /// the run's forgiveness tier, and whether this is the boss fight.
    ///
    /// A defaulted value (Area null) means "no run context" - e.g. launching the Battle scene
    /// directly to test - and the curve falls back to the enemy's own fallback fields.
    /// </summary>
    public readonly struct DifficultyContext
    {
        public readonly Area Area;
        public readonly float DepthT;
        public readonly DifficultyTier Tier;
        public readonly bool IsBoss;

        public DifficultyContext(Area area, float depthT, DifficultyTier tier, bool isBoss)
        {
            Area = area;
            DepthT = Mathf.Clamp01(depthT);
            Tier = tier;
            IsBoss = isBoss;
        }
    }

    /// <summary>
    /// Single source of truth for how hard a fight is. An enemy is flavour (a small tilt); the
    /// area and node depth set the actual level. The same enemy is easy at the opener of area 1
    /// and brutal deep in a later area, because the slot moved, not the enemy.
    ///
    ///   chart difficulty = areaBand(depth) + enemy flavour + elite boost + tier offset
    ///   enemy HP         = areaBaseHP * (1 + depthGain * depth) * enemy hpFlavour [* elite]
    ///
    /// Both clamp/secure their outputs. With no Area context, both fall back to the enemy's own
    /// fallback fields so a direct battle-scene launch still works.
    /// </summary>
    public static class DifficultyCurve
    {
        /// <summary>Chart difficulty (0-1) handed to the chart assembler.</summary>
        public static float ChartDifficulty(in DifficultyContext ctx, EnemyData enemy, bool isElite, EliteConfig elite)
        {
            float baseline;
            if (ctx.Area == null)
                baseline = enemy != null ? enemy.markerDifficulty : 0.4f; // no-context fallback
            else
                baseline = ctx.IsBoss
                    ? ctx.Area.bossDifficulty
                    : Mathf.Lerp(ctx.Area.difficultyFloor, ctx.Area.difficultyCeil, ctx.DepthT);

            float flavor = enemy != null ? enemy.difficultyFlavor : 0f;
            float eliteBoost = (isElite && elite != null) ? elite.difficultyBoost * 0.1f : 0f;
            float tierOffset = DifficultyTierConfig.DifficultyOffset(ctx.Tier);

            return Mathf.Clamp01(baseline + flavor + eliteBoost + tierOffset);
        }

        /// <summary>Enemy HP (fight length). Elite scaling is applied here so callers do not double it.</summary>
        public static int EnemyHP(in DifficultyContext ctx, EnemyData enemy, bool isElite, EliteConfig elite)
        {
            float baseHP;
            if (ctx.Area == null)
                baseHP = enemy != null ? enemy.maxHP : 100; // no-context fallback
            else
                baseHP = ctx.IsBoss
                    ? ctx.Area.bossHP
                    : ctx.Area.baseEnemyHP * (1f + ctx.Area.hpDepthGain * ctx.DepthT);

            float hpFlavor = enemy != null ? Mathf.Max(0.1f, enemy.hpFlavor) : 1f;
            int hp = Mathf.RoundToInt(baseHP * hpFlavor);

            if (isElite && elite != null) hp = elite.ScaleHP(hp);
            return Mathf.Max(1, hp);
        }
    }
}
