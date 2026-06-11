using System.Collections.Generic;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Computes aggregate RelicModifiers from a list of active relics.
    /// 
    /// Pure function: List&lt;RelicData&gt; in, RelicModifiers out.
    /// No MonoBehaviour, no Unity dependency, no state, no allocation
    /// beyond the returned struct. Easily unit-testable.
    /// 
    /// Called once per battle by BattleManager.InitializeBattle().
    /// 
    /// Stacking rules:
    ///   - Most effects are additive (two +5ms window relics = +10ms)
    ///   - CurrencyMultiplier is multiplicative from a 1.0 base
    ///     (two +25% relics = 1.25 × 1.25 = 1.5625×)
    /// 
    /// SOLID:
    ///   S — Only aggregates. No application, no UI, no battle logic.
    ///   O — New RelicEffects are handled by adding a case, not modifying existing ones.
    ///   D — Depends on RelicData abstraction, not on gameplay systems.
    /// </summary>
    public static class RelicEffectAggregator
    {
        /// <summary>
        /// Compute aggregate modifiers from all active relics.
        /// Returns RelicModifiers.None if the list is null or empty.
        /// </summary>
        public static RelicModifiers Aggregate(IReadOnlyList<RelicData> relics)
        {
            if (relics == null || relics.Count == 0)
                return RelicModifiers.None;

            float bonusPerfectWindowMs = 0f;
            float bonusPerfectDamage = 0f;
            float comboRateBoost = 0f;
            float comboCapBoost = 0f;
            int healOnMilestoneHP = 0;
            int missDamageReduction = 0;
            float currencyMultiplier = 1f;
            int missShieldCharges = 0;

            for (int i = 0; i < relics.Count; i++)
            {
                RelicData relic = relics[i];
                if (relic == null) continue;

                switch (relic.effect)
                {
                    case RelicEffect.WiderPerfectWindow:
                        bonusPerfectWindowMs += relic.floatValue;
                        break;

                    case RelicEffect.BonusPerfectDamage:
                        bonusPerfectDamage += relic.floatValue;
                        break;

                    case RelicEffect.ComboRateBoost:
                        comboRateBoost += relic.floatValue;
                        break;

                    case RelicEffect.ComboCapBoost:
                        comboCapBoost += relic.floatValue;
                        break;

                    case RelicEffect.HealOnComboMilestone:
                        healOnMilestoneHP += relic.intValue;
                        break;

                    case RelicEffect.ReduceMissDamage:
                        missDamageReduction += relic.intValue;
                        break;

                    case RelicEffect.MaxHPBoost:
                        // Applied immediately on pickup in RewardPickScreen.
                        // No per-battle aggregation needed.
                        break;

                    case RelicEffect.CurrencyMultiplier:
                        // Multiplicative stacking: 1.0 × (1 + 0.25) × (1 + 0.25) = 1.5625
                        currencyMultiplier *= (1f + relic.floatValue);
                        break;

                    case RelicEffect.MissShield:
                        missShieldCharges += relic.intValue;
                        break;
                }
            }

            return new RelicModifiers(
                bonusPerfectWindowMs,
                bonusPerfectDamage,
                comboRateBoost,
                comboCapBoost,
                healOnMilestoneHP,
                missDamageReduction,
                currencyMultiplier,
                missShieldCharges);
        }
    }
}
