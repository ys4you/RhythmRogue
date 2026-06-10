using System.Collections.Generic;
using System.Globalization;

namespace RhythmRogue.Util.Console
{
    /// <summary>
    /// Parsed command-line arguments in the POSIX style. Built by <see cref="Parse"/> from the raw
    /// tokens plus the command's option spec (the spec is needed so the parser knows which options
    /// consume a value). Supports:
    ///   -n 50  / -n50            short option with value
    ///   --amount 50 / --amount=50  long option with value
    ///   -v / --verbose            boolean flag
    ///   -h / --help               always recognized, sets HasHelp
    ///   anything else             a positional (a leading '-' followed by a digit, like -50, is a
    ///                             positional/negative number, not an option)
    /// Unknown options and missing values are collected in Errors rather than thrown.
    /// </summary>
    public sealed class ConsoleArgs
    {
        private readonly Dictionary<string, string> _values = new(); // key -> value ("" for flags)
        private readonly List<string> _positionals = new();
        private readonly List<string> _errors = new();

        public bool HasHelp { get; private set; }
        public IReadOnlyList<string> Positionals => _positionals;
        public IReadOnlyList<string> Errors => _errors;
        public bool HasErrors => _errors.Count > 0;

        private ConsoleArgs() { }

        /// <summary>True if the flag/option (by long name) was present.</summary>
        public bool Has(string longName) => longName != null && _values.ContainsKey(longName.ToLowerInvariant());

        /// <summary>Value of an option by long name, or <paramref name="fallback"/> if absent.</summary>
        public string Get(string longName, string fallback = null)
            => longName != null && _values.TryGetValue(longName.ToLowerInvariant(), out var v) ? v : fallback;

        public bool TryGetInt(string longName, out int value)
        {
            value = 0;
            var s = Get(longName);
            return s != null && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public bool TryGetFloat(string longName, out float value)
        {
            value = 0f;
            var s = Get(longName);
            return s != null && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static ConsoleArgs Parse(string[] raw, IReadOnlyList<ConsoleOption> options)
        {
            var result = new ConsoleArgs();
            if (raw == null) return result;

            var byLong = new Dictionary<string, ConsoleOption>();
            var byShort = new Dictionary<char, ConsoleOption>();
            if (options != null)
                foreach (var o in options)
                {
                    if (o.Long != null) byLong[o.Long] = o;
                    if (o.Short != '\0') byShort[o.Short] = o;
                }

            for (int i = 0; i < raw.Length; i++)
            {
                string tok = raw[i];
                if (string.IsNullOrEmpty(tok)) continue;

                if (tok == "-h" || tok == "--help") { result.HasHelp = true; continue; }

                // long option: --name or --name=value
                if (tok.StartsWith("--"))
                {
                    string body = tok.Substring(2);
                    string inlineVal = null;
                    int eq = body.IndexOf('=');
                    if (eq >= 0) { inlineVal = body.Substring(eq + 1); body = body.Substring(0, eq); }
                    string name = body.ToLowerInvariant();

                    if (!byLong.TryGetValue(name, out var opt)) { result._errors.Add($"unknown option --{name}"); continue; }
                    if (opt.TakesValue)
                    {
                        string val = inlineVal;
                        if (val == null)
                        {
                            if (i + 1 < raw.Length) val = raw[++i];
                            else { result._errors.Add($"option --{name} requires a value"); continue; }
                        }
                        result._values[opt.Long] = val;
                    }
                    else result._values[opt.Long] = "";
                    continue;
                }

                // short option: '-' + a non-digit (so -50 stays a positional/negative number)
                if (tok.Length >= 2 && tok[0] == '-' && tok[1] != '-' && !char.IsDigit(tok[1]))
                {
                    char shortName = tok[1];
                    if (!byShort.TryGetValue(shortName, out var opt)) { result._errors.Add($"unknown option -{shortName}"); continue; }
                    string key = opt.Long ?? shortName.ToString();
                    if (opt.TakesValue)
                    {
                        string val = tok.Length > 2 ? tok.Substring(2) : null; // attached, e.g. -n50
                        if (val == null)
                        {
                            if (i + 1 < raw.Length) val = raw[++i];
                            else { result._errors.Add($"option -{shortName} requires a value"); continue; }
                        }
                        result._values[key] = val;
                    }
                    else result._values[key] = "";
                    continue;
                }

                result._positionals.Add(tok);
            }

            return result;
        }
    }
}
