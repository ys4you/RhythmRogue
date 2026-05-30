using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace RhythmRogue.UI.Navigation
{
    /// <summary>
    /// Shows a focus frame GameObject when its Selectable gains focus (keyboard/gamepad
    /// select, or mouse hover) and hides it when focus leaves.
    ///
    /// Why this exists: Unity's built-in ColorTint transition multiplies state colors
    /// against the Selectable's targetGraphic. When the targetGraphic is a content panel
    /// (like a relic card background), the selected/highlighted tint recolors the content
    /// itself, so a focused card looks like a different color than its unfocused siblings.
    /// That reads as a rendering bug even though every card has identical color data.
    ///
    /// This component sidesteps ColorTint entirely: the card background is never tinted,
    /// and focus is shown by toggling a dedicated frame object on top. Content colors stay
    /// true in every state; only the frame appears/disappears.
    ///
    /// Pair this with Selectable.transition = None so Unity does no color multiplication.
    ///
    /// Usage:
    ///   var focus = cardGO.AddComponent&lt;UIFocusFrame&gt;();
    ///   focus.SetFrame(frameGO);          // the highlight object to toggle
    ///   button.transition = Selectable.Transition.None;
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class UIFocusFrame : MonoBehaviour,
        ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private GameObject _frame;
        private Selectable _selectable;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
            if (_frame != null) _frame.SetActive(false);
        }

        /// <summary>Assign the frame object to show on focus. Hidden immediately.</summary>
        public void SetFrame(GameObject frame)
        {
            _frame = frame;
            if (_frame != null) _frame.SetActive(false);
        }

        public void OnSelect(BaseEventData eventData) => Show();
        public void OnDeselect(BaseEventData eventData) => Hide();

        // Pointer enter/exit mirror the keyboard focus so mouse hover also shows the frame.
        // Note: pointer hover does not change EventSystem selection, so we toggle directly.
        public void OnPointerEnter(PointerEventData eventData) => Show();

        public void OnPointerExit(PointerEventData eventData)
        {
            // Only hide on pointer-exit if this element isn't the actively selected one,
            // so moving the mouse away from a keyboard-focused card keeps its frame.
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
                return;
            Hide();
        }

        private void Show()
        {
            if (_frame != null) _frame.SetActive(true);
        }

        private void Hide()
        {
            if (_frame != null) _frame.SetActive(false);
        }
    }
}
