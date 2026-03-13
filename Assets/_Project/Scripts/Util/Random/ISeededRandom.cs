using System.Collections.Generic;

namespace RhythmRogue.Util.Random
{
    /// <summary>
    /// Abstraction for a deterministic random number generator.
    /// All procedural systems (map gen, chart assembly, enemy selection,
    /// reward pools, shop inventory) depend on this interface.
    /// 
    /// Determinism guarantee: given the same seed, the same sequence
    /// of calls produces the same results across platforms and sessions.
    /// </summary>
    public interface ISeededRandom
    {
        /// <summary>
        /// The numeric seed this instance was created with.
        /// </summary>
        int Seed { get; }

        /// <summary>
        /// Return a random int in [0, int.MaxValue).
        /// </summary>
        int Next();

        /// <summary>
        /// Return a random int in [0, maxExclusive).
        /// </summary>
        int Next(int maxExclusive);

        /// <summary>
        /// Return a random int in [minInclusive, maxExclusive).
        /// </summary>
        int Range(int minInclusive, int maxExclusive);

        /// <summary>
        /// Return a random float in [0.0, 1.0).
        /// </summary>
        float NextFloat();

        /// <summary>
        /// Return a random float in [min, max).
        /// </summary>
        float Range(float min, float max);

        /// <summary>
        /// Return true with the given probability (0.0 to 1.0).
        /// </summary>
        bool Chance(float probability);

        /// <summary>
        /// Pick a random element from a list.
        /// </summary>
        T Pick<T>(IReadOnlyList<T> items);

        /// <summary>
        /// Shuffle a list in-place using the Fisher-Yates algorithm.
        /// Deterministic given the current random state.
        /// </summary>
        void Shuffle<T>(IList<T> list);

        /// <summary>
        /// Create an independent child stream with a derived seed.
        /// 
        /// Critical for system isolation: the map generator and chart
        /// generator each get their own fork, so adding a call in one
        /// system doesn't shift the sequence of another.
        /// </summary>
        /// <param name="salt">
        /// Domain identifier that makes this fork unique.
        /// Use an enum or constant string per system (e.g. "map", "chart", "enemy").
        /// </param>
        ISeededRandom Fork(string salt);

        /// <summary>
        /// Create an independent child stream with a numeric salt.
        /// Useful for indexing (e.g. fork per area: Fork(areaIndex)).
        /// </summary>
        ISeededRandom Fork(int salt);
    }
}
