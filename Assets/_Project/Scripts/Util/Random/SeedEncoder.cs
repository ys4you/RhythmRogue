using System;
using System.Text;
using System.Text.RegularExpressions;

namespace RhythmRogue.Util.Random
{
    /// <summary>
    /// Seed encoder for the GDD format: 4 alphanumeric characters, dash,
    /// 4 alphanumeric characters (e.g. "BEAT-7X3K").
    /// 
    /// Character set uses uppercase letters and digits, excluding
    /// ambiguous characters (O/0, I/1, L) to keep codes easy to read
    /// and type when shared between players.
    /// 
    /// Encoding is deterministic: the same code always hashes to the
    /// same numeric seed via FNV-1a.
    /// </summary>
    public class SeedEncoder : ISeedEncoder
    {
        // Unambiguous character set — no O/0, I/1, L confusion
        private const string CharSet = "ABCDEFGHJKMNPQRSTVWXYZ23456789";
        private static readonly Regex FormatPattern = new(@"^[A-Z0-9]{4}-[A-Z0-9]{4}$", RegexOptions.Compiled);

        private readonly System.Random _entropySource;

        /// <summary>
        /// Create a seed encoder.
        /// </summary>
        /// <param name="entropySource">
        /// Optional random source for GenerateCode().
        /// If null, uses a new System.Random seeded from system entropy.
        /// Only used for code generation, not for encoding.
        /// </param>
        public SeedEncoder(System.Random entropySource = null)
        {
            _entropySource = entropySource ?? new System.Random();
        }

        /// <inheritdoc/>
        public string GenerateCode()
        {
            var sb = new StringBuilder(9); // XXXX-XXXX = 9 chars

            for (int i = 0; i < 8; i++)
            {
                if (i == 4) sb.Append('-');
                sb.Append(CharSet[_entropySource.Next(CharSet.Length)]);
            }

            return sb.ToString();
        }

        /// <inheritdoc/>
        public int Encode(string code)
        {
            string normalized = Normalize(code);

            if (!IsValid(normalized))
            {
                throw new ArgumentException(
                    $"Invalid seed code format: \"{code}\". Expected format: XXXX-XXXX " +
                    "(uppercase alphanumeric, e.g. BEAT-7X3K).", nameof(code));
            }

            return Fnv1aHash(normalized);
        }

        /// <inheritdoc/>
        public bool IsValid(string code)
        {
            if (string.IsNullOrEmpty(code)) return false;
            return FormatPattern.IsMatch(code);
        }

        /// <inheritdoc/>
        public string Normalize(string code)
        {
            if (string.IsNullOrEmpty(code)) return string.Empty;

            string trimmed = code.Trim().ToUpperInvariant();

            // If someone types 8 characters without the dash, insert it
            if (trimmed.Length == 8 && !trimmed.Contains('-'))
            {
                trimmed = trimmed.Insert(4, "-");
            }

            // Replace commonly confused characters
            trimmed = trimmed
                .Replace('O', '0')
                .Replace('I', '1')
                .Replace('L', '1');

            return trimmed;
        }

        /// <summary>
        /// FNV-1a hash — fast, well-distributed, deterministic.
        /// Used because it produces good distribution for short strings
        /// and is dead simple to implement consistently across platforms.
        /// </summary>
        private static int Fnv1aHash(string input)
        {
            unchecked
            {
                const uint fnvPrime = 16777619;
                const uint fnvOffset = 2166136261;

                uint hash = fnvOffset;

                for (int i = 0; i < input.Length; i++)
                {
                    hash ^= input[i];
                    hash *= fnvPrime;
                }

                return (int)hash;
            }
        }
    }
}
