using System.Collections.Generic;
using System.Text;
using RhythmRogue.Core;
using RhythmRogue.Battle;
using RhythmRogue.Util;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Applies an event choice's effects to run state. Stateless static utility: all the
    /// "what does this do to the game" logic lives here, so EventData stays pure data and
    /// EventScreen stays pure presentation.
    ///
    /// Returns a short summary string of the concrete changes (e.g. "+50 Beats, -10 HP")
    /// so the screen can show the player exactly what happened, in addition to any authored
    /// flavor result text.
    ///
    /// SOLID:
    ///   S - Only applies effects + reports them. No selection, no UI, no data definition.
    ///   D - Depends on RunState / PlayerHealth / RelicPool abstractions passed in, not on
    ///       singletons it reaches for (except PlayerHealth, which is the established
    ///       run-scoped HP singleton).
    /// </summary>
    public static class EventOutcomeApplier
    {
        /// <summary>
        /// Apply every effect in a choice. Currency and relics go through RunState; HP goes
        /// through PlayerHealth. Random relic grants use the supplied seeded RNG so outcomes
        /// stay deterministic for a given seed.
        /// </summary>
        /// <returns>A short comma-separated summary of concrete changes, or "" if nothing changed.</returns>
        public static string Apply(EventChoice choice, RunState runState, RelicPool relicPool, ISeededRandom rng)
        {
            if (choice == null || choice.Effects == null || runState == null) return "";

            var parts = new List<string>();
            string currencyName = runState.Economy != null ? runState.Economy.CurrencyName : "Beats";

            foreach (var effect in choice.Effects)
            {
                if (effect == null) continue;
                switch (effect.Kind)
                {
                    case EventEffect.EffectKind.Currency:
                        ApplyCurrency(effect.Amount, runState, currencyName, parts);
                        break;

                    case EventEffect.EffectKind.HealthFlat:
                        ApplyHealth(effect.Amount, parts);
                        break;

                    case EventEffect.EffectKind.HealthPercent:
                        ApplyHealthPercent(effect.Amount, parts);
                        break;

                    case EventEffect.EffectKind.GrantRandomRelic:
                        ApplyRandomRelic(Mathf_Max1(effect.Amount), runState, relicPool, rng, parts);
                        break;

                    case EventEffect.EffectKind.GrantSpecificRelic:
                        ApplySpecificRelic(effect.SpecificRelic, runState, parts);
                        break;
                }
            }

            return string.Join(",  ", parts);
        }

        private static void ApplyCurrency(int amount, RunState runState, string currencyName, List<string> parts)
        {
            if (amount > 0)
            {
                runState.AddCurrency(amount);
                parts.Add($"+{amount} {currencyName}");
            }
            else if (amount < 0)
            {
                // Spend up to what the player has; never go negative. TrySpendCurrency
                // refuses partial spends, so clamp the cost to the current balance first.
                int cost = UnityEngine.Mathf.Min(-amount, runState.Currency);
                if (cost > 0)
                {
                    runState.TrySpendCurrency(cost);
                    parts.Add($"-{cost} {currencyName}");
                }
            }
        }

        private static void ApplyHealth(int amount, List<string> parts)
        {
            var ph = PlayerHealth.Instance;
            if (ph == null || amount == 0) return;

            if (amount > 0)
            {
                ph.Heal(amount);
                parts.Add($"+{amount} HP");
            }
            else
            {
                ph.TakeDamage(-amount);
                parts.Add($"-{-amount} HP");
            }
        }

        private static void ApplyHealthPercent(int percent, List<string> parts)
        {
            var ph = PlayerHealth.Instance;
            if (ph == null || percent == 0) return;

            int amount = UnityEngine.Mathf.RoundToInt(ph.MaxHP * (percent / 100f));
            if (amount == 0) return;

            if (amount > 0)
            {
                ph.Heal(amount);
                parts.Add($"+{amount} HP");
            }
            else
            {
                ph.TakeDamage(-amount);
                parts.Add($"-{-amount} HP");
            }
        }

        private static void ApplyRandomRelic(int count, RunState runState, RelicPool relicPool, ISeededRandom rng, List<string> parts)
        {
            if (relicPool == null || count <= 0) return;

            List<RelicData> picks = relicPool.PickOptions(rng, count, runState.ActiveRelics);
            foreach (var relic in picks)
            {
                GrantRelic(relic, runState, parts);
            }
        }

        private static void ApplySpecificRelic(RelicData relic, RunState runState, List<string> parts)
        {
            if (relic == null) return;
            // Don't grant a duplicate the player already owns.
            if (runState.ActiveRelics.Contains(relic)) return;
            GrantRelic(relic, runState, parts);
        }

        /// <summary>
        /// Add a relic to the run through the shared acquire path, which also applies any
        /// one-time on-pickup effect it has (e.g. Max HP). Identical to the reward and shop
        /// screens, so pickup behaves the same wherever a relic is granted.
        /// </summary>
        private static void GrantRelic(RelicData relic, RunState runState, List<string> parts)
        {
            if (relic == null) return;

            runState.AcquireRelic(relic, PlayerHealthAcquireContext.Default);

            parts.Add($"Relic: {relic.relicName}");
            GameLog.Info($"[EventOutcomeApplier] Granted relic {relic.relicName}.");
        }

        private static int Mathf_Max1(int v) => v < 1 ? 1 : v;
    }
}
