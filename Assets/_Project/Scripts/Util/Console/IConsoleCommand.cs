using System.Collections.Generic;

namespace RhythmRogue.Util.Console
{
    /// <summary>
    /// Command-pattern contract for the developer console. Each command is a self-contained unit:
    /// it knows its own name, help text, and how to run itself. The console core depends only on
    /// this interface, so new commands are added by implementing it and registering an instance,
    /// with zero edits to the console itself (open for extension, closed for modification).
    ///
    /// This framework lives in Util and intentionally has no Unity-UI or game knowledge, so it can
    /// be reused for any text-command surface (dev console now, possibly an in-game prompt later).
    /// </summary>
    public interface IConsoleCommand
    {
        /// <summary>Primary token typed to invoke the command. Lower-case, no spaces.</summary>
        string Name { get; }

        /// <summary>One-line usage and description, shown by the built-in 'help' command.</summary>
        string Help { get; }

        /// <summary>Optional alternate tokens that also invoke this command. May be empty, never null.</summary>
        IReadOnlyList<string> Aliases { get; }

        /// <summary>
        /// Run the command. <paramref name="args"/> are the whitespace-split tokens that followed
        /// the command name (never null). Return text to print to the console; null or empty prints
        /// nothing. Throwing is safe: the console catches it and prints the message.
        /// </summary>
        string Execute(string[] args);
    }
}
