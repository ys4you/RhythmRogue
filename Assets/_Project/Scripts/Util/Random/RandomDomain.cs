namespace RhythmRogue.Util.Random
{
    /// <summary>
    /// Identifies each independent random stream in a run.
    /// 
    /// Each domain gets its own forked ISeededRandom so that adding
    /// or removing random calls in one system never shifts the
    /// output of another. This is what makes seeds truly reproducible
    /// even as you add content during development.
    /// 
    /// Add new entries as you build new procedural systems.
    /// </summary>
    public enum RandomDomain
    {
        /// <summary>Map layout, branching paths, node placement.</summary>
        Map,

        /// <summary>Node type assignment (Enemy, Elite, Event, Shop, Rest, Boss).</summary>
        NodeTypes,

        /// <summary>Enemy type selection and modifier assignment.</summary>
        Enemies,

        /// <summary>Rhythm chart pattern selection and assembly.</summary>
        Charts,

        /// <summary>Post-battle reward pool generation and reward picks.</summary>
        Rewards,

        /// <summary>Shop inventory and pricing.</summary>
        Shop,

        /// <summary>Random event encounters and their outcomes.</summary>
        Events,

        /// <summary>Boss mechanic selection (phase patterns, tempo changes).</summary>
        Bosses
    }
}
