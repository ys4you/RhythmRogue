using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Global note scroll speed, expressed as a CONSTANT velocity in world units per second.
    /// Persisted in PlayerPrefs. Range 2 to 40 u/s.
    ///
    /// This is BPM-independent on purpose. The highways convert it into a per-beat height using
    /// the song's current BPM (units/beat = UnitsPerSecond * 60 / BPM), so a note travels at the
    /// same on-screen speed no matter how fast or slow the song is. A higher value scrolls faster.
    /// </summary>
    public static class ScrollSpeedSetting
    {
        // New key on purpose: the old "RhythmRogue_ScrollSpeed" stored a BPM-relative multiplier
        // (0.5-3.0), which would load as a nonsensical velocity here. A fresh key resets cleanly.
        private const string PrefsKey = "RhythmRogue_ScrollUnitsPerSecond";
        private const float DefaultSpeed = 5f;
        private const float MinSpeed = 2f;
        private const float MaxSpeed = 40f;
        private const float StepSize = 0.5f;

        private static float _cached = -1f;

        /// <summary>Note travel speed in world units per second. Constant regardless of song BPM.</summary>
        public static float UnitsPerSecond
        {
            get { if (_cached < 0f) _cached = PlayerPrefs.GetFloat(PrefsKey, DefaultSpeed); return _cached; }
            set { _cached = Mathf.Clamp(value, MinSpeed, MaxSpeed); PlayerPrefs.SetFloat(PrefsKey, _cached); PlayerPrefs.Save(); }
        }

        public static float Min => MinSpeed;
        public static float Max => MaxSpeed;

        public static void Increase() => UnitsPerSecond += StepSize;
        public static void Decrease() => UnitsPerSecond -= StepSize;
        public static void Reset() => UnitsPerSecond = DefaultSpeed;
        public static string DisplayString => $"{UnitsPerSecond:0.0} u/s";
    }
}
