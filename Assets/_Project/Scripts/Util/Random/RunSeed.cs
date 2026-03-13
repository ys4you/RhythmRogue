using System;
using System.Collections.Generic;

namespace RhythmRogue.Util.Random
{
    /// <summary>
    /// Concrete per-run seed context.
    /// 
    /// Created once when a run starts (either from a player-entered code
    /// or a freshly generated one). Holds the master seed and lazily
    /// creates cached domain forks on first access.
    /// 
    /// Composition: uses an ISeedEncoder for code ↔ numeric conversion
    /// and SeededRandom for the actual RNG. Neither dependency is hard-coded
    /// at the interface level.
    /// </summary>
    public class RunSeed : IRunSeed
    {
        private readonly ISeededRandom _masterRandom;
        private readonly Dictionary<RandomDomain, ISeededRandom> _domainCache;

        /// <inheritdoc/>
        public string Code { get; }

        /// <inheritdoc/>
        public int NumericSeed => _masterRandom.Seed;

        /// <summary>
        /// Create a run seed from a human-readable code.
        /// </summary>
        /// <param name="code">Seed code (e.g. "BEAT-7X3K").</param>
        /// <param name="encoder">Encoder to convert the code to a numeric seed.</param>
        public RunSeed(string code, ISeedEncoder encoder)
        {
            if (encoder == null) throw new ArgumentNullException(nameof(encoder));

            Code = encoder.Normalize(code);
            int numericSeed = encoder.Encode(Code);

            _masterRandom = new SeededRandom(numericSeed);
            _domainCache = new Dictionary<RandomDomain, ISeededRandom>();
        }

        /// <summary>
        /// Create a run seed from a pre-computed numeric seed.
        /// The code is stored as-is (useful for testing or internal use).
        /// </summary>
        /// <param name="code">Display code for UI.</param>
        /// <param name="numericSeed">Pre-computed numeric seed.</param>
        public RunSeed(string code, int numericSeed)
        {
            Code = code;
            _masterRandom = new SeededRandom(numericSeed);
            _domainCache = new Dictionary<RandomDomain, ISeededRandom>();
        }

        /// <inheritdoc/>
        public ISeededRandom GetRandom(RandomDomain domain)
        {
            if (!_domainCache.TryGetValue(domain, out ISeededRandom rng))
            {
                // Fork from the master seed using the domain name as salt.
                // This is deterministic — same master seed + same domain = same fork.
                rng = _masterRandom.Fork(domain.ToString());
                _domainCache[domain] = rng;
            }

            return rng;
        }

        /// <inheritdoc/>
        public ISeededRandom GetRandom(RandomDomain domain, int subIndex)
        {
            // Sub-forks are NOT cached — they're typically used once per
            // area or encounter and the caller holds the reference.
            ISeededRandom domainRng = GetRandom(domain);
            return domainRng.Fork(subIndex);
        }

        /// <summary>
        /// Factory helper: create a RunSeed with a randomly generated code.
        /// </summary>
        public static RunSeed CreateRandom(ISeedEncoder encoder)
        {
            string code = encoder.GenerateCode();
            return new RunSeed(code, encoder);
        }
    }
}
