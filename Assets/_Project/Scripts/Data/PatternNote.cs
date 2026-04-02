using System;
using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// A single note within a pattern.
    /// 
    /// Beat offset is RELATIVE to the pattern's start beat, not the
    /// song's absolute position. The ChartAssembler stamps these into
    /// absolute positions when assembling the BattleChart.
    /// 
    /// This makes patterns BPM-agnostic and reusable across any song.
    /// A quarter-note stream at beat offsets [0, 1, 2, 3] works at
    /// 100 BPM or 180 BPM — the Conductor handles timing.
    /// </summary>
    [Serializable]
    public struct PatternNote
    {
        /// <summary>Lane index (0-3: Left, Down, Up, Right).</summary>
        [Range(0, 3)]
        public int Lane;

        /// <summary>Beat offset relative to pattern start. 0 = first beat.</summary>
        [Min(0f)]
        public float BeatOffset;

        /// <summary>
        /// Hold duration in beats. 0 = tap note.
        /// A value of 2 means the player must hold for 2 beats.
        /// </summary>
        [Min(0f)]
        public float HoldBeats;

        /// <summary>Whether this is a tap note (not a hold).</summary>
        public bool IsTap => HoldBeats <= 0f;

        public PatternNote(int lane, float beatOffset, float holdBeats = 0f)
        {
            Lane = lane;
            BeatOffset = beatOffset;
            HoldBeats = holdBeats;
        }

        public override string ToString()
        {
            string type = IsTap ? "Tap" : $"Hold({HoldBeats:F1}b)";
            return $"L{Lane} @{BeatOffset:F2} {type}";
        }
    }
}
