namespace RhythmRogue.Data
{
    /// <summary>
    /// Tags describing a rhythm pattern's character.
    /// Used by the chart assembler to filter patterns during selection.
    /// 
    /// A pattern can have multiple tags (e.g. a dense hold pattern
    /// would be tagged Stream | Hold).
    /// 
    /// These are game-specific — lives in Data, not Util.
    /// </summary>
    [System.Flags]
    public enum PatternTag
    {
        None     = 0,

        /// <summary>Single notes in sequence, one at a time.</summary>
        Stream   = 1 << 0,

        /// <summary>Two or more simultaneous lane presses.</summary>
        Jump     = 1 << 1,

        /// <summary>Contains sustained hold notes.</summary>
        Hold     = 1 << 2,

        /// <summary>Mix of different note types.</summary>
        Mixed    = 1 << 3,

        /// <summary>Sparse, easy to read patterns.</summary>
        Simple   = 1 << 4,

        /// <summary>Dense, high note count patterns.</summary>
        Dense    = 1 << 5,

        /// <summary>Tricky timing or lane switching.</summary>
        Tricky   = 1 << 6,

        /// <summary>Rest / empty pattern — no notes, just silence.</summary>
        Rest     = 1 << 7
    }
}
