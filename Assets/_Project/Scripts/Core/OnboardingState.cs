using UnityEngine;

namespace RhythmRogue.Core
{
    /// <summary>
    /// Tiny persistent flag for whether the player has been shown the onboarding. Lives in
    /// PlayerPrefs so it survives quitting and stays independent of the run/save system.
    ///
    /// The main menu reads this on boot to route a brand-new player into the onboarding once. The
    /// flag is set the moment that routing happens (not on completion), so a player who leaves the
    /// tutorial early is not forced back in: it forces once, then it is their choice via How to
    /// Play. When a real meta-progression save exists, fold this into it so it travels with the
    /// rest of the profile.
    /// </summary>
    public static class OnboardingState
    {
        private const string CompleteKey = "rr_onboarding_complete";

        /// <summary>True once the first-launch onboarding has been triggered (or the player has
        /// otherwise been routed through it). The menu uses this to decide whether to auto-route.</summary>
        public static bool IsComplete => PlayerPrefs.GetInt(CompleteKey, 0) == 1;

        /// <summary>Mark the onboarding as seen so the menu stops auto-routing into it.</summary>
        public static void MarkComplete()
        {
            PlayerPrefs.SetInt(CompleteKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Clear the flag so the next launch forces the onboarding again. For testing the
        /// first-launch flow without wiping every other PlayerPrefs value.</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(CompleteKey);
            PlayerPrefs.Save();
        }
    }
}
