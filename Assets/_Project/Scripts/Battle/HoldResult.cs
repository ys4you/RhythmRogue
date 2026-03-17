namespace RhythmRogue.Battle
{
    /// <summary>
    /// Result of a completed or partially completed hold note.
    /// Fired by HoldTracker when a hold ends for any reason.
    /// 
    /// The damage pipeline uses this to calculate:
    ///   totalHoldDamage = ticksHeld × damagePerTick × comboMultiplier
    /// 
    /// This is added ON TOP of the initial tap judgment damage
    /// that was already applied when the hold started.
    /// </summary>
    public readonly struct HoldResult
    {
        /// <summary>The hold note view.</summary>
        public readonly NoteView Note;

        /// <summary>Lane (0-3).</summary>
        public readonly int Lane;

        /// <summary>Number of ticks the player held through.</summary>
        public readonly int TicksHeld;

        /// <summary>Total possible ticks for this hold.</summary>
        public readonly int TotalTicks;

        /// <summary>0.0 to 1.0 — proportion of hold completed.</summary>
        public readonly float Progress;

        /// <summary>Whether the player held all the way to the end.</summary>
        public readonly bool Completed;

        public HoldResult(NoteView note, int lane, int ticksHeld, int totalTicks, bool completed)
        {
            Note = note;
            Lane = lane;
            TicksHeld = ticksHeld;
            TotalTicks = totalTicks;
            Progress = totalTicks > 0 ? (float)ticksHeld / totalTicks : 1f;
            Completed = completed;
        }

        public override string ToString()
        {
            string status = Completed ? "COMPLETE" : $"EARLY ({Progress:P0})";
            return $"[Hold L{Lane} {TicksHeld}/{TotalTicks} ticks — {status}]";
        }
    }
}
