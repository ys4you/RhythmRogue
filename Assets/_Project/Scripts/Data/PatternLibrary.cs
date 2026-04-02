using System.Collections.Generic;
using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Collection of all available rhythm patterns.
    /// 
    /// The ChartAssembler queries this library to find patterns
    /// matching a section slot's difficulty and tag requirements.
    /// 
    /// Designed to grow over time — add patterns as you author them.
    /// The assembler handles any library size gracefully.
    /// 
    /// Create one via: Assets → Create → RhythmRogue → Pattern Library
    /// Then drag PatternData assets into the patterns list.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PatternLibrary",
        menuName = "RhythmRogue/Pattern Library",
        order = 22)]
    public class PatternLibrary : ScriptableObject
    {
        [Tooltip("All available patterns. The assembler filters this list per section slot.")]
        public List<PatternData> patterns = new();

        /// <summary>
        /// Find all patterns matching the given constraints.
        /// </summary>
        /// <param name="maxDifficulty">Maximum difficulty tier (inclusive).</param>
        /// <param name="requiredTags">Tags the pattern must have (all of them).</param>
        /// <param name="results">Pre-allocated list to fill. Cleared before use.</param>
        /// <returns>Number of matching patterns found.</returns>
        public int Query(int maxDifficulty, PatternTag requiredTags, List<PatternData> results)
        {
            results.Clear();

            if (patterns == null) return 0;

            for (int i = 0; i < patterns.Count; i++)
            {
                PatternData p = patterns[i];
                if (p == null) continue;
                if (p.difficulty > maxDifficulty) continue;
                if (!p.HasTags(requiredTags)) continue;

                results.Add(p);
            }

            return results.Count;
        }

        /// <summary>
        /// Find all patterns matching difficulty and any of the given tags.
        /// More lenient than Query — matches if the pattern has at least one tag.
        /// </summary>
        public int QueryAny(int maxDifficulty, PatternTag anyTags, List<PatternData> results)
        {
            results.Clear();

            if (patterns == null) return 0;

            for (int i = 0; i < patterns.Count; i++)
            {
                PatternData p = patterns[i];
                if (p == null) continue;
                if (p.difficulty > maxDifficulty) continue;
                if (!p.HasAnyTag(anyTags)) continue;

                results.Add(p);
            }

            return results.Count;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Remove null entries
            patterns?.RemoveAll(p => p == null);
        }
#endif
    }
}
