using System;
using System.Collections.Generic;
using System.Text;

namespace RhythmRogue.Util.Console
{
    /// <summary>
    /// Engine-agnostic developer console core: a command registry, a parser/dispatcher, an output
    /// log, and input history. Knows nothing about Unity UI or about the game, so it is reusable and
    /// unit-testable. A view (e.g. DevConsoleUI) drives it, and game-specific commands are
    /// registered from outside.
    ///
    /// Command pattern: every action is an IConsoleCommand. Registration is the only extension
    /// point, so adding commands never modifies this class. Ships with two built-in, game-agnostic
    /// commands: 'help' and 'clear'.
    /// </summary>
    public class DeveloperConsole
    {
        private readonly Dictionary<string, IConsoleCommand> _byToken = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<IConsoleCommand> _commands = new();   // distinct instances, for listing
        private readonly List<string> _log = new();
        private readonly List<string> _history = new();

        /// <summary>Max lines kept in the output log; older lines are dropped past this.</summary>
        public int MaxLogLines { get; set; } = 200;

        public IReadOnlyList<string> Log => _log;
        public IReadOnlyList<string> History => _history;
        public IReadOnlyList<IConsoleCommand> Commands => _commands;

        public DeveloperConsole()
        {
            Register(new LambdaConsoleCommand("help", "list all commands, or show help for one", HelpCommand, "?"));
            Register(new LambdaConsoleCommand("clear", "clear the console output", ClearCommand, "cls"));
        }

        /// <summary>
        /// Register a command under its name and any aliases. A later registration for the same
        /// token overrides the earlier one, so games can replace built-ins if needed.
        /// </summary>
        public void Register(IConsoleCommand command)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.Name)) return;
            if (!_commands.Contains(command)) _commands.Add(command);
            _byToken[command.Name] = command;
            if (command.Aliases != null)
                foreach (var alias in command.Aliases)
                    if (!string.IsNullOrWhiteSpace(alias)) _byToken[alias] = command;
        }

        /// <summary>Append text to the output log. Multi-line text is split into individual lines.</summary>
        public void Print(string text)
        {
            if (text == null) return;
            foreach (var line in text.Split('\n'))
                _log.Add(line.TrimEnd('\r'));
            int overflow = _log.Count - MaxLogLines;
            if (overflow > 0) _log.RemoveRange(0, overflow);
        }

        /// <summary>Parse and run a typed line. Records it in history and echoes it to the log.</summary>
        public void Execute(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            line = line.Trim();
            _history.Add(line);

            Print("> " + line);

            var tokens = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            string name = tokens[0];
            string[] args = tokens.Length > 1 ? new string[tokens.Length - 1] : Array.Empty<string>();
            for (int i = 1; i < tokens.Length; i++) args[i - 1] = tokens[i];

            if (!_byToken.TryGetValue(name, out var command))
            {
                Print($"unknown command '{name}'. type 'help'.");
                return;
            }

            try
            {
                string result = command.Execute(args);
                if (!string.IsNullOrEmpty(result)) Print(result);
            }
            catch (Exception e)
            {
                Print($"error: {e.Message}");
            }
        }

        /// <summary>
        /// Tab-completion. Completes the command token while it is being typed, or delegates to the
        /// command's IArgumentCompleter (if any) once arguments are being typed. Returns the
        /// (possibly extended) input plus the candidate list for the UI to display when ambiguous.
        /// </summary>
        public CompletionResult Complete(string input)
        {
            input ??= "";
            bool trailingSpace = input.Length > 0 && char.IsWhiteSpace(input[input.Length - 1]);
            var tokens = input.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

            // Still on the command token (nothing typed yet, or one token with no trailing space).
            if (tokens.Length == 0 || (tokens.Length == 1 && !trailingSpace))
            {
                string prefix = tokens.Length == 0 ? "" : tokens[0];
                var matches = new List<string>();
                foreach (var key in _byToken.Keys)
                    if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) matches.Add(key);
                matches.Sort(StringComparer.OrdinalIgnoreCase);
                return Resolve(input, prefix, matches);
            }

            // Completing an argument: hand off to the command's completer if it provides one.
            if (_byToken.TryGetValue(tokens[0], out var cmd) && cmd is IArgumentCompleter completer)
            {
                string partial = trailingSpace ? "" : tokens[tokens.Length - 1];
                int priorCount = tokens.Length - 1 - (trailingSpace ? 0 : 1);
                if (priorCount < 0) priorCount = 0;
                var prior = new string[priorCount];
                for (int i = 0; i < priorCount; i++) prior[i] = tokens[1 + i];

                var matches = new List<string>();
                foreach (var c in completer.CompleteArguments(prior, partial))
                    if (!string.IsNullOrEmpty(c) && c.StartsWith(partial, StringComparison.OrdinalIgnoreCase)) matches.Add(c);
                matches.Sort(StringComparer.OrdinalIgnoreCase);
                return Resolve(input, partial, matches);
            }

            return new CompletionResult { Completed = input, Matches = Array.Empty<string>() };
        }

        // Replaces the trailing `partial` of `input` with the best completion: the full word if
        // there is exactly one match, otherwise the longest common prefix of all matches.
        private static CompletionResult Resolve(string input, string partial, List<string> matches)
        {
            var result = new CompletionResult { Completed = input, Matches = matches };
            if (matches.Count == 0) return result;

            string head = input.Substring(0, input.Length - partial.Length);
            if (matches.Count == 1)
            {
                result.Completed = head + matches[0] + " ";
                return result;
            }
            string lcp = LongestCommonPrefix(matches);
            if (lcp.Length > partial.Length) result.Completed = head + lcp;
            return result;
        }

        private static string LongestCommonPrefix(List<string> items)
        {
            if (items.Count == 0) return "";
            string first = items[0];
            int len = first.Length;
            for (int i = 1; i < items.Count; i++)
            {
                string s = items[i];
                int j = 0;
                while (j < len && j < s.Length && char.ToLowerInvariant(first[j]) == char.ToLowerInvariant(s[j])) j++;
                len = j;
                if (len == 0) break;
            }
            return first.Substring(0, len);
        }

        private string HelpCommand(string[] args)
        {
            // 'help <name>' shows full usage for command-base commands, else the one-liner.
            if (args.Length > 0 && _byToken.TryGetValue(args[0], out var c))
                return c is ConsoleCommandBase b ? b.Usage() : c.Help;

            var sb = new StringBuilder("commands (type '<name> -h' for details):");
            foreach (var cmd in _commands)
            {
                sb.Append('\n').Append("  ").Append(cmd.Name.PadRight(10)).Append(' ').Append(cmd.Help);
            }
            return sb.ToString();
        }

        // Clears the log. Note the echoed "> clear" line is cleared too, leaving a clean screen.
        private string ClearCommand(string[] args)
        {
            _log.Clear();
            return null;
        }
    }
}
