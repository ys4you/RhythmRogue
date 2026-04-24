using System;
using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Annotates a region of a song with its structural role.
    /// 
    /// Sections define the energy arc of the song and control
    /// which highway(s) are active. The assembler reads these
    /// to know when to generate player notes vs enemy notes.
    /// 
    /// Sections should cover the entire song without gaps or overlap.
    /// </summary>
    [Serializable]
    public struct SongSection : IComparable<SongSection>
    {
        [Tooltip("Human-readable label (e.g. 'Verse 1', 'Chorus', 'Bridge').")]
        public string label;

        [Tooltip("Beat where this section starts (inclusive).")]
        public float startBeat;

        [Tooltip("Beat where this section ends (exclusive).")]
        public float endBeat;

        [Tooltip("Musical role of this section.")]
        public SongSectionType type;

        [Tooltip("Which highway(s) are active during this section.")]
        public SectionType highway;

        [Tooltip("Base intensity override for this section (0 = use marker intensities as-is, " +
                 "> 0 = scale marker intensities by this value). " +
                 "Use to globally boost or reduce a section's density.")]
        [Range(0f, 1.5f)]
        public float intensityScale;

        /// <summary>Duration in beats.</summary>
        public float DurationBeats => endBeat - startBeat;

        /// <summary>Whether a given beat falls within this section.</summary>
        public bool ContainsBeat(float beat) => beat >= startBeat && beat < endBeat;

        /// <summary>Sort by start beat.</summary>
        public int CompareTo(SongSection other) => startBeat.CompareTo(other.startBeat);

        public override string ToString()
        {
            return $"[{label}: {startBeat:F0}–{endBeat:F0} ({type}, {highway})]";
        }
    }
}
