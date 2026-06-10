using System.Text;

namespace RhythmRogue.Util.Console
{
    /// <summary>
    /// Declares one command-line option in the POSIX style: an optional short form (-n), an
    /// optional long form (--amount), whether it consumes a value, and a help description. Commands
    /// expose their options so the parser knows which tokens take values and so -h/--help can print
    /// usage automatically.
    /// </summary>
    public sealed class ConsoleOption
    {
        /// <summary>Short form character, e.g. 'n' for -n. '\0' means no short form.</summary>
        public char Short { get; }

        /// <summary>Long form (lower-case, no dashes), e.g. "amount" for --amount. null means none.</summary>
        public string Long { get; }

        /// <summary>True if the option consumes the next token as a value (e.g. -n 50).</summary>
        public bool TakesValue { get; }

        /// <summary>Placeholder shown in usage for the value, e.g. "value" -&gt; "-n &lt;value&gt;".</summary>
        public string ValueName { get; }

        /// <summary>One-line description shown in usage.</summary>
        public string Description { get; }

        public ConsoleOption(char shortName, string longName, bool takesValue, string description, string valueName = "value")
        {
            Short = shortName;
            Long = string.IsNullOrWhiteSpace(longName) ? null : longName.Trim().ToLowerInvariant();
            TakesValue = takesValue;
            Description = description ?? "";
            ValueName = string.IsNullOrWhiteSpace(valueName) ? "value" : valueName;
        }

        /// <summary>A boolean flag (no value), e.g. -v / --verbose.</summary>
        public static ConsoleOption Flag(char shortName, string longName, string description)
            => new ConsoleOption(shortName, longName, false, description);

        /// <summary>An option that takes a value, e.g. -n 50 / --amount 50.</summary>
        public static ConsoleOption Value(char shortName, string longName, string description, string valueName = "value")
            => new ConsoleOption(shortName, longName, true, description, valueName);

        /// <summary>Renders the option for a usage line, e.g. "-n, --amount &lt;value&gt;".</summary>
        public string Signature()
        {
            var sb = new StringBuilder();
            if (Short != '\0') { sb.Append('-').Append(Short); if (Long != null) sb.Append(", "); }
            else sb.Append("    ");
            if (Long != null) sb.Append("--").Append(Long);
            if (TakesValue) sb.Append(" <").Append(ValueName).Append('>');
            return sb.ToString();
        }
    }
}
