using System.Collections.Generic;
using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Collection of NotePattern fragments for the chart assembler. The assembler queries
    /// by difficulty and tags, then picks one with seeded random, weighted and density-aware.
    ///
    /// Create via: Assets > Create > RhythmRogue > Note Pattern Library
    /// </summary>
    [CreateAssetMenu(fileName = "NotePatternLibrary", menuName = "RhythmRogue/Note Pattern Library", order = 32)]
    public class NotePatternLibrary : ScriptableObject
    {
        [Tooltip("All available fragments.")]
        public List<NotePattern> patterns = new();

        /// <summary>Find fragments at or below the given difficulty carrying ALL required tags.</summary>
        public int Query(int maxDifficulty, ShapeTag requiredTags, List<NotePattern> results)
        {
            results.Clear();
            if (patterns == null) return 0;

            for (int i = 0; i < patterns.Count; i++)
            {
                NotePattern p = patterns[i];
                if (p == null) continue;
                if (p.difficulty > maxDifficulty) continue;
                if (!p.HasTags(requiredTags)) continue;
                results.Add(p);
            }

            return results.Count;
        }

        /// <summary>Find fragments at or below difficulty with no tag filter.</summary>
        public int QueryAll(int maxDifficulty, List<NotePattern> results) => Query(maxDifficulty, ShapeTag.None, results);

#if UNITY_EDITOR
        private void OnValidate() => patterns?.RemoveAll(p => p == null);
#endif
    }
}
