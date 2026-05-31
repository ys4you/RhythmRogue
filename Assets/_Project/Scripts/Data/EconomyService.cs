using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Computes how much currency a battle awards. Pure function: inputs in, int out.
    /// No MonoBehaviour, no state, no side effects, so it is trivially unit-testable
    /// and the same call works from BattleResultHandler, a preview UI, or a test.
    ///
    /// The formula is driven entirely by EconomyConfig, so balancing happens in the
    /// asset, not here. This class only encodes the SHAPE of each payout model; the
    /// numbers live in the config.
    ///
    /// SOLID:
    ///   S - Only computes an award amount. Does not store, display, or apply it.
    ///   O - Adding a payout model = adding a case here + a value in the enum.
    ///   D - Depends on the EconomyConfig data abstraction, not on battle internals.
    /// </summary>
    public static class EconomyService
    {
        /// <summary>The kind of encounter that was won, selecting which base/bonus to use.</summary>
        public enum EncounterKind { Normal, Elite, Boss }

        /// <summary>
        /// Compute the currency awarded for winning a battle.
        /// </summary>
        /// <param name="config">Economy configuration (numbers + model). If null, returns 0.</param>
        /// <param name="kind">Normal, Elite, or Boss - selects base and bonus values.</param>
        /// <param name="accuracy01">Battle accuracy in 0..1. Clamped internally.</param>
        /// <param name="currencyMultiplier">Relic multiplier (1.0 = none). From RelicModifiers.</param>
        /// <returns>Currency to award, never negative.</returns>
        public static int ComputeAward(EconomyConfig config, EncounterKind kind, float accuracy01, float currencyMultiplier)
        {
            if (config == null) return 0;

            float acc = Mathf.Clamp01(accuracy01);
            int baseAmount = BaseFor(config, kind);
            int maxBonus = BonusFor(config, kind);

            float raw;
            switch (config.Model)
            {
                case EconomyConfig.PayoutModel.Flat:
                    // Performance ignored: just the flat base.
                    raw = baseAmount;
                    break;

                case EconomyConfig.PayoutModel.PerformanceScaled:
                    // Entire payout scales with accuracy. A perfect run pays the full
                    // base; a sloppy win pays a fraction. (Harsh on weak players.)
                    raw = baseAmount * acc;
                    break;

                case EconomyConfig.PayoutModel.BasePlusBonus:
                default:
                    // Flat floor + accuracy-scaled bonus on top. The floor guarantees
                    // everyone can shop; the bonus (capped at maxBonus) rewards skill.
                    raw = baseAmount + acc * maxBonus;
                    break;
            }

            raw *= Mathf.Max(0f, currencyMultiplier);
            return Mathf.Max(0, Mathf.RoundToInt(raw));
        }

        private static int BaseFor(EconomyConfig c, EncounterKind kind) => kind switch
        {
            EncounterKind.Normal => c.NormalBattleBase,
            EncounterKind.Elite => c.EliteBattleBase,
            EncounterKind.Boss => c.BossBattleBase,
            _ => c.NormalBattleBase
        };

        private static int BonusFor(EconomyConfig c, EncounterKind kind) => kind switch
        {
            EncounterKind.Normal => c.NormalPerformanceBonus,
            EncounterKind.Elite => c.ElitePerformanceBonus,
            EncounterKind.Boss => c.BossPerformanceBonus,
            _ => c.NormalPerformanceBonus
        };
    }
}
