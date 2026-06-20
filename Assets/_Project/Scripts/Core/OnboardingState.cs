using UnityEngine;

namespace RhythmRogue.Core
{
    /// <summary>
    /// Tracks whether the player has been through the onboarding, so the first New Run can route a
    /// brand-new player into it once. Lives in PlayerPrefs so it survives quitting and stays
    /// independent of the run/save system.
    ///
    /// Completion is stamped with a BUILD SIGNATURE (game version + Unity's per-build GUID) rather
    /// than a plain on/off flag. This matters because PlayerPrefs live on the player's machine, not
    /// in the build: a fresh build would otherwise inherit a "seen it" flag left behind by an
    /// earlier build and never show the onboarding. With a signature, every new build produces a
    /// value that will not match the stored stamp, so IsComplete reports false the first time that
    /// build runs, onboarding shows once, and MarkComplete writes the new stamp. Running the same
    /// build again matches the stamp and skips onboarding as expected. That is what makes a fresh
    /// playtest build start "false" without any manual reset on the player's machine.
    ///
    /// In the Editor the signature is constant (buildGUID is empty and the version is fixed), so
    /// completion persists normally between play sessions; use the dev cheat panel (F10) to force
    /// it back while iterating.
    ///
    /// Release note: keying on buildGUID means every distributed build re-shows onboarding once,
    /// which is what you want during playtesting. Before shipping, if you would rather NOT re-show
    /// it on every patch, change <see cref="CurrentSignature"/> to use only Application.version (or
    /// a dedicated, manually bumped onboarding-version constant) so routine rebuilds do not re-arm
    /// it. When a real meta-progression save exists, fold this into it so it travels with the
    /// profile.
    /// </summary>
    public static class OnboardingState
    {
        // Stores the build signature at which onboarding was completed. Absent/empty means never
        // completed on any build. Deliberately a NEW key: any legacy on/off flag is ignored, so the
        // first build carrying this change re-shows onboarding regardless of old saved state.
        private const string StampKey = "rr_onboarding_stamp";

        // Legacy key from the old on/off flag. Cleared on Reset so it does not linger in prefs.
        private const string LegacyCompleteKey = "rr_onboarding_complete";

        /// <summary>
        /// Identifies this exact build: the project version plus the GUID Unity generates per
        /// build. A change in either (a version you bump, or simply a new build of the player)
        /// yields a new signature, which re-arms the one-time onboarding for that build.
        /// </summary>
        private static string CurrentSignature => Application.version + "|" + Application.buildGUID;

        /// <summary>
        /// True only if onboarding was completed on THIS build. A new build (or a bumped version)
        /// reports false until completed again, so the first New Run routes into onboarding.
        /// </summary>
        public static bool IsComplete =>
            PlayerPrefs.GetString(StampKey, string.Empty) == CurrentSignature;

        /// <summary>Stamp the onboarding as seen for this build so New Run stops routing into it.</summary>
        public static void MarkComplete()
        {
            PlayerPrefs.SetString(StampKey, CurrentSignature);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Clear the stamp so the next New Run forces the onboarding again. For testing the
        /// first-time flow without wiping every other PlayerPrefs value.
        /// </summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(StampKey);
            PlayerPrefs.DeleteKey(LegacyCompleteKey);
            PlayerPrefs.Save();
        }
    }
}
