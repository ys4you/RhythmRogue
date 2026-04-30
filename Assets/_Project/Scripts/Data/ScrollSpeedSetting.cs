using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Global scroll speed multiplier. Persisted in PlayerPrefs. Range 0.5 to 6.0.
    /// </summary>
    public static class ScrollSpeedSetting
    {
        private const string PrefsKey = "RhythmRogue_ScrollSpeed";
        private const float DefaultSpeed = 1.0f;
        private const float MinSpeed = 0.5f;
        private const float MaxSpeed = 6.0f;
        private const float StepSize = 0.1f;

        private static float _cached = -1f;

        public static float Multiplier
        {
            get { if (_cached < 0f) _cached = PlayerPrefs.GetFloat(PrefsKey, DefaultSpeed); return _cached; }
            set { _cached = Mathf.Clamp(value, MinSpeed, MaxSpeed); PlayerPrefs.SetFloat(PrefsKey, _cached); }
        }

        public static void Increase() => Multiplier += StepSize;
        public static void Decrease() => Multiplier -= StepSize;
        public static void Reset() => Multiplier = DefaultSpeed;
        public static string DisplayString => $"{Multiplier:F1}x";
    }
}
