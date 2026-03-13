using UnityEngine;

namespace RhythmRogue.Util.Extensions
{
    /// <summary>
    /// Extension methods for Color and Color32.
    /// Used constantly for UI fades, hit feedback coloring,
    /// and lane color manipulation.
    /// </summary>
    public static class ColorExtensions
    {
        /// <summary>
        /// Return a copy with a different alpha.
        /// 
        ///   // Fade a sprite to half opacity
        ///   renderer.color = renderer.color.WithAlpha(0.5f);
        /// 
        ///   // Fade out completely
        ///   image.color = image.color.WithAlpha(0f);
        /// </summary>
        public static Color WithAlpha(this Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        /// <summary>
        /// Return a copy with modified RGB, preserving alpha.
        /// 
        ///   // Flash white on Perfect hit, keep current alpha
        ///   renderer.color = renderer.color.WithRGB(1f, 1f, 1f);
        /// </summary>
        public static Color WithRGB(this Color color, float r, float g, float b)
        {
            return new Color(r, g, b, color.a);
        }

        /// <summary>
        /// Brighten or darken a color by a factor.
        /// Factor > 1 brightens, < 1 darkens. Alpha is preserved.
        /// 
        ///   // Brighten on hover
        ///   button.color = baseColor.Brighten(1.2f);
        /// 
        ///   // Darken for disabled state
        ///   button.color = baseColor.Brighten(0.5f);
        /// </summary>
        public static Color Brighten(this Color color, float factor)
        {
            return new Color(
                Mathf.Clamp01(color.r * factor),
                Mathf.Clamp01(color.g * factor),
                Mathf.Clamp01(color.b * factor),
                color.a
            );
        }

        /// <summary>
        /// Lerp only the alpha channel between current and target.
        /// 
        ///   // Smooth fade during timer progress
        ///   renderer.color = renderer.color.LerpAlpha(0f, progress);
        /// </summary>
        public static Color LerpAlpha(this Color color, float targetAlpha, float t)
        {
            return new Color(color.r, color.g, color.b, Mathf.Lerp(color.a, targetAlpha, t));
        }
    }
}
