namespace RhythmRogue.Util.Extensions
{
    /// <summary>
    /// Extension methods for strings.
    /// General-purpose text manipulation for UI display,
    /// debug logging, and seed formatting.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Check if a string is null, empty, or whitespace.
        /// Shorthand for string.IsNullOrWhiteSpace.
        /// 
        ///   if (seedInput.IsNullOrEmpty())
        ///       GenerateRandomSeed();
        /// </summary>
        public static bool IsNullOrEmpty(this string str)
        {
            return string.IsNullOrWhiteSpace(str);
        }

        /// <summary>
        /// Truncate a string to a maximum length, adding "..." if truncated.
        /// 
        ///   // Truncate relic description for tooltip
        ///   tooltip.text = description.Truncate(50);
        /// </summary>
        public static string Truncate(this string str, int maxLength, string suffix = "...")
        {
            if (string.IsNullOrEmpty(str) || str.Length <= maxLength)
                return str;

            return str.Substring(0, maxLength - suffix.Length) + suffix;
        }

        /// <summary>
        /// Wrap a string in Unity rich text color tags.
        /// 
        ///   Debug.Log("Perfect!".Colored("green"));
        ///   Debug.Log($"+{damage}".Colored("#FF5555"));
        /// </summary>
        public static string Colored(this string str, string color)
        {
            return $"<color={color}>{str}</color>";
        }

        /// <summary>
        /// Wrap in bold rich text tags.
        /// 
        ///   Debug.Log("COMBO BROKEN!".Bold());
        /// </summary>
        public static string Bold(this string str)
        {
            return $"<b>{str}</b>";
        }

        /// <summary>
        /// Wrap in italic rich text tags.
        /// </summary>
        public static string Italic(this string str)
        {
            return $"<i>{str}</i>";
        }

        /// <summary>
        /// Set rich text size.
        /// 
        ///   label.text = "x3.0".Sized(24);
        /// </summary>
        public static string Sized(this string str, int size)
        {
            return $"<size={size}>{str}</size>";
        }
    }
}
