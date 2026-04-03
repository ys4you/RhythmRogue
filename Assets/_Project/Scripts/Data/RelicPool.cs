using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Collection of all available relics for reward generation.
    /// 
    /// The reward pick screen uses this to select 2-3 options
    /// weighted by rarity. Rarity weights are configurable.
    /// 
    /// Create one asset: Assets → Create → RhythmRogue → RelicPool
    /// Then drag all your RelicData assets into the Relics list.
    /// </summary>
    [CreateAssetMenu(fileName = "RelicPool", menuName = "RhythmRogue/RelicPool")]
    public class RelicPool : ScriptableObject
    {
        [Header("Available Relics")]
        [Tooltip("All relics that can appear in reward picks.")]
        public List<RelicData> relics = new();

        [Header("Rarity Weights")]
        [Tooltip("Selection weight for Common relics.")]
        public float commonWeight = 60f;
        [Tooltip("Selection weight for Uncommon relics.")]
        public float uncommonWeight = 30f;
        [Tooltip("Selection weight for Rare relics.")]
        public float rareWeight = 10f;

        /// <summary>
        /// Pick N unique relics from the pool using the provided RNG.
        /// Excludes relics the player already holds.
        /// </summary>
        /// <param name="rng">Seeded random for deterministic picks.</param>
        /// <param name="count">How many relics to offer (typically 3).</param>
        /// <param name="alreadyOwned">Relics the player already has (excluded).</param>
        /// <returns>List of unique relic options.</returns>
        public List<RelicData> PickOptions(ISeededRandom rng, int count,
            IReadOnlyList<RelicData> alreadyOwned = null)
        {
            // Build available pool (exclude owned)
            var available = new List<RelicData>();

            foreach (var relic in relics)
            {
                if (relic == null) continue;

                bool owned = false;
                if (alreadyOwned != null)
                {
                    foreach (var o in alreadyOwned)
                    {
                        if (o != null && o.relicId == relic.relicId)
                        {
                            owned = true;
                            break;
                        }
                    }
                }

                if (!owned)
                    available.Add(relic);
            }

            if (available.Count == 0)
                return new List<RelicData>();

            // Clamp count to available
            int pickCount = Mathf.Min(count, available.Count);

            // Build weighted table
            var builder = WeightedTable<RelicData>.Build();

            foreach (var relic in available)
            {
                float weight = relic.rarity switch
                {
                    RelicRarity.Common => commonWeight,
                    RelicRarity.Uncommon => uncommonWeight,
                    RelicRarity.Rare => rareWeight,
                    _ => commonWeight
                };

                builder.Add(relic, weight);
            }

            var table = builder.Done();
            return table.PickUnique(rng, pickCount);
        }
    }
}
