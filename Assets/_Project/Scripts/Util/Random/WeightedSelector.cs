using System;
using System.Collections.Generic;

namespace RhythmRogue.Util.Random
{
    /// <summary>
    /// Weighted random selector using cumulative weight binary search.
    /// 
    /// Pre-computes a cumulative weight array on construction for
    /// O(log n) picks. Ideal for tables that are built once and
    /// picked from many times (pattern pools, reward tables, loot).
    /// 
    /// For tables that change frequently, rebuild via WeightedTable.Build().
    /// 
    /// SOLID breakdown:
    /// - S: Only selects items from a weighted table. No table building logic.
    /// - O: New selection strategies can implement IWeightedSelector without changes.
    /// - L: Substitutable anywhere IWeightedSelector is expected.
    /// - I: Consumers see only the IWeightedSelector interface.
    /// - D: Depends on ISeededRandom abstraction for random input.
    /// </summary>
    /// <typeparam name="T">Type of item in the table.</typeparam>
    public class WeightedSelector<T> : IWeightedSelector<T>
    {
        private readonly List<WeightedEntry<T>> _entries;
        private readonly float[] _cumulative;

        /// <inheritdoc/>
        public int Count => _entries.Count;

        /// <inheritdoc/>
        public float TotalWeight { get; }

        /// <inheritdoc/>
        public IReadOnlyList<WeightedEntry<T>> Entries => _entries;

        /// <summary>
        /// Create a selector from a list of weighted entries.
        /// Pre-computes cumulative weights for fast binary search picks.
        /// 
        /// Prefer using WeightedTable.Build() for fluent construction.
        /// </summary>
        /// <param name="entries">
        /// Entries with positive weights. Empty entries or zero/negative
        /// weights will throw.
        /// </param>
        public WeightedSelector(IEnumerable<WeightedEntry<T>> entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));

            _entries = new List<WeightedEntry<T>>(entries);

            if (_entries.Count == 0)
                throw new ArgumentException("Weighted table must have at least one entry.", nameof(entries));

            _cumulative = new float[_entries.Count];
            float sum = 0f;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Weight <= 0f)
                    throw new ArgumentException(
                        $"Entry [{i}] '{_entries[i].Item}' has invalid weight {_entries[i].Weight}. " +
                        "Weights must be positive.", nameof(entries));

                sum += _entries[i].Weight;
                _cumulative[i] = sum;
            }

            TotalWeight = sum;
        }

        /// <inheritdoc/>
        public T Pick(ISeededRandom rng)
        {
            if (rng == null)
                throw new ArgumentNullException(nameof(rng));

            float roll = rng.NextFloat() * TotalWeight;
            int index = FindIndex(roll);
            return _entries[index].Item;
        }

        /// <inheritdoc/>
        public List<T> Pick(ISeededRandom rng, int count)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), "Must be positive.");

            var results = new List<T>(count);

            for (int i = 0; i < count; i++)
            {
                results.Add(Pick(rng));
            }

            return results;
        }

        /// <inheritdoc/>
        public List<T> PickUnique(ISeededRandom rng, int count)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), "Must be positive.");
            if (count > _entries.Count)
                throw new ArgumentException(
                    $"Cannot pick {count} unique items from a table of {_entries.Count}.", nameof(count));

            // Build a temporary table excluding already-picked indices
            var remaining = new List<WeightedEntry<T>>(_entries);
            var results = new List<T>(count);

            for (int i = 0; i < count; i++)
            {
                // Build cumulative for remaining entries
                var tempSelector = new WeightedSelector<T>(remaining);
                T picked = tempSelector.Pick(rng);
                results.Add(picked);

                // Remove picked item from remaining pool
                remaining.RemoveAll(e => EqualityComparer<T>.Default.Equals(e.Item, picked));
            }

            return results;
        }

        /// <inheritdoc/>
        public float GetProbability(int index)
        {
            if (index < 0 || index >= _entries.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _entries[index].Weight / TotalWeight;
        }

        /// <summary>
        /// Binary search the cumulative weight array to find which
        /// entry a roll value falls into. O(log n).
        /// </summary>
        private int FindIndex(float roll)
        {
            int lo = 0;
            int hi = _cumulative.Length - 1;

            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;

                if (_cumulative[mid] <= roll)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            return lo;
        }
    }
}
