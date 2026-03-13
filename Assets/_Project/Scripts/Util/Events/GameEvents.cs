namespace RhythmRogue.Util.Events
{
    // ========================================================================
    // GAME EVENTS
    // Plain structs carrying data. No logic, no dependencies, no allocations.
    // Add new events here as you build systems — the EventBus handles them
    // automatically with zero changes to the bus itself (Open/Closed).
    // ========================================================================

    // --- Run Flow -----------------------------------------------------------

    public struct RunStartedEvent : IEvent
    {
        public string SeedCode;
    }

    public struct RunEndedEvent : IEvent
    {
        public bool Victory;
        public int AreasCleared;
        public int EnemiesDefeated;
        public float Accuracy;
        public int BeatsEarned;
        public int ScoreEarned;
    }

    // --- Map ----------------------------------------------------------------

    public struct NodeSelectedEvent : IEvent
    {
        /// <summary>Index of the selected node on the current map.</summary>
        public int NodeIndex;
        /// <summary>Type of the node (Enemy, Elite, Shop, Rest, Event, Boss).</summary>
        public int NodeType;
    }

    public struct AreaCompletedEvent : IEvent
    {
        public int AreaIndex;
    }

    // --- Battle Flow --------------------------------------------------------

    public struct BattleStartedEvent : IEvent
    {
        public int EnemyId;
        public float Bpm;
        public int EnemyHp;
    }

    public struct BattleEndedEvent : IEvent
    {
        public bool Victory;
        public int PlayerHpRemaining;
        public float Accuracy;
    }

    /// <summary>
    /// Published when a boss transitions between phases.
    /// </summary>
    public struct BossPhaseChangedEvent : IEvent
    {
        public int PhaseIndex;
        public float NewBpm;
    }

    // --- Combat Feedback ----------------------------------------------------

    /// <summary>
    /// Published on every note judgment. UI elements like combo counters,
    /// score displays, and hit effect spawners listen for this.
    /// 
    /// Note: Per-beat timing events (OnBeat, OnHalfBeat) should use
    /// C# events directly on the Conductor for performance, not this bus.
    /// </summary>
    public struct NoteJudgedEvent : IEvent
    {
        /// <summary>0 = Perfect, 1 = Good, 2 = Bad, 3 = Miss</summary>
        public int Judgment;
        /// <summary>Lane index (0-3).</summary>
        public int Lane;
        /// <summary>Timing offset in milliseconds (signed).</summary>
        public float OffsetMs;
    }

    public struct ComboChangedEvent : IEvent
    {
        public int CurrentCombo;
        public float Multiplier;
    }

    public struct ComboResetEvent : IEvent
    {
        /// <summary>What the combo was before it reset.</summary>
        public int LostCombo;
    }

    // --- Health -------------------------------------------------------------

    public struct PlayerHpChangedEvent : IEvent
    {
        public int CurrentHp;
        public int MaxHp;
        public int Delta;
    }

    public struct EnemyHpChangedEvent : IEvent
    {
        public int CurrentHp;
        public int MaxHp;
        public int Delta;
    }

    // --- Economy & Rewards --------------------------------------------------

    public struct CurrencyChangedEvent : IEvent
    {
        /// <summary>Beats currency total after the change.</summary>
        public int CurrentBeats;
        public int Delta;
    }

    public struct RewardPickOfferedEvent : IEvent
    {
        /// <summary>Number of options the player can choose from (2-3).</summary>
        public int OptionCount;
    }

    public struct RewardPickedEvent : IEvent
    {
        /// <summary>Index of the chosen reward (0-based).</summary>
        public int ChosenIndex;
        /// <summary>Identifier of the reward item (relic ID, upgrade ID, etc).</summary>
        public int RewardId;
    }

    // --- Meta Progression ---------------------------------------------------

    public struct MetaScoreChangedEvent : IEvent
    {
        public int CurrentScore;
        public int Delta;
    }

    public struct UnlockAcquiredEvent : IEvent
    {
        /// <summary>ID of the unlocked item (relic, song, modifier, cosmetic).</summary>
        public int UnlockId;
    }
}
