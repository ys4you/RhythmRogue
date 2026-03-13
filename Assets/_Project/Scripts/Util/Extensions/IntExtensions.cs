using UnityEngine;

namespace RhythmRogue.Util.Extensions
{
    /// <summary>
    /// Extension methods for int values.
    /// </summary>
    public static class IntExtensions
    {
        /// <summary>
        /// Clamp between min and max (inclusive).
        /// 
        ///   int lane = inputLane.Clamp(0, 3);
        /// </summary>
        public static int Clamp(this int value, int min, int max)
        {
            return Mathf.Clamp(value, min, max);
        }

        /// <summary>
        /// Modular wrap — always returns a positive result.
        /// C#'s % operator returns negative for negative inputs,
        /// which breaks lane wrapping and array indexing.
        /// 
        ///   // Wrap lane index: -1 becomes 3, 4 becomes 0
        ///   int lane = rawLane.Wrap(4);
        /// </summary>
        public static int Wrap(this int value, int length)
        {
            return ((value % length) + length) % length;
        }

        /// <summary>
        /// Check if a value is within an inclusive range.
        /// 
        ///   if (combo.IsBetween(10, 20))
        ///       TriggerComboBonus();
        /// </summary>
        public static bool IsBetween(this int value, int min, int max)
        {
            return value >= min && value <= max;
        }
    }
}
