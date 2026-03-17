namespace RhythmRogue.Data
{
    /// <summary>
    /// Runtime representation of a single note event.
    /// 
    /// This is the parsed, validated, sorted form that the note highway
    /// and hit detection systems consume. Created by ChartLoader from
    /// the raw JSON ChartData.
    /// 
    /// Immutable after creation — note data doesn't change during gameplay.
    /// Mutable state (isHit, isMissed) lives on the NoteView component,
    /// not here. This keeps data separate from runtime state.
    /// 
    /// Beat positions are fractional floats:
    ///   1.0   = beat 1 (quarter note)
    ///   1.5   = eighth note after beat 1
    ///   1.25  = sixteenth note after beat 1
    ///   4.0   = beat 4
    /// </summary>
    public readonly struct NoteData
    {
        /// <summary>
        /// When this note should be hit, in beats from song start.
        /// </summary>
        public readonly float BeatPosition;

        /// <summary>
        /// Which lane (0 = Left, 1 = Down, 2 = Up, 3 = Right).
        /// </summary>
        public readonly int Lane;

        /// <summary>
        /// Tap or Hold.
        /// </summary>
        public readonly NoteType Type;

        /// <summary>
        /// Duration in beats for hold notes. 0 for tap notes.
        /// The hold ends at BeatPosition + HoldDuration.
        /// </summary>
        public readonly float HoldDuration;

        /// <summary>
        /// Beat position where a hold note ends.
        /// For tap notes, this equals BeatPosition.
        /// </summary>
        public float EndBeatPosition => BeatPosition + HoldDuration;

        public NoteData(float beatPosition, int lane, NoteType type, float holdDuration = 0f)
        {
            BeatPosition = beatPosition;
            Lane = lane;
            Type = type;
            HoldDuration = holdDuration;
        }

        public override string ToString()
        {
            return Type == NoteType.Hold
                ? $"[{Type} L{Lane} @{BeatPosition:F2} dur:{HoldDuration:F2}]"
                : $"[{Type} L{Lane} @{BeatPosition:F2}]";
        }
    }
}
