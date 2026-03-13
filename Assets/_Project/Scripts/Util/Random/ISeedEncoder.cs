namespace RhythmRogue.Util.Random
{
    /// <summary>
    /// Abstraction for encoding and decoding seed values.
    /// Separates the human-readable seed format (e.g. "BEAT-7X3K")
    /// from the numeric seed used by the random engine.
    /// 
    /// Different implementations can support different formats
    /// without changing any system that consumes seeds.
    /// </summary>
    public interface ISeedEncoder
    {
        /// <summary>
        /// Generate a random seed code.
        /// Uses system entropy (not deterministic) — call this once at run start.
        /// </summary>
        string GenerateCode();

        /// <summary>
        /// Convert a human-readable seed code to a numeric hash.
        /// Must be deterministic: the same code always produces the same hash.
        /// </summary>
        /// <param name="code">Seed code (e.g. "BEAT-7X3K").</param>
        /// <returns>Numeric seed value for use with System.Random.</returns>
        int Encode(string code);

        /// <summary>
        /// Validate whether a seed code matches the expected format.
        /// </summary>
        /// <param name="code">Seed code to validate.</param>
        /// <returns>True if the code is well-formed.</returns>
        bool IsValid(string code);

        /// <summary>
        /// Normalize a seed code (trim whitespace, fix casing, etc.)
        /// so that "beat-7x3k" and "BEAT-7X3K" produce identical results.
        /// </summary>
        string Normalize(string code);
    }
}
