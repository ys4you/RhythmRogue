namespace RhythmRogue.Util.Random
{
    /// <summary>
    /// Per-run seed context. Created once at run start, provides
    /// isolated random streams to each procedural system.
    /// 
    /// Systems request their stream via GetRandom(domain) and use it
    /// for all random decisions within that domain. This guarantees
    /// that the same seed code produces the same run.
    /// </summary>
    public interface IRunSeed
    {
        /// <summary>
        /// The human-readable seed code for this run (e.g. "BEAT-7X3K").
        /// Displayed on the Map Screen and Run Summary Screen.
        /// </summary>
        string Code { get; }

        /// <summary>
        /// The numeric seed derived from the code.
        /// </summary>
        int NumericSeed { get; }

        /// <summary>
        /// Get the isolated random stream for a specific game system.
        /// Each domain returns the same ISeededRandom instance on
        /// repeated calls — streams are created once and cached.
        /// </summary>
        /// <param name="domain">Which system is requesting random numbers.</param>
        ISeededRandom GetRandom(RandomDomain domain);

        /// <summary>
        /// Get a sub-fork within a domain, indexed by a numeric key.
        /// 
        /// Useful when a system needs per-area or per-encounter isolation.
        /// Example: Charts domain forked per area index, so area 2's chart
        /// patterns are independent of how many battles area 1 had.
        /// </summary>
        /// <param name="domain">Parent domain.</param>
        /// <param name="subIndex">Numeric sub-key (e.g. area index, encounter index).</param>
        ISeededRandom GetRandom(RandomDomain domain, int subIndex);
    }
}
