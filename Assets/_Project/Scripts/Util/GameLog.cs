using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace RhythmRogue.Util
{
    /// <summary>
    /// Conditional debug logger that compiles out of release builds.
    /// 
    /// Usage:
    ///   GameLog.Info("[BattleManager] Battle started");        // stripped in release
    ///   GameLog.Error("[BattleManager] No enemy data!");        // always included
    ///   GameLog.Error("[BattleManager] Chart load failed!");   // always included
    /// 
    /// [Conditional] attribute means the METHOD CALL (and argument evaluation)
    /// is stripped at compile time when UNITY_EDITOR and DEVELOPMENT_BUILD
    /// are not defined. Zero overhead in release.
    /// 
    /// Warnings and Errors are NOT conditional — they always fire
    /// because they indicate real problems worth catching in any build.
    /// </summary>
    public static class GameLog
    {
        /// <summary>
        /// Informational log. Stripped from release builds.
        /// Use for: system initialization, state transitions, debug tracing.
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Info(string message)
        {
            Debug.Log(message);
        }

        /// <summary>
        /// Warning log. Always included — indicates a recoverable problem.
        /// Use for: missing optional references, fallback paths, unexpected state.
        /// </summary>
        public static void Warn(string message)
        {
            Debug.LogWarning(message);
        }

        /// <summary>
        /// Error log. Always included — indicates a breaking problem.
        /// Use for: missing required references, failed initialization, data errors.
        /// </summary>
        public static void Error(string message)
        {
            Debug.LogError(message);
        }

        /// <summary>
        /// Informational exception log. Stripped from release builds.
        /// Use for: caught exceptions in non-critical paths where you want
        /// the full stack trace during development but zero overhead in release.
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void InfoException(System.Exception ex)
        {
            Debug.LogException(ex);
        }
    }
}
