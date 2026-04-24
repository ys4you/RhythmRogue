using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Global scroll speed preference. Persisted in PlayerPrefs.
    /// 
    /// Multiplies the highway's base beatHeight:
    ///   1.0 = default speed
    ///   1.5 = 50% faster (notes more spread out)
    ///   0.7 = 30% slower (notes more compact)
    /// 
    /// Both player and enemy highways read from this.
    /// Adjustable from settings menu, pause menu, or debug keys.
    /// 
    /// Range: 0.5 to 3.0 (covers slow readers to expert players).
    /// </summary>
    public static class ScrollSpeedSetting
    {
        private const string PrefsKey = "RhythmRogue_ScrollSpeed";
        private const float DefaultSpeed = 1.0f;
        private const float MinSpeed = 0.5f;
        private const float MaxSpeed = 6.0f;
        private const float StepSize = 0.1f;

        private static float _cached = -1f;

        /// <summary>
        /// Current scroll speed multiplier. Cached after first read.
        /// </summary>
        public static float Multiplier
        {
            get
            {
                if (_cached < 0f)
                    _cached = PlayerPrefs.GetFloat(PrefsKey, DefaultSpeed);
                return _cached;
            }
            set
            {
                _cached = Mathf.Clamp(value, MinSpeed, MaxSpeed);
                PlayerPrefs.SetFloat(PrefsKey, _cached);
            }
        }

        /// <summary>Increase by one step (0.1).</summary>
        public static void Increase()
        {
            Multiplier += StepSize;
        }

        /// <summary>Decrease by one step (0.1).</summary>
        public static void Decrease()
        {
            Multiplier -= StepSize;
        }

        /// <summary>Reset to default.</summary>
        public static void Reset()
        {
            Multiplier = DefaultSpeed;
        }

        /// <summary>Formatted string for UI display (e.g. "1.5x").</summary>
        public static string DisplayString => $"{Multiplier:F1}x";
    }
}