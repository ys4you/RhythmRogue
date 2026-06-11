namespace RhythmRogue.Data
{
    /// <summary>
    /// Identifies which gameplay system a relic modifies.
    /// 
    /// Each effect maps to a specific system:
    /// - Hit detection reads WiderPerfectWindow
    /// - DamagePipeline reads BonusPerfectDamage, ReduceMissDamage
    /// - ComboSystem reads ComboRateBoost, ComboCapBoost, HealOnComboMilestone
    /// - PlayerHealth reads MaxHPBoost
    /// - Economy reads CurrencyMultiplier
    /// 
    /// The consuming system is responsible for querying active relics
    /// and applying the effect. Relics themselves carry no logic.
    /// </summary>
    public enum RelicEffect
    {
        /// <summary>Widens the Perfect timing window by floatValue ms.</summary>
        WiderPerfectWindow,

        /// <summary>Adds floatValue bonus damage on Perfect hits.</summary>
        BonusPerfectDamage,

        /// <summary>Increases combo multiplier rate by floatValue per hit.</summary>
        ComboRateBoost,

        /// <summary>Raises combo multiplier cap by floatValue.</summary>
        ComboCapBoost,

        /// <summary>Heals intValue HP at combo milestones.</summary>
        HealOnComboMilestone,

        /// <summary>Reduces miss damage taken by intValue.</summary>
        ReduceMissDamage,

        /// <summary>Increases max HP by intValue (applied immediately on pickup).</summary>
        MaxHPBoost,

        /// <summary>Multiplies currency earned by (1 + floatValue).</summary>
        CurrencyMultiplier,

        /// <summary>
        /// Grants intValue shield charges per battle that absorb Miss damage: each non-final charge
        /// halves the hit, the final charge fully blocks it, then the shield is spent until the next
        /// battle. Read by DamagePipeline.
        /// </summary>
        MissShield
    }
}
