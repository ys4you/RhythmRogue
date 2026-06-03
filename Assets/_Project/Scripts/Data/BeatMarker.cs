using System;
using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// A single musical event in a SongBeatMap.
    /// 
    /// Represents one moment where something musically interesting happens —
    /// a kick drum, a snare hit, a melodic accent, a drop. The algorithm
    /// reads these to know WHEN to place notes.
    /// 
    /// The marker doesn't specify lanes or note types — that's the
    /// LanePlanner's job. The marker only says "something happens here,
    /// this is what kind of thing it is, and this is how important it is."
    /// 
    /// Serializable for inspector editing and future tap-along editor tool.
    /// </summary>
    [Serializable]
    public struct BeatMarker : IComparable<BeatMarker>
    {
        [Tooltip("Beat position in the song (e.g. 1.0 = beat 1, 4.5 = beat 4 and a half).")]
        public float beat;

        [Tooltip("What kind of musical event this is. Guides lane selection.")]
        public MarkerType type;

        [Tooltip("How important this moment is (0.0 = barely noticeable, 1.0 = most important moment in the song). " +
                 "Controls whether this becomes a note at lower difficulties.")]
        [Range(0f, 1f)]
        public float intensity;

        [Tooltip("Suggested direction for melodic movement (-1 = descending/left, 0 = neutral, 1 = ascending/right). " +
                 "Only used by Melodic markers. Leave at 0 for non-melodic events.")]
        [Range(-1f, 1f)]
        public float direction;

        [Tooltip("Hold duration in beats. 0 = tap note. > 0 = sustain/hold note. " +
                 "Use for sustained vocal notes, long synth pads, etc.")]
        [Min(0f)]
        public float holdBeats;

        [Tooltip("Which instrument stem this marker was detected in (Drums/Bass/Melody). " +
                 "An enemy's chartInstrument selector keeps only markers matching its choice, " +
                 "so the chart can follow one instrument. 'All' on a marker is treated as " +
                 "untagged and always kept.")]
        public ChartInstrument instrument;

        public BeatMarker(float beat, MarkerType type, float intensity,
            float direction = 0f, float holdBeats = 0f,
            ChartInstrument instrument = ChartInstrument.All)
        {
            this.beat = beat;
            this.type = type;
            this.intensity = Mathf.Clamp01(intensity);
            this.direction = Mathf.Clamp(direction, -1f, 1f);
            this.holdBeats = Mathf.Max(0f, holdBeats);
            this.instrument = instrument;
        }

        /// <summary>Whether this is a hold note (sustained musical event).</summary>
        public bool IsHold => holdBeats > 0f;

        /// <summary>Sort by beat position.</summary>
        public int CompareTo(BeatMarker other) => beat.CompareTo(other.beat);

        public override string ToString()
        {
            string hold = IsHold ? $" hold:{holdBeats:F1}b" : "";
            string inst = instrument != ChartInstrument.All ? $" {instrument}" : "";
            return $"[{beat:F2} {type} i:{intensity:F1}{hold}{inst}]";
        }
    }
}
