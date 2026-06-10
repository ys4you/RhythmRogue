using System;
using System.Collections.Generic;
using System.Text;

namespace RhythmRogue.Util.Console
{
    /// <summary>
    /// Base for commands that want POSIX-style argument handling. It parses the raw tokens against
    /// the command's declared <see cref="Options"/>, transparently handles -h/--help by printing an
    /// auto-generated usage block, reports parse errors, and then hands a typed <see cref="ConsoleArgs"/>
    /// to <see cref="Run"/>. Commands therefore declare their flags once and never touch raw strings
    /// or reimplement help.
    ///
    /// Still an IConsoleCommand, so the console core treats these identically to any other command.
    /// </summary>
    public abstract class ConsoleCommandBase : IConsoleCommand
    {
        private static readonly string[] NoAliases = Array.Empty<string>();
        private static readonly ConsoleOption[] NoOptions = Array.Empty<ConsoleOption>();

        public abstract string Name { get; }

        /// <summary>Short, one-line description (shown in the 'help' listing). No leading name.</summary>
        public abstract string Help { get; }

        public virtual IReadOnlyList<string> Aliases => NoAliases;

        /// <summary>Options this command accepts. Default: none (just the implicit -h/--help).</summary>
        public virtual IReadOnlyList<ConsoleOption> Options => NoOptions;

        public string Execute(string[] args)
        {
            var parsed = ConsoleArgs.Parse(args, Options);
            if (parsed.HasHelp) return Usage();
            if (parsed.HasErrors) return string.Join("\n", parsed.Errors) + $"\ntry '{Name} -h'.";
            return Run(parsed);
        }

        /// <summary>Run the command with already-parsed, validated arguments.</summary>
        protected abstract string Run(ConsoleArgs args);

        /// <summary>Auto-generated usage block: synopsis, description, and the option table.</summary>
        public string Usage()
        {
            var sb = new StringBuilder();
            sb.Append("usage: ").Append(Name);
            if (Options.Count > 0) sb.Append(" [options]");
            sb.Append('\n').Append("  ").Append(Help);
            sb.Append('\n').Append("options:");
            AppendOption(sb, "-h, --help", "show this help");
            foreach (var o in Options) AppendOption(sb, o.Signature(), o.Description);
            return sb.ToString();
        }

        private static void AppendOption(StringBuilder sb, string signature, string description)
        {
            const int col = 26;
            sb.Append('\n').Append("  ").Append(signature);
            int pad = col - signature.Length;
            sb.Append(pad > 0 ? new string(' ', pad) : "  ");
            sb.Append(description);
        }
    }
}
