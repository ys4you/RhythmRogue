using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmRogue.UI.Navigation
{
    /// <summary>
    /// Sets a default focused UI element when this component enables.
    /// Attach to each screen's root Canvas or controller GameObject.
    ///
    /// Also monitors for lost focus each frame — if the EventSystem
    /// has no selected object (e.g. after a button was disabled or
    /// destroyed), it re-selects the default.
    ///
    /// Usage:
    ///   var setter = screenRoot.AddComponent&lt;UIFocusSetter&gt;();
    ///   setter.SetDefault(newRunButton.gameObject);
    /// </summary>
    [DisallowMultipleComponent]
    public class UIFocusSetter : MonoBehaviour
    {
        [Tooltip("The element to select when this screen appears.")]
        [SerializeField] private GameObject _defaultSelected;

        private EventSystem _eventSystem;

        /// <summary>
        /// Set the default element at runtime (for code-generated UI).
        /// </summary>
        public void SetDefault(GameObject selectable)
        {
            _defaultSelected = selectable;
        }

        private void OnEnable()
        {
            // Defer one frame so all UI has finished layout
            StartCoroutine(SelectNextFrame());
        }

        private System.Collections.IEnumerator SelectNextFrame()
        {
            yield return null;
            ApplyFocus();
        }

        private void LateUpdate()
        {
            // Guard: recover focus if it's lost
            if (_eventSystem == null)
                _eventSystem = EventSystem.current;

            if (_eventSystem == null || _defaultSelected == null)
                return;

            GameObject current = _eventSystem.currentSelectedGameObject;

            if (current == null || !current.activeInHierarchy)
            {
                ApplyFocus();
            }
        }

        /// <summary>
        /// Force focus to the default element. Safe to call anytime.
        /// </summary>
        public void ApplyFocus()
        {
            if (_defaultSelected == null) return;

            if (_eventSystem == null)
                _eventSystem = EventSystem.current;

            if (_eventSystem == null) return;

            // Only select if the target is active and interactable
            Selectable sel = _defaultSelected.GetComponent<Selectable>();
            if (sel != null && sel.IsInteractable() && sel.gameObject.activeInHierarchy)
            {
                _eventSystem.SetSelectedGameObject(null);
                _eventSystem.SetSelectedGameObject(_defaultSelected);
            }
        }

        /// <summary>
        /// Change the default and immediately apply focus.
        /// Used after state changes (e.g. healing completes → focus Continue).
        /// </summary>
        public void FocusOn(GameObject target)
        {
            _defaultSelected = target;
            ApplyFocus();
        }
    }
}
