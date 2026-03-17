namespace RhythmRogue.Battle
{
    /// <summary>
    /// Tracks the runtime state of a single active hold note.
    /// 
    /// Created when a hold note is successfully matched (tap judgment passed).
    /// Updated each frame by HoldTracker. Destroyed when the hold completes,
    /// is released early, or the note despawns.
    /// </summary>
    public class HoldState
    {
        /// <summary>The hold note view on the highway.</summary>
        public readonly NoteView Note;

        /// <summary>Lane this hold is on (0-3).</summary>
        public readonly int Lane;

        /// <summary>Beat position where the hold ends.</summary>
        public readonly float EndBeat;

        /// <summary>Total duration in beats.</summary>
        public readonly float TotalDurationBeats;

        /// <summary>Number of ticks in this hold (based on tick interval).</summary>
        public readonly int TotalTicks;

        /// <summary>Number of ticks successfully held so far.</summary>
        public int TicksHeld;

        /// <summary>Beat of the next tick to award.</summary>
        public float NextTickBeat;

        /// <summary>Whether the player is still holding.</summary>
        public bool IsActive;

        /// <summary>Whether the hold was completed (held to the end).</summary>
        public bool IsCompleted;

        /// <summary>Whether the player released early (partial credit).</summary>
        public bool IsReleasedEarly;

        /// <summary>
        /// Progress from 0.0 (just started) to 1.0 (held to end).
        /// Based on ticks held / total ticks.
        /// </summary>
        public float Progress => TotalTicks > 0 ? (float)TicksHeld / TotalTicks : 1f;

        public HoldState(NoteView note, int lane, float endBeat, float totalDuration, int totalTicks, float firstTickBeat)
        {
            Note = note;
            Lane = lane;
            EndBeat = endBeat;
            TotalDurationBeats = totalDuration;
            TotalTicks = totalTicks;
            TicksHeld = 0;
            NextTickBeat = firstTickBeat;
            IsActive = true;
            IsCompleted = false;
            IsReleasedEarly = false;
        }
    }
}
