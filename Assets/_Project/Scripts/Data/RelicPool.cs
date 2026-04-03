using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Pool of all available relics for reward selection.
    /// 
    /// Holds every relic in the game and provides weighted random
    /// picking that excludes already-owned relics. Rarity determines
    /// base weight: Common = 50, Uncommon = 30, Rare = 15.
    /// 
    /// Create via: Assets → Create → RhythmRogue → Data → Relic Pool
    /// </summary>
    [CreateAssetMenu(fileName = "RelicPool", menuName = "RhythmRogue/Data/Relic Pool")]
    public class RelicPool : ScriptableObject
    {
        [Tooltip("All relics available in the game. Order doesn't matter — selection is weighted by rarity.")]
        [SerializeField] private List<RelicData> _allRelics = new();

        /// <summary>
        /// All relics in the pool (read-only).
        /// </summary>
        public IReadOnlyList<RelicData> AllRelics => _allRelics;

        /// <summary>
        /// Pick a set of unique relic options, excluding relics the player already owns.
        /// 
        /// Uses rarity-based weighting:
        ///   Common   = 50 weight
        ///   Uncommon = 30 weight
        ///   Rare     = 15 weight
        /// </summary>
        /// <param name="rng">Seeded random for deterministic selection.</param>
        /// <param name="count">Number of options to offer (typically 2-3).</param>
        /// <param name="owned">Relics the player already has (excluded from results).</param>
        /// <returns>List of unique relic options. May be shorter than count if pool is exhausted.</returns>
        public List<RelicData> PickOptions(ISeededRandom rng, int count, IReadOnlyList<RelicData> owned)
        {
            // Build eligible pool (exclude owned relics)
            var eligible = new List<RelicData>();

            foreach (var relic in _allRelics)
            {
                if (relic == null) continue;
                if (owned != null && Contains(owned, relic)) continue;

                eligible.Add(relic);
            }

            if (eligible.Count == 0)
                return new List<RelicData>();

            // Clamp count to available
            int pickCount = Mathf.Min(count, eligible.Count);

            // Build weighted table from eligible relics
            var builder = WeightedTable<RelicData>.Build();

            foreach (var relic in eligible)
            {
                float weight = GetRarityWeight(relic.rarity);
                builder.Add(relic, weight);
            }

            IWeightedSelector<RelicData> table = builder.Done();
            return table.PickUnique(rng, pickCount);
        }

        /// <summary>
        /// Get the base selection weight for a rarity tier.
        /// </summary>
        private static float GetRarityWeight(RelicRarity rarity)
        {
            return rarity switch
            {
                RelicRarity.Common => 50f,
                RelicRarity.Uncommon => 30f,
                RelicRarity.Rare => 15f,
                _ => 50f
            };
        }

        /// <summary>
        /// Check if a relic is in a collection (by reference equality).
        /// Avoids LINQ to keep allocation-free.
        /// </summary>
        private static bool Contains(IReadOnlyList<RelicData> list, RelicData relic)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == relic) return true;
            }
            return false;
        }
    }
}
