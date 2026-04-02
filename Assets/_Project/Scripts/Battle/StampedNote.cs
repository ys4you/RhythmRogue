namespace RhythmRogue.Battle
{
    /// <summary>
    /// A note with an absolute beat position, ready for highway consumption.
    /// 
    /// Created by the ChartAssembler when it stamps a pattern's relative
    /// beat offsets into absolute song positions.
    /// 
    /// This is what the NoteHighway and EnemyHighway actually render.
    /// Equivalent to the existing NoteData from JSON charts but produced
    /// dynamically from patterns.
    /// </summary>
    public readonly struct StampedNote
    {
        /// <summary>Lane index (0-3).</summary>
        public readonly int Lane;

        /// <summary>Absolute beat position in the song.</summary>
        public readonly float Beat;

        /// <summary>Hold duration in beats. 0 = tap note.</summary>
        public readonly float HoldBeats;

        /// <summary>Whether this is a tap note (not a hold).</summary>
        public bool IsTap => HoldBeats <= 0f;

        public StampedNote(int lane, float beat, float holdBeats = 0f)
        {
            Lane = lane;
            Beat = beat;
            HoldBeats = holdBeats;
        }

        public override string ToString()
        {
            string type = IsTap ? "Tap" : $"Hold({HoldBeats:F1}b)";
            return $"L{Lane} @{Beat:F2} {type}";
        }
    }
}
