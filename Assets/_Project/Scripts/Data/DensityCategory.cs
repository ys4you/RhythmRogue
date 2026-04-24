namespace RhythmRogue.Data
{
    /// <summary>
    /// Classifies the note density of a pattern or song section.
    /// 
    /// Used to match hand-crafted patterns to analyzed audio sections.
    /// A Sparse pattern fits a section with few onsets (verse),
    /// a Dense pattern fits a section with many onsets (chorus).
    /// 
    /// The audio analyzer computes this from onset count per bar.
    /// Pattern authors set this when creating patterns (or it's
    /// auto-calculated from the pattern's note count / bar count).
    /// </summary>
    public enum DensityCategory
    {
        /// <summary>1-2 notes per bar. Slow sections, intros, breaks.</summary>
        Sparse,

        /// <summary>2-4 notes per bar. Verses, calm sections.</summary>
        Light,

        /// <summary>4-6 notes per bar. Standard gameplay, moderate sections.</summary>
        Medium,

        /// <summary>6-8 notes per bar. Choruses, intense sections.</summary>
        Dense,

        /// <summary>8+ notes per bar. Solos, climaxes, expert sections.</summary>
        VeryDense
    }
}
