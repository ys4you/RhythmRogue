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
    }
}
