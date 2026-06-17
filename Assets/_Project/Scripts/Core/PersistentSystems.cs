using UnityEngine;

namespace RhythmRogue.Core
{
    /// <summary>
    /// Creates the genuinely global, persistent overlays exactly once at startup instead of
    /// placing a copy in every scene.
    ///
    /// Both <see cref="GlobalPauseManager"/> and <see cref="CRTOverlay"/> build their own UI in
    /// code and need no scene references, so the cleanest setup is to auto-create them here and
    /// keep ZERO copies in any scene. That also removes the duplicate-instance warnings: with no
    /// placed copies, a reloaded scene (e.g. returning to the main menu) never spawns a second
    /// instance that has to self-destruct against the survivor.
    ///
    /// Runs once per play session, after the first scene has loaded so the pause manager can read
    /// the active scene name correctly. Touching <c>.Instance</c> forces the Singleton to create
    /// the object (DontDestroyOnLoad), after which it persists for the rest of the session.
    ///
    /// Note: the per-battle <see cref="Conductor"/> is deliberately NOT created here. It is
    /// scene-scoped and lives in the battle scene only.
    /// </summary>
    public static class PersistentSystems
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // The Singleton getter finds an existing instance or creates one. Since these are no
            // longer placed in scenes, this is what brings them into existence each session.
            _ = CRTOverlay.Instance;
            _ = GlobalPauseManager.Instance;
        }
    }
}
