using System.Collections.Generic;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Reads active relics and computes aggregate gameplay modifiers.
    /// 
    /// Called by BattleManager at battle start to configure systems.
    /// Pure static utility — no state, no MonoBehaviour. Just reads
    /// the relic list and returns accumulated values.
    /// 
    /// Multiple relics of the same effect type stack additively.
    /// 
    /// Usage:
    ///   var mods = RelicApplier.ComputeModifiers(runState.ActiveRelics);
    ///   judgmentSystem.SetPerfectWindowBonus(mods.PerfectWindowBonus);
    ///   comboSystem.SetRateBonus(mods.ComboRateBonus);
    /// </summary>
    public static class RelicApplier
    {
        /// <summary>
        /// Aggregated modifier values from all active relics.
        /// Battle systems read these at start.
        /// </summary>
        public struct Modifiers
        {
            /// <summary>Extra ms added to the Perfect timing window (default 0).</summary>
            public float PerfectWindowBonus;

            /// <summary>Flat bonus damage on Perfect hits (default 0).</summary>
            public float BonusPerfectDamage;

            /// <summary>Extra multiplier rate per combo hit (default 0, base is 0.1).</summary>
            public float ComboRateBonus;

            /// <summary>Extra cap added to combo multiplier (default 0, base cap is 3.0).</summary>
            public float ComboCapBonus;

            /// <summary>HP healed at each combo milestone (default 0).</summary>
            public int HealOnMilestone;

            /// <summary>Flat reduction to miss damage (default 0).</summary>
            public int MissDamageReduction;

            /// <summary>Extra max HP from relics (default 0).</summary>
            public int MaxHPBoost;

            /// <summary>Currency multiplier bonus (default 0, applied as 1 + bonus).</summary>
            public float CurrencyMultiplierBonus;
        }

        /// <summary>
        /// Compute aggregate modifiers from a list of relics.
        /// Returns zeroed modifiers if the list is null or empty.
        /// </summary>
        public static Modifiers ComputeModifiers(IReadOnlyList<RelicData> relics)
        {
            var mods = new Modifiers();

            if (relics == null) return mods;

            foreach (var relic in relics)
            {
                if (relic == null) continue;

                switch (relic.effect)
                {
                    case RelicEffect.WiderPerfectWindow:
                        mods.PerfectWindowBonus += relic.floatValue;
                        break;

                    case RelicEffect.BonusPerfectDamage:
                        mods.BonusPerfectDamage += relic.floatValue;
                        break;

                    case RelicEffect.ComboRateBoost:
                        mods.ComboRateBonus += relic.floatValue;
                        break;

                    case RelicEffect.ComboCapBoost:
                        mods.ComboCapBonus += relic.floatValue;
                        break;

                    case RelicEffect.HealOnComboMilestone:
                        mods.HealOnMilestone += relic.intValue;
                        break;

                    case RelicEffect.ReduceMissDamage:
                        mods.MissDamageReduction += relic.intValue;
                        break;

                    case RelicEffect.MaxHPBoost:
                        mods.MaxHPBoost += relic.intValue;
                        break;

                    case RelicEffect.CurrencyMultiplier:
                        mods.CurrencyMultiplierBonus += relic.floatValue;
                        break;
                }
            }

            return mods;
        }
    }
}
