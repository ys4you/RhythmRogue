namespace RhythmRogue.Data
{
    /// <summary>
    /// Determines which highway(s) receive notes during a chart section.
    /// 
    /// EnemyOnly = FNF-style "enemy's turn" — enemy highway plays,
    ///             player watches. Visual/musical only.
    /// PlayerOnly = player's turn — player highway active, hit detection on.
    /// Both = simultaneous — both highways active at once.
    /// </summary>
    public enum SectionType
    {
        /// <summary>Enemy highway plays a pattern. Player watches.</summary>
        EnemyOnly,

        /// <summary>Player highway active. Hit detection on.</summary>
        PlayerOnly,

        /// <summary>Both highways active simultaneously.</summary>
        Both
    }
}
