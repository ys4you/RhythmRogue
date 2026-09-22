using UnityEngine;
using RhythmRogue.Data;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Drives a lane backdrop sprite from the per-side <see cref="BackdropSettings"/>: shows or hides
    /// it and tints it (colour plus alpha). Put this on the backdrop child of a highway and pick
    /// which side it is. It reads the setting when it enables and refreshes live when the setting
    /// changes, so a change in the pause-menu settings updates the current fight at once.
    ///
    /// Note: the backdrop's sprite should be WHITE. A SpriteRenderer tints by multiplying, so a
    /// white sprite takes the chosen colour and alpha exactly, while a black sprite stays black.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class HighwayBackdrop : MonoBehaviour
    {
        [Tooltip("Which side's settings drive this backdrop.")]
        [SerializeField] private BackdropSide _side = BackdropSide.Player;

        private SpriteRenderer _renderer;

        private void Awake() => _renderer = GetComponent<SpriteRenderer>();

        private void OnEnable()
        {
            Apply();
            BackdropSettings.OnChanged += Apply;
        }

        private void OnDisable() => BackdropSettings.OnChanged -= Apply;

        // Toggle the renderer (not the GameObject) so this component keeps listening while the
        // backdrop is hidden, and can switch it back on when the setting changes.
        private void Apply()
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            _renderer.enabled = BackdropSettings.GetEnabled(_side);
            _renderer.color = BackdropSettings.GetColor(_side);
        }
    }
}
