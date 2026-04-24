namespace RhythmRogue.Data
{
    /// <summary>
    /// Structural sections of a song.
    /// 
    /// Used by SongBeatMap to annotate regions of the song with
    /// their musical role. The assembler uses this to understand
    /// the energy arc — verses are calmer, choruses are intense,
    /// breaks are silent.
    /// 
    /// Also controls the enemy/player highway assignment:
    /// intros and verses may be enemy-only (player watches),
    /// choruses are player-turn, drops are both highways active.
    /// </summary>
    public enum SongSectionType
    {
        /// <summary>Opening — low energy, often enemy-only.</summary>
        Intro,

        /// <summary>Main body — moderate energy, player turn.</summary>
        Verse,

        /// <summary>High energy — dense notes, player turn.</summary>
        Chorus,

        /// <summary>Transitional — moderate energy, variable.</summary>
        Bridge,

        /// <summary>Maximum energy — both highways active, dense notes.</summary>
        Drop,

        /// <summary>Silence or minimal — few or no notes.</summary>
        Break,

        /// <summary>Closing — energy winding down.</summary>
        Outro
    }
}
