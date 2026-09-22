using System;
using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>Which highway a backdrop belongs to. Drives which settings slot it reads.</summary>
    public enum BackdropSide { Player, Enemy }

    /// <summary>
    /// Per-side lane backdrop settings: whether the backdrop shows, and its colour (including
    /// alpha/transparency). Persisted in PlayerPrefs, one slot for the player side and one for the
    /// enemy side. The <see cref="HighwayBackdrop"/> components read these and apply them, and
    /// refresh live through <see cref="OnChanged"/>, so changing a value in the pause-menu settings
    /// updates the current battle immediately.
    ///
    /// Colour is stored as an "RRGGBBAA" hex string so a single key holds the full colour and alpha.
    /// </summary>
    public static class BackdropSettings
    {
        // Default: on, a dark near-black tone at ~62% alpha (a calm track that reads over the scene
        // without being a hard black box). Both sides share the same default.
        private static readonly Color DefaultColor = new Color(0.082f, 0.047f, 0.129f, 0.62f);

        /// <summary>Raised whenever any backdrop setting changes, so live views can refresh.</summary>
        public static event Action OnChanged;

        public static bool GetEnabled(BackdropSide side) => PlayerPrefs.GetInt(EnabledKey(side), 1) != 0;

        public static void SetEnabled(BackdropSide side, bool on)
        {
            PlayerPrefs.SetInt(EnabledKey(side), on ? 1 : 0);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        public static Color GetColor(BackdropSide side)
        {
            string s = PlayerPrefs.GetString(ColorKey(side), string.Empty);
            if (!string.IsNullOrEmpty(s) && ColorUtility.TryParseHtmlString("#" + s, out Color c)) return c;
            return DefaultColor;
        }

        public static void SetColor(BackdropSide side, Color c)
        {
            PlayerPrefs.SetString(ColorKey(side), ColorUtility.ToHtmlStringRGBA(c));
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        private static string EnabledKey(BackdropSide side) =>
            side == BackdropSide.Player ? "RhythmRogue_Backdrop_Player_On" : "RhythmRogue_Backdrop_Enemy_On";

        private static string ColorKey(BackdropSide side) =>
            side == BackdropSide.Player ? "RhythmRogue_Backdrop_Player_Color" : "RhythmRogue_Backdrop_Enemy_Color";
    }
}
