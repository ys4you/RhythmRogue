using System;
using System.Collections.Generic;

namespace RhythmRogue.Util.Random
{
    /// <summary>
    /// Fluent builder for constructing weighted selection tables.
    /// 
    /// Separates table construction from selection logic (SRP).
    /// Build the table once, pick from it many times.
    /// 
    /// Usage:
    ///   var table = WeightedTable&lt;string&gt;.Build()
    ///       .Add("Common", 60f)
    ///       .Add("Uncommon", 25f)
    ///       .Add("Rare", 10f)
    ///       .Add("Legendary", 5f)
    ///       .Done();
    ///
    ///   string item = table.Pick(rng);
    /// </summary>
    /// <typeparam name="T">Type of item in the table.</typeparam>
    public static class WeightedTable<T>
    {
        /// <summary>
        /// Start building a new weighted table.
        /// </summary>
        public static Builder Build() => new Builder();

        /// <summary>
        /// Create a selector directly from an existing list of entries.
        /// </summary>
        public static IWeightedSelector<T> From(IEnumerable<WeightedEntry<T>> entries)
        {
            return new WeightedSelector<T>(entries);
        }

        /// <summary>
        /// Create a selector from parallel arrays of items and weights.
        /// Convenience for data-driven setups where items and weights
        /// come from separate sources (e.g. ScriptableObject arrays).
        /// </summary>
        public static IWeightedSelector<T> From(IReadOnlyList<T> items, IReadOnlyList<float> weights)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (weights == null) throw new ArgumentNullException(nameof(weights));
            if (items.Count != weights.Count)
                throw new ArgumentException(
                    $"Items ({items.Count}) and weights ({weights.Count}) must have the same length.");

            var entries = new List<WeightedEntry<T>>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                entries.Add(new WeightedEntry<T>(items[i], weights[i]));
            }

            return new WeightedSelector<T>(entries);
        }

        /// <summary>
        /// Fluent builder for adding entries one at a time.
        /// </summary>
        public class Builder
        {
            private readonly List<WeightedEntry<T>> _entries = new();

            /// <summary>
            /// Add an item with a weight to the table.
            /// </summary>
            /// <param name="item">Item to add.</param>
            /// <param name="weight">Relative weight (must be positive).</param>
            /// <returns>This builder for chaining.</returns>
            public Builder Add(T item, float weight)
            {
                _entries.Add(new WeightedEntry<T>(item, weight));
                return this;
            }

            /// <summary>
            /// Add an item only if a condition is met.
            /// Useful for conditionally including entries based on
            /// unlocks, area progression, or active relics.
            /// </summary>
            /// <param name="condition">If false, the item is skipped.</param>
            /// <param name="item">Item to add.</param>
            /// <param name="weight">Relative weight.</param>
            /// <returns>This builder for chaining.</returns>
            public Builder AddIf(bool condition, T item, float weight)
            {
                if (condition)
                    _entries.Add(new WeightedEntry<T>(item, weight));

                return this;
            }

            /// <summary>
            /// Add multiple entries from a collection.
            /// </summary>
            public Builder AddRange(IEnumerable<WeightedEntry<T>> entries)
            {
                _entries.AddRange(entries);
                return this;
            }

            /// <summary>
            /// Finalize the table and return a selector.
            /// </summary>
            public IWeightedSelector<T> Done()
            {
                return new WeightedSelector<T>(_entries);
            }
        }
    }
}
