using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmRogue.UI.Navigation
{
    /// <summary>
    /// Holds the "default" element a screen wants keyboard navigation to start from.
    ///
    /// This component no longer forces selection on its own. Global navigation gating
    /// (mouse vs keyboard) is handled once, on the EventSystem, by UINavigationGate.
    /// That gate keeps arrow-key navigation OFF until the player presses a nav key, then
    /// asks the active UIFocusSetter (via DefaultSelected) where to place the starting
    /// focus. The moment the mouse is used again, the gate clears selection.
    ///
    /// So this class is intentionally thin:
    ///   - SetDefault: record which element keyboard navigation should start from.
    ///   - FocusOn: a deliberate hand-off (e.g. a panel opened, an action completed) that
    ///     should move keyboard focus right now IF the player is currently navigating by
    ///     keyboard. In mouse mode it only updates the default, so no highlight pops up
    ///     mid-mouse-use.
    ///   - DefaultSelected: read by UINavigationGate when navigation switches on.
    ///
    /// Usage:
    ///   var setter = screenRoot.AddComponent&lt;UIFocusSetter&gt;();
    ///   setter.SetDefault(newRunButton.gameObject);
    /// </summary>
    [DisallowMultipleComponent]
    public class UIFocusSetter : MonoBehaviour
    {
        [Tooltip("The element keyboard navigation starts from when the player first presses a nav key.")]
        [SerializeField] private GameObject _defaultSelected;

        /// <summary>The element keyboard navigation should start from. Read by UINavigationGate.</summary>
        public GameObject DefaultSelected => _defaultSelected;

        /// <summary>
        /// Record the element keyboard navigation starts from. Does NOT select it now -
        /// selection waits until the player actually uses the keyboard (handled by the gate).
        /// </summary>
        public void SetDefault(GameObject selectable)
        {
            _defaultSelected = selectable;
        }

        /// <summary>
        /// Deliberate focus hand-off. Updates the default, and if the player is already
        /// navigating by keyboard (something is currently selected), moves focus to the
        /// target immediately. In mouse mode it just records the default so no keyboard
        /// highlight appears mid-mouse-use; when the player next presses a nav key, the
        /// gate will start from this target.
        /// </summary>
        public void FocusOn(GameObject target)
        {
            _defaultSelected = target;
            if (target == null) return;

            var es = EventSystem.current;
            if (es == null) return;

            // Only move selection now if keyboard navigation is already active. We treat
            // "something is currently selected" as the signal that the player is in keyboard
            // mode; in mouse mode the gate keeps selection cleared, so this won't fire.
            if (es.currentSelectedGameObject == null) return;

            var sel = target.GetComponent<Selectable>();
            if (sel != null && sel.IsInteractable() && target.activeInHierarchy)
            {
                es.SetSelectedGameObject(null);
                es.SetSelectedGameObject(target);
            }
        }

        /// <summary>
        /// Back-compat no-op-ish helper. Focus is now driven by the global gate, so this
        /// only nudges focus if keyboard navigation is already active. Kept so existing
        /// callers (e.g. SummaryScreen) compile and behave sensibly.
        /// </summary>
        public void ApplyFocus()
        {
            if (_defaultSelected != null) FocusOn(_defaultSelected);
        }
    }
}
