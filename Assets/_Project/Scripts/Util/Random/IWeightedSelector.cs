using System.Collections.Generic;

namespace RhythmRogue.Util.Random
{
    /// <summary>
    /// Abstraction for selecting items from a weighted probability table.
    /// 
    /// Consumers depend on this interface, allowing different implementations
    /// (linear scan, binary search, alias method) to be swapped based on
    /// table size and performance needs.
    /// </summary>
    /// <typeparam name="T">Type of item in the table.</typeparam>
    public interface IWeightedSelector<T>
    {
        /// <summary>
        /// Number of entries in the table.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Sum of all weights in the table.
        /// </summary>
        float TotalWeight { get; }

        /// <summary>
        /// Pick one item using the provided random source.
        /// </summary>
        /// <param name="rng">Seeded random for deterministic selection.</param>
        T Pick(ISeededRandom rng);

        /// <summary>
        /// Pick multiple items (with replacement — same item can appear twice).
        /// </summary>
        /// <param name="rng">Seeded random for deterministic selection.</param>
        /// <param name="count">Number of items to pick.</param>
        List<T> Pick(ISeededRandom rng, int count);

        /// <summary>
        /// Pick multiple unique items (without replacement).
        /// Throws if count exceeds the number of entries.
        /// 
        /// Useful for reward picks where the player chooses 1 from 2-3
        /// unique options, or shop inventory generation.
        /// </summary>
        /// <param name="rng">Seeded random for deterministic selection.</param>
        /// <param name="count">Number of unique items to pick.</param>
        List<T> PickUnique(ISeededRandom rng, int count);

        /// <summary>
        /// Get the probability (0.0 to 1.0) of a specific entry being selected.
        /// Useful for UI display (e.g. showing drop rates).
        /// </summary>
        /// <param name="index">Index of the entry.</param>
        float GetProbability(int index);

        /// <summary>
        /// Get all entries in the table (read-only).
        /// </summary>
        IReadOnlyList<WeightedEntry<T>> Entries { get; }
    }
}
