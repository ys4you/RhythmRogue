namespace RhythmRogue.Data
{
    /// <summary>
    /// Type of note in a rhythm chart.
    /// 
    /// Tap: single press on beat arrival.
    /// Hold: press and sustain for a duration.
    /// 
    /// Add new types here as mechanics expand post-prototype
    /// (e.g. Swipe, Double, Mine).
    /// </summary>
    public enum NoteType
    {
        Tap,
        Hold
    }
}
