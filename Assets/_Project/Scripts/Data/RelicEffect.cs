namespace RhythmRogue.Data
{
    /// <summary>
    /// Identifies which gameplay system a relic modifies.
    /// 
    /// Each effect maps to a specific config value that gets
    /// adjusted when the relic is active. The RelicApplier reads
    /// these at battle start to configure the gameplay systems.
    /// 
    /// Add new entries as you create new relic designs.
    /// </summary>
    public enum RelicEffect
    {
        /// <summary>Widens the Perfect timing window by FloatValue ms.</summary>
        WiderPerfectWindow,

        /// <summary>Adds FloatValue flat bonus damage on Perfect hits.</summary>
        BonusPerfectDamage,

        /// <summary>Increases combo multiplier rate by FloatValue per hit (default +0.1).</summary>
        ComboRateBoost,

        /// <summary>Raises the combo multiplier cap by FloatValue (default cap 3.0).</summary>
        ComboCapBoost,

        /// <summary>Heals IntValue HP every time combo reaches a milestone (50, 100, etc).</summary>
        HealOnComboMilestone,

        /// <summary>Reduces miss damage by IntValue.</summary>
        ReduceMissDamage,

        /// <summary>Increases max HP by IntValue (applied once on pickup).</summary>
        MaxHPBoost,

        /// <summary>Multiplies all Beats currency earned by FloatValue.</summary>
        CurrencyMultiplier
    }
}
