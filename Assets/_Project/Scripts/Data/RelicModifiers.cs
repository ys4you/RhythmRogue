namespace RhythmRogue.Data
{
    /// <summary>
    /// Aggregated relic bonuses computed at battle start.
    /// 
    /// Pure data snapshot — computed once by RelicEffectAggregator,
    /// distributed to gameplay systems by BattleManager. No system
    /// knows about individual RelicData or RelicEffect — they just
    /// receive numeric bonuses.
    /// 
    /// Additive effects sum (two +5ms window relics = +10ms).
    /// Multiplicative effects multiply (two 1.25x currency = 1.5625x).
    /// </summary>
    public readonly struct RelicModifiers
    {
        /// <summary>Added to the Perfect timing window (ms). Additive.</summary>
        public readonly float BonusPerfectWindowMs;

        /// <summary>Bonus damage on Perfect hits. Additive.</summary>
        public readonly float BonusPerfectDamage;

        /// <summary>Added to combo multiplier rate per hit. Additive.</summary>
        public readonly float ComboRateBoost;

        /// <summary>Added to combo multiplier cap. Additive.</summary>
        public readonly float ComboCapBoost;

        /// <summary>HP healed at combo milestones. Additive (stacks).</summary>
        public readonly int HealOnMilestoneHP;

        /// <summary>Flat damage reduction on Miss. Additive.</summary>
        public readonly int MissDamageReduction;

        /// <summary>Currency earned multiplier. Multiplicative (base 1.0).</summary>
        public readonly float CurrencyMultiplier;

        /// <summary>Shield charges per battle that absorb Miss damage (non-final halve, final blocks). Additive.</summary>
        public readonly int MissShieldCharges;

        /// <summary>Whether any relics are active at all.</summary>
        public bool HasAnyEffect =>
            BonusPerfectWindowMs != 0f ||
            BonusPerfectDamage != 0f ||
            ComboRateBoost != 0f ||
            ComboCapBoost != 0f ||
            HealOnMilestoneHP != 0 ||
            MissDamageReduction != 0 ||
            CurrencyMultiplier != 1f ||
            MissShieldCharges != 0;

        public RelicModifiers(
            float bonusPerfectWindowMs,
            float bonusPerfectDamage,
            float comboRateBoost,
            float comboCapBoost,
            int healOnMilestoneHP,
            int missDamageReduction,
            float currencyMultiplier,
            int missShieldCharges)
        {
            BonusPerfectWindowMs = bonusPerfectWindowMs;
            BonusPerfectDamage = bonusPerfectDamage;
            ComboRateBoost = comboRateBoost;
            ComboCapBoost = comboCapBoost;
            HealOnMilestoneHP = healOnMilestoneHP;
            MissDamageReduction = missDamageReduction;
            CurrencyMultiplier = currencyMultiplier;
            MissShieldCharges = missShieldCharges;
        }

        /// <summary>No bonuses. Used when no relics are active.</summary>
        public static readonly RelicModifiers None = new(0f, 0f, 0f, 0f, 0, 0, 1f, 0);

        public override string ToString()
        {
            if (!HasAnyEffect) return "[No relic effects]";

            return $"[Relics: " +
                   (BonusPerfectWindowMs != 0f ? $"PerfWin+{BonusPerfectWindowMs}ms " : "") +
                   (BonusPerfectDamage != 0f ? $"PerfDmg+{BonusPerfectDamage} " : "") +
                   (ComboRateBoost != 0f ? $"Rate+{ComboRateBoost} " : "") +
                   (ComboCapBoost != 0f ? $"Cap+{ComboCapBoost} " : "") +
                   (HealOnMilestoneHP != 0 ? $"MileHP+{HealOnMilestoneHP} " : "") +
                   (MissDamageReduction != 0 ? $"MissRed-{MissDamageReduction} " : "") +
                   (CurrencyMultiplier != 1f ? $"Curr×{CurrencyMultiplier:F2} " : "") +
                   (MissShieldCharges != 0 ? $"Shield×{MissShieldCharges}" : "") +
                   "]";
        }
    }
}
