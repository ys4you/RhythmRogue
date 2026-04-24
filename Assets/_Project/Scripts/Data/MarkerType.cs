namespace RhythmRogue.Data
{
    /// <summary>
    /// Types of musical events marked in a SongBeatMap.
    /// 
    /// These are hints to the LanePlanner about what kind of musical
    /// event this beat represents. The planner uses them to bias lane
    /// selection — kicks go low, snares go high, melodic accents follow
    /// directional movement.
    /// 
    /// A marker's type does NOT determine if it becomes a note —
    /// that's controlled by the difficulty filter and the marker's intensity.
    /// </summary>
    public enum MarkerType
    {
        /// <summary>Bass drum / kick — biases toward lower lanes (0, 1).</summary>
        Kick,

        /// <summary>Snare / clap — biases toward upper lanes (2, 3).</summary>
        Snare,

        /// <summary>Hi-hat / cymbal — light accent, alternating lanes.</summary>
        HiHat,

        /// <summary>Melodic note or vocal — follows directional lane movement.</summary>
        Melodic,

        /// <summary>Strong musical accent — any lane, higher chance of jumps.</summary>
        Accent,

        /// <summary>Major energy moment (chorus drop, buildup peak) — jumps or dense notes.</summary>
        Drop,

        /// <summary>Silence or breakdown — suppresses notes even at high difficulty.</summary>
        Break,

        /// <summary>Drum fill or transition — rapid notes across lanes.</summary>
        Fill
    }
}
