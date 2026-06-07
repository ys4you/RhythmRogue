using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Global note scroll direction. Persisted in PlayerPrefs.
    ///
    /// Downscroll (default): receptors sit at the bottom, notes fall down to meet them.
    /// Upscroll: receptors sit at the top, notes rise up to meet them.
    ///
    /// The highways read this and mirror the playfield accordingly. Changing it takes effect
    /// immediately, including mid-battle, because the highways re-check it each frame.
    /// </summary>
    public static class ScrollDirectionSetting
    {
        private const string PrefsKey = "RhythmRogue_Downscroll";
        private static int _cached = -1;

        /// <summary>True = downscroll (default), false = upscroll.</summary>
        public static bool Downscroll
        {
            get { if (_cached < 0) _cached = PlayerPrefs.GetInt(PrefsKey, 1); return _cached != 0; }
            set { _cached = value ? 1 : 0; PlayerPrefs.SetInt(PrefsKey, _cached); PlayerPrefs.Save(); }
        }

        public static void Toggle() => Downscroll = !Downscroll;
        public static string DisplayString => Downscroll ? "Down" : "Up";
    }
}
