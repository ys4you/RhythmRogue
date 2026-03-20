namespace RhythmRogue.Battle
{
    /// <summary>
    /// Results from a completed battle. Stored by BattleManager,
    /// read by the run summary screen (PROTO-022).
    /// </summary>
    public class BattleStats
    {
        /// <summary>Whether the player won.</summary>
        public bool Victory;

        /// <summary>Player HP remaining after battle.</summary>
        public int PlayerHPRemaining;

        /// <summary>Enemy HP remaining (0 if won).</summary>
        public int EnemyHPRemaining;

        /// <summary>Highest combo achieved.</summary>
        public int MaxCombo;

        /// <summary>Number of combo resets (misses that broke a streak).</summary>
        public int ComboResets;

        /// <summary>Accuracy as 0.0-1.0 (Perfect+Good / Total).</summary>
        public float Accuracy;

        /// <summary>Total notes in the chart.</summary>
        public int TotalNotes;

        /// <summary>Notes successfully hit (not missed).</summary>
        public int NotesHit;

        /// <summary>Enemy name for display.</summary>
        public string EnemyName;

        /// <summary>Whether this was a boss fight.</summary>
        public bool WasBoss;
    }
}
