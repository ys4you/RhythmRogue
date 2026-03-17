namespace RhythmRogue.Battle
{
    /// <summary>
    /// Result of matching a player input to a note on the highway.
    /// 
    /// Passed from NoteMatcher to the hit judgment system (PROTO-007).
    /// Contains everything judgment needs: which note, how far off
    /// the timing was, and in which direction (early/late).
    /// 
    /// Using a struct avoids GC allocation on every note hit.
    /// </summary>
    public readonly struct NoteMatchResult
    {
        /// <summary>The matched note view on the highway.</summary>
        public readonly NoteView Note;

        /// <summary>
        /// Timing offset in milliseconds.
        /// Negative = early (player hit before the beat).
        /// Positive = late (player hit after the beat).
        /// </summary>
        public readonly float OffsetMs;

        /// <summary>Lane that was pressed (0-3).</summary>
        public readonly int Lane;

        public NoteMatchResult(NoteView note, float offsetMs, int lane)
        {
            Note = note;
            OffsetMs = offsetMs;
            Lane = lane;
        }

        public override string ToString()
        {
            return $"[Match L{Lane} offset:{OffsetMs:+0.0;-0.0}ms {Note.Data}]";
        }
    }
}
