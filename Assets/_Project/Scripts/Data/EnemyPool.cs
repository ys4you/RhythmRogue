using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Weighted pool of enemies. Mirrors RelicPool's design.
    /// Each entry has a base EnemyData and a relative weight.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyPool", menuName = "RhythmRogue/Data/Enemy Pool")]
    public class EnemyPool : ScriptableObject
    {
        [System.Serializable]
        public struct WeightedEntry
        {
            public EnemyData enemy;
            [Min(0f)] public float weight;
        }

        [SerializeField] private List<WeightedEntry> _entries = new();

        public IReadOnlyList<WeightedEntry> Entries => _entries;
        public bool IsEmpty => _entries == null || _entries.Count == 0;

        /// <summary>Pick a single enemy, seeded.</summary>
        public EnemyData Pick(ISeededRandom rng)
        {
            if (IsEmpty) return null;

            var builder = WeightedTable<EnemyData>.Build();
            foreach (var entry in _entries)
            {
                if (entry.enemy == null) continue;
                float w = entry.weight > 0f ? entry.weight : 1f;
                builder.Add(entry.enemy, w);
            }

            return builder.Done().Pick(rng);
        }

        /// <summary>
        /// Pick a single enemy, seeded, considering only enemies allowed at the given normalized
        /// map depth (0 = opener, 1 = pre-boss). An enemy is eligible when its
        /// <see cref="EnemyData.minDepthT"/> is at or below <paramref name="depthT"/>, so harder-
        /// feeling enemies can be kept out of the opening layers. If nothing is eligible yet
        /// (every enemy gated above this depth), falls back to the full weighted pick so a node
        /// always gets an enemy.
        /// </summary>
        public EnemyData PickEligible(ISeededRandom rng, float depthT)
        {
            if (IsEmpty) return null;

            var builder = WeightedTable<EnemyData>.Build();
            int eligible = 0;
            foreach (var entry in _entries)
            {
                if (entry.enemy == null) continue;
                if (entry.enemy.minDepthT > depthT) continue;
                float w = entry.weight > 0f ? entry.weight : 1f;
                builder.Add(entry.enemy, w);
                eligible++;
            }

            if (eligible == 0) return Pick(rng);
            return builder.Done().Pick(rng);
        }
    }
}
