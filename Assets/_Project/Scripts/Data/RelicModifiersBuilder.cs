namespace RhythmRogue.Data
{
    /// <summary>
    /// Mutable accumulator that relic effects write into during aggregation, then sealed into an
    /// immutable <see cref="RelicModifiers"/> via <see cref="Build"/>.
    ///
    /// Keeping the consumed type (RelicModifiers) immutable while building it incrementally lets
    /// each effect contribute independently (open/closed) without ever exposing a half-built
    /// struct to the gameplay systems. Field names and defaults mirror RelicModifiers exactly,
    /// including CurrencyMultiplier starting at 1.0 (multiplicative base).
    /// </summary>
    public sealed class RelicModifiersBuilder
    {
        public float BonusPerfectWindowMs;
        public float BonusPerfectDamage;
        public float ComboRateBoost;
        public float ComboCapBoost;
        public int HealOnMilestoneHP;
        public int MissDamageReduction;
        public float CurrencyMultiplier = 1f;
        public int MissShieldCharges;

        public RelicModifiers Build() => new RelicModifiers(
            BonusPerfectWindowMs,
            BonusPerfectDamage,
            ComboRateBoost,
            ComboCapBoost,
            HealOnMilestoneHP,
            MissDamageReduction,
            CurrencyMultiplier,
            MissShieldCharges);
    }
}
