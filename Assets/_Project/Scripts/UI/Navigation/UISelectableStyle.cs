using UnityEngine;
using UnityEngine.UI;

namespace RhythmRogue.UI.Navigation
{
    /// <summary>
    /// Applies a consistent visual focus style to a Selectable.
    ///
    /// Call ApplyStyle() after creating any interactive UI element
    /// to ensure keyboard/gamepad focus is always clearly visible.
    ///
    /// Color scheme (matches pixel-art aesthetic):
    ///   Normal:      element's original color
    ///   Highlighted: slightly brighter (mouse hover / initial focus)
    ///   Selected:    bright accent border (keyboard/gamepad active focus)
    ///   Pressed:     dimmed flash
    ///   Disabled:    desaturated, low alpha
    ///
    /// The Selected state is the critical one — it must be visually
    /// distinct from Highlighted so players always know which element
    /// has keyboard focus.
    /// </summary>
    public static class UISelectableStyle
    {
        // Accent color for selected state — warm gold, matches the game's palette
        private static readonly Color SelectedTint = new Color(1f, 0.85f, 0.3f);
        private static readonly Color DisabledTint = new Color(0.35f, 0.35f, 0.35f, 0.6f);

        /// <summary>
        /// Apply navigation-friendly color transitions to a Selectable.
        /// Call after creating the element and setting its base color.
        ///
        ///   Button btn = MakeButton(...);
        ///   UISelectableStyle.Apply(btn);
        /// </summary>
        public static void Apply(Selectable selectable, float fadeDuration = 0.1f)
        {
            if (selectable == null) return;

            ColorBlock colors = selectable.colors;

            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.selectedColor = SelectedTint;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.disabledColor = DisabledTint;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = fadeDuration;

            selectable.colors = colors;

            // Ensure transition uses ColorTint (not Animation or None)
            selectable.transition = Selectable.Transition.ColorTint;
        }

        /// <summary>
        /// Apply style with a custom selected color.
        /// Use for special elements (e.g. map nodes use their type color).
        /// </summary>
        public static void Apply(Selectable selectable, Color selectedColor, float fadeDuration = 0.1f)
        {
            if (selectable == null) return;

            ColorBlock colors = selectable.colors;

            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.selectedColor = selectedColor;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.disabledColor = DisabledTint;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = fadeDuration;

            selectable.colors = colors;
            selectable.transition = Selectable.Transition.ColorTint;
        }

        /// <summary>
        /// Apply style optimized for sliders. Selected tints the handle.
        /// </summary>
        public static void ApplySlider(Slider slider, float fadeDuration = 0.1f)
        {
            if (slider == null) return;

            // Style the slider's target graphic (handle)
            Apply(slider, fadeDuration);

            // Also make the handle brighter when selected
            if (slider.handleRect != null)
            {
                Image handle = slider.handleRect.GetComponent<Image>();
                if (handle != null)
                    slider.targetGraphic = handle;
            }
        }
    }
}
