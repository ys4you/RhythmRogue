using System.Collections.Generic;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Computes aggregate <see cref="RelicModifiers"/> from a list of active relics.
    ///
    /// Pure function: relics in, RelicModifiers out. No MonoBehaviour, no Unity dependency, no
    /// state beyond the returned struct. Easily unit-testable. Called once per battle by
    /// BattleManager.InitializeBattle().
    ///
    /// Each relic carries a list of <see cref="RelicEffectDef"/>; every effect contributes its
    /// own numbers to a <see cref="RelicModifiersBuilder"/>. The stacking rules live in the
    /// effects themselves (most additive; currency multiplicative from a 1.0 base), so this
    /// aggregator is a fixed double loop that never needs editing when effects are added.
    ///
    /// SOLID:
    ///   S — Only aggregates. No application, no UI, no battle logic.
    ///   O — New effects are new RelicEffectDef subclasses; this loop never changes.
    ///   D — Depends on the RelicData / RelicEffectDef abstractions, not on gameplay systems.
    /// </summary>
    public static class RelicEffectAggregator
    {
        /// <summary>
        /// Compute aggregate modifiers from all active relics.
        /// Returns <see cref="RelicModifiers.None"/> if the list is null or empty.
        /// </summary>
        public static RelicModifiers Aggregate(IReadOnlyList<RelicData> relics)
        {
            if (relics == null || relics.Count == 0)
                return RelicModifiers.None;

            var builder = new RelicModifiersBuilder();

            for (int i = 0; i < relics.Count; i++)
            {
                RelicData relic = relics[i];
                if (relic == null || relic.Effects == null) continue;

                List<RelicEffectDef> effects = relic.Effects;
                for (int j = 0; j < effects.Count; j++)
                    effects[j]?.Contribute(builder);
            }

            return builder.Build();
        }
    }
}
