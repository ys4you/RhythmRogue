using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Data
{
    [CreateAssetMenu(fileName = "RelicPool", menuName = "RhythmRogue/Data/Relic Pool")]
    public class RelicPool : ScriptableObject
    {
        [SerializeField] private List<RelicData> _allRelics = new();

        public IReadOnlyList<RelicData> AllRelics => _allRelics;

        public List<RelicData> PickOptions(ISeededRandom rng, int count, IReadOnlyList<RelicData> owned)
        {
            var eligible = new List<RelicData>();
            foreach (var relic in _allRelics)
            {
                if (relic == null) continue;
                if (owned != null && Contains(owned, relic)) continue;
                eligible.Add(relic);
            }

            if (eligible.Count == 0) return new List<RelicData>();

            var builder = WeightedTable<RelicData>.Build();
            foreach (var relic in eligible)
                builder.Add(relic, GetRarityWeight(relic.rarity));

            return builder.Done().PickUnique(rng, Mathf.Min(count, eligible.Count));
        }

        private static float GetRarityWeight(RelicRarity rarity) => rarity switch
        {
            RelicRarity.Common => 50f, RelicRarity.Uncommon => 30f, RelicRarity.Rare => 15f, _ => 50f
        };

        private static bool Contains(IReadOnlyList<RelicData> list, RelicData relic)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == relic) return true;
            return false;
        }
    }
}
