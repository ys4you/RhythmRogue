using System.Collections.Generic;
using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Collection of LaneShape assets for the chart assembler.
    /// 
    /// The assembler queries this library to find shapes matching
    /// the current difficulty and tag requirements, then picks one
    /// using seeded random.
    /// 
    /// Create via: Assets > Create > RhythmRogue > Shape Library
    /// </summary>
    [CreateAssetMenu(
        fileName = "ShapeLibrary",
        menuName = "RhythmRogue/Shape Library",
        order = 31)]
    public class ShapeLibrary : ScriptableObject
    {
        [Tooltip("All available shapes.")]
        public List<LaneShape> shapes = new();

        /// <summary>
        /// Find shapes at or below the given difficulty with ALL required tags.
        /// </summary>
        public int Query(int maxDifficulty, ShapeTag requiredTags, List<LaneShape> results)
        {
            results.Clear();
            if (shapes == null) return 0;

            for (int i = 0; i < shapes.Count; i++)
            {
                LaneShape s = shapes[i];
                if (s == null) continue;
                if (s.difficulty > maxDifficulty) continue;
                if (!s.HasTags(requiredTags)) continue;
                results.Add(s);
            }

            return results.Count;
        }

        /// <summary>
        /// Find shapes at or below difficulty with no tag filter.
        /// </summary>
        public int QueryAll(int maxDifficulty, List<LaneShape> results)
        {
            return Query(maxDifficulty, ShapeTag.None, results);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            shapes?.RemoveAll(s => s == null);
        }
#endif
    }
}
