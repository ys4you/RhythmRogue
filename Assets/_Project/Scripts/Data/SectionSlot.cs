using System;
using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Defines one section slot in a ChartTemplate.
    /// 
    /// During assembly, the ChartAssembler picks a pattern from the
    /// library that matches this slot's difficulty and tag requirements,
    /// then stamps it into the correct beat position.
    /// 
    /// The section type determines which highway(s) the pattern goes to.
    /// For SectionType.Both, the assembler picks two patterns — one for
    /// each highway.
    /// </summary>
    [Serializable]
    public struct SectionSlot
    {
        [Tooltip("Which highway(s) are active during this section.")]
        public SectionType type;

        [Tooltip("Duration of this section in beats. Pattern is trimmed or padded to fit.")]
        [Min(1f)]
        public float durationBeats;

        [Tooltip("Max difficulty tier for patterns in this section. Assembler picks patterns at or below this level.")]
        [Range(1, 10)]
        public int maxDifficulty;

        [Tooltip("Required tags — pattern must have ALL of these. Use None for no filter.")]
        public PatternTag requiredTags;

        [Tooltip("Optional: specific pattern to force for this slot. Overrides random selection.")]
        public PatternData forcedPattern;

        /// <summary>Whether this slot forces a specific pattern (no random pick).</summary>
        public bool IsForced => forcedPattern != null;
    }
}
