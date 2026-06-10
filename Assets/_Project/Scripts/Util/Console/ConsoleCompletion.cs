using System.Collections.Generic;

namespace RhythmRogue.Util.Console
{
    /// <summary>Result of a tab-completion attempt.</summary>
    public sealed class CompletionResult
    {
        /// <summary>The input after completion (may be unchanged if nothing matched or it was ambiguous).</summary>
        public string Completed;

        /// <summary>
        /// Candidate tokens. More than one means the completion was ambiguous and a UI can list them;
        /// zero means no match; one means it was filled in.
        /// </summary>
        public IReadOnlyList<string> Matches = System.Array.Empty<string>();
    }

    /// <summary>
    /// Optional hook a command implements to offer tab-completions for its own arguments. The
    /// console handles command-name completion itself and only calls this once arguments are being
    /// typed, so argument knowledge stays in the command and the console core remains generic.
    /// </summary>
    public interface IArgumentCompleter
    {
        /// <summary>
        /// Candidate completions for the current (last) argument token. <paramref name="priorArgs"/>
        /// are the completed argument tokens before it (excluding the command name and the partial).
        /// <paramref name="partial"/> is the token being typed (may be empty). The console filters the
        /// returned list by <paramref name="partial"/>, so returning the full candidate set is fine.
        /// </summary>
        IEnumerable<string> CompleteArguments(string[] priorArgs, string partial);
    }
}
