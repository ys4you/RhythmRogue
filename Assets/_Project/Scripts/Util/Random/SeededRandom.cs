using System;
using System.Collections.Generic;

namespace RhythmRogue.Util.Random
{
    /// <summary>
    /// Deterministic random number generator built on System.Random.
    /// 
    /// Supports forking: each game system (map gen, chart gen, enemy selection,
    /// rewards, shop) creates its own fork so their random sequences are
    /// independent. Adding a random call in the map generator won't shift
    /// the chart generator's output — essential for seed reproducibility.
    /// 
    /// SOLID breakdown:
    /// - S: Only generates random numbers from a seed. No formatting, no game logic.
    /// - O: Extended via Fork() to create sub-streams without modifying this class.
    /// - L: Any ISeededRandom works identically from the consumer's perspective.
    /// - I: Consumers see only the ISeededRandom interface.
    /// - D: Game systems depend on ISeededRandom, not this concrete class.
    /// </summary>
    public class SeededRandom : ISeededRandom
    {
        private readonly System.Random _rng;

        /// <inheritdoc/>
        public int Seed { get; }

        /// <summary>
        /// Create a new seeded random instance.
        /// </summary>
        /// <param name="seed">Numeric seed. Same seed = same sequence.</param>
        public SeededRandom(int seed)
        {
            Seed = seed;
            _rng = new System.Random(seed);
        }

        /// <inheritdoc/>
        public int Next() => _rng.Next();

        /// <inheritdoc/>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Must be positive.");

            return _rng.Next(maxExclusive);
        }

        /// <inheritdoc/>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
                throw new ArgumentException(
                    $"min ({minInclusive}) must be less than max ({maxExclusive}).");

            return _rng.Next(minInclusive, maxExclusive);
        }

        /// <inheritdoc/>
        public float NextFloat() => (float)_rng.NextDouble();

        /// <inheritdoc/>
        public float Range(float min, float max)
        {
            return min + (float)_rng.NextDouble() * (max - min);
        }

        /// <inheritdoc/>
        public bool Chance(float probability)
        {
            return NextFloat() < probability;
        }

        /// <inheritdoc/>
        public T Pick<T>(IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0)
                throw new ArgumentException("Cannot pick from an empty or null collection.", nameof(items));

            return items[Next(items.Count)];
        }

        /// <inheritdoc/>
        public void Shuffle<T>(IList<T> list)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));

            // Fisher-Yates shuffle (inside-out)
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <inheritdoc/>
        public ISeededRandom Fork(string salt)
        {
            if (string.IsNullOrEmpty(salt))
                throw new ArgumentException("Fork salt cannot be null or empty.", nameof(salt));

            int derivedSeed = DeriveChildSeed(salt.GetHashCode());
            return new SeededRandom(derivedSeed);
        }

        /// <inheritdoc/>
        public ISeededRandom Fork(int salt)
        {
            int derivedSeed = DeriveChildSeed(salt);
            return new SeededRandom(derivedSeed);
        }

        /// <summary>
        /// Combine the parent seed with a salt to produce a child seed.
        /// Uses hash combining to ensure good distribution even with
        /// sequential salt values (e.g. area 0, area 1, area 2).
        /// </summary>
        private int DeriveChildSeed(int salt)
        {
            unchecked
            {
                // Hash combine: parent seed × prime + salt
                // Same approach used by .NET's HashCode.Combine
                int hash = Seed;
                hash = hash * 397 ^ salt;
                hash = hash * 397 ^ (salt >> 16);
                return hash;
            }
        }
    }
}
