namespace RhythmRogue.Battle
{
    /// <summary>
    /// Complete result of evaluating a note hit or miss.
    /// 
    /// Fired by JudgmentSystem for every note — both player hits and
    /// auto-misses. All downstream systems (combo, damage, UI feedback,
    /// accuracy tracking) consume this single struct.
    /// 
    /// Struct to avoid GC allocation. Created once per note, passed
    /// through the event chain, then discarded.
    /// </summary>
    public readonly struct JudgmentResult
    {
        /// <summary>The judgment classification.</summary>
        public readonly Judgment Judgment;

        /// <summary>
        /// Timing offset in milliseconds after calibration adjustment.
        /// Negative = early, positive = late.
        /// Zero for auto-misses.
        /// </summary>
        public readonly float AdjustedOffsetMs;

        /// <summary>
        /// Raw timing offset before calibration.
        /// Useful for calibration screen feedback.
        /// </summary>
        public readonly float RawOffsetMs;

        /// <summary>Lane index (0-3).</summary>
        public readonly int Lane;

        /// <summary>
        /// Whether this was an auto-miss (note passed with no input)
        /// versus a player-triggered miss (input was too far off).
        /// Auto-misses don't have meaningful timing data.
        /// </summary>
        public readonly bool IsAutoMiss;

        /// <summary>The note that was judged.</summary>
        public readonly NoteView Note;

        public JudgmentResult(
            Judgment judgment,
            float adjustedOffsetMs,
            float rawOffsetMs,
            int lane,
            bool isAutoMiss,
            NoteView note)
        {
            Judgment = judgment;
            AdjustedOffsetMs = adjustedOffsetMs;
            RawOffsetMs = rawOffsetMs;
            Lane = lane;
            IsAutoMiss = isAutoMiss;
            Note = note;
        }

        public override string ToString()
        {
            if (IsAutoMiss)
                return $"[MISS (auto) L{Lane}]";

            string dir = AdjustedOffsetMs < 0 ? "early" : "late";
            return $"[{Judgment} L{Lane} {AdjustedOffsetMs:+0.0;-0.0}ms ({dir})]";
        }
    }
}
