using System;
using System.Collections.Generic;

namespace RhythmRogue.Util.Console
{
    /// <summary>
    /// A command defined inline by a delegate, for quick one-off commands that do not warrant a
    /// dedicated class. Mirrors the project's LambdaState helper in the FSM. For anything
    /// non-trivial or reused, prefer a dedicated IConsoleCommand class for clarity and testability.
    /// </summary>
    public sealed class LambdaConsoleCommand : IConsoleCommand
    {
        private static readonly string[] NoAliases = Array.Empty<string>();
        private readonly Func<string[], string> _run;

        public string Name { get; }
        public string Help { get; }
        public IReadOnlyList<string> Aliases { get; }

        public LambdaConsoleCommand(string name, string help, Func<string[], string> run, params string[] aliases)
        {
            Name = name;
            Help = help;
            _run = run ?? throw new ArgumentNullException(nameof(run));
            Aliases = aliases ?? NoAliases;
        }

        public string Execute(string[] args) => _run(args);
    }
}
