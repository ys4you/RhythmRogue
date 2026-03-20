namespace RhythmRogue.Battle
{
    /// <summary>
    /// Battle lifecycle phases, managed by BattleManager via the FSM.
    /// </summary>
    public enum BattlePhase
    {
        /// <summary>Loading data, initializing systems, countdown.</summary>
        Intro,

        /// <summary>Song playing, notes scrolling, input active.</summary>
        Playing,

        /// <summary>Enemy HP hit 0 — player wins.</summary>
        Won,

        /// <summary>Player HP hit 0 or song ended with enemy alive.</summary>
        Lost
    }
}
