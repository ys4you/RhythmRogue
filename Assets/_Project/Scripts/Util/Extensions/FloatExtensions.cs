using UnityEngine;

namespace RhythmRogue.Util.Extensions
{
    /// <summary>
    /// Extension methods for float values.
    /// Focused on mapping, clamping, and comparison operations
    /// that come up constantly in rhythm gameplay math.
    /// </summary>
    public static class FloatExtensions
    {
        /// <summary>
        /// Remap a value from one range to another.
        /// 
        ///   // Beat position → screen Y
        ///   float screenY = beatPos.Remap(currentBeat, currentBeat + visible, 0f, highwayHeight);
        /// 
        ///   // HP → fill amount
        ///   float fill = currentHp.Remap(0f, maxHp, 0f, 1f);
        /// </summary>
        public static float Remap(this float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            float t = (value - fromMin) / (fromMax - fromMin);
            return Mathf.LerpUnclamped(toMin, toMax, t);
        }

        /// <summary>
        /// Remap a value from one range to another, clamped to the target range.
        /// 
        ///   // Progress bar that never exceeds 1 or goes below 0
        ///   float fill = elapsed.RemapClamped(0f, duration, 0f, 1f);
        /// </summary>
        public static float RemapClamped(this float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            float t = Mathf.Clamp01((value - fromMin) / (fromMax - fromMin));
            return Mathf.Lerp(toMin, toMax, t);
        }

        /// <summary>
        /// Check if two floats are approximately equal.
        /// Wraps Mathf.Approximately for readability.
        /// 
        ///   if (beatPosition.Approximately(targetBeat))
        ///       TriggerNote();
        /// </summary>
        public static bool Approximately(this float value, float other)
        {
            return Mathf.Approximately(value, other);
        }

        /// <summary>
        /// Check if a float is within a tolerance of a target.
        /// Useful for hit detection windows.
        /// 
        ///   // Is the input within ±35ms of the beat?
        ///   if (offsetMs.Within(0f, 35f))
        ///       judgment = Perfect;
        /// </summary>
        public static bool Within(this float value, float target, float tolerance)
        {
            return Mathf.Abs(value - target) <= tolerance;
        }

        /// <summary>
        /// Clamp between 0 and 1. Shorthand for Mathf.Clamp01.
        /// 
        ///   float progress = (elapsed / duration).Clamp01();
        /// </summary>
        public static float Clamp01(this float value)
        {
            return Mathf.Clamp01(value);
        }

        /// <summary>
        /// Clamp between min and max.
        /// 
        ///   float bpm = requestedBpm.Clamp(80f, 200f);
        /// </summary>
        public static float Clamp(this float value, float min, float max)
        {
            return Mathf.Clamp(value, min, max);
        }

        /// <summary>
        /// Snap a float to the nearest multiple of a step value.
        /// Useful for quantizing to beat subdivisions.
        /// 
        ///   // Snap to nearest 8th note (0.5 beats)
        ///   float snapped = beatPos.SnapTo(0.5f);
        /// </summary>
        public static float SnapTo(this float value, float step)
        {
            if (step <= 0f) return value;
            return Mathf.Round(value / step) * step;
        }
    }
}
