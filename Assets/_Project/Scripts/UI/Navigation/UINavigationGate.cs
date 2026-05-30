using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace RhythmRogue.UI.Navigation
{
    /// <summary>
    /// Globally gates keyboard/gamepad UI navigation so it is OFF until the player
    /// actually presses a navigation key, and turns back OFF the moment they use the mouse.
    ///
    /// Why this exists: many players use the mouse only. Unity's UI, with the Input System
    /// UI module, keeps a "selected" object and will auto-select / show a focus highlight
    /// as soon as a Navigate event fires (or even on load). That highlight jumps around and
    /// fights the mouse. Per-screen attempts to suppress it are fragile because every screen
    /// has to opt in and the module re-asserts selection on its own.
    ///
    /// This component sits on the EventSystem (one instance, global) and controls the
    /// InputSystemUIInputModule's "move" (Navigate) action directly:
    ///   - Start: navigation action DISABLED, selection cleared. Mouse hover/click work
    ///     normally; no keyboard highlight appears anywhere.
    ///   - First navigation key press (arrows / WASD / Tab): enable the navigation action
    ///     and select a starting element so arrow keys begin working.
    ///   - Any mouse movement or button press: disable the navigation action again and clear
    ///     the selection, so the highlight disappears and hover is clean.
    ///
    /// Because it operates on the shared module, it covers every screen at once - menu,
    /// map, reward, rest, summary, pause - with no per-screen code.
    ///
    /// Detection reads the Input System devices directly (Keyboard.current / Mouse.current)
    /// so it works even when legacy Input is disabled ("Input System Only"). A legacy
    /// fallback is compiled in when the new Input System is not enabled.
    /// </summary>
    [DisallowMultipleComponent]
    public class UINavigationGate : MonoBehaviour
    {
        // Mouse movement (pixels, squared) in a frame that counts as "using the mouse".
        private const float MouseMoveThresholdSqr = 1.0f;

        private bool _navEnabled;
        private Vector3 _lastMousePosition;

#if ENABLE_INPUT_SYSTEM
        private InputSystemUIInputModule _module;
        private InputAction _moveAction;
#endif

        /// <summary>
        /// Finds or creates the global gate on the current EventSystem. Safe to call
        /// repeatedly; only one is ever added. Call after EnsureEventSystem().
        /// </summary>
        public static void Ensure()
        {
            var es = EventSystem.current;
            if (es == null) return;
            if (es.GetComponent<UINavigationGate>() == null)
                es.gameObject.AddComponent<UINavigationGate>();
        }

        private void Awake()
        {
#if ENABLE_INPUT_SYSTEM
            _module = GetComponent<InputSystemUIInputModule>();
            ResolveMoveAction();
#endif
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>
        /// Cache the module's Navigate ("move") action. The module assigns its actions in
        /// its own OnEnable, which may run after our Awake depending on component order, so
        /// this is also retried lazily from Update until resolved.
        /// </summary>
        private void ResolveMoveAction()
        {
            if (_moveAction != null) return;
            if (_module == null) _module = GetComponent<InputSystemUIInputModule>();
            if (_module != null && _module.move != null)
                _moveAction = _module.move.action;
        }
#endif

        private void OnEnable()
        {
            _lastMousePosition = CurrentMousePosition();
            DisableNavigation();
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            // Retry resolving the move action until the module has assigned it.
            if (_moveAction == null) ResolveMoveAction();
#endif

            if (MouseUsedThisFrame())
            {
                if (_navEnabled) DisableNavigation();
                return;
            }

            if (!_navEnabled && NavigationKeyPressed())
                EnableNavigation();
        }

        // === Navigation enable/disable ===

        private void EnableNavigation()
        {
            _navEnabled = true;
#if ENABLE_INPUT_SYSTEM
            _moveAction?.Enable();
#endif
            // Select a starting element so the very first key press has somewhere to land.
            SelectStartingElement();
        }

        private void DisableNavigation()
        {
            _navEnabled = false;
#if ENABLE_INPUT_SYSTEM
            _moveAction?.Disable();
#endif
            // Clear any keyboard highlight so mouse hover is clean.
            var es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(null);
        }

        /// <summary>
        /// When navigation switches on, pick a sensible element to start from. Prefer the
        /// element a UIFocusSetter on the active screen designates as default; otherwise
        /// fall back to the EventSystem's configured firstSelectedGameObject.
        /// </summary>
        private void SelectStartingElement()
        {
            var es = EventSystem.current;
            if (es == null) return;

            // If something valid is already selected, keep it.
            GameObject current = es.currentSelectedGameObject;
            if (current != null && current.activeInHierarchy)
            {
                var sel = current.GetComponent<Selectable>();
                if (sel != null && sel.IsInteractable()) return;
            }

            // Otherwise ask any active UIFocusSetter for its default.
            var setter = FindActiveFocusSetter();
            GameObject target = setter != null ? setter.DefaultSelected : null;

            if (target == null && es.firstSelectedGameObject != null)
                target = es.firstSelectedGameObject;

            if (target != null)
            {
                var sel = target.GetComponent<Selectable>();
                if (sel != null && sel.IsInteractable() && target.activeInHierarchy)
                {
                    es.SetSelectedGameObject(null);
                    es.SetSelectedGameObject(target);
                }
            }
        }

        private static UIFocusSetter FindActiveFocusSetter()
        {
            // Cheap: screens have at most one focus setter active at a time. Using the
            // non-deprecated FindObjectsByType with no sorting for minimal overhead.
#if UNITY_2023_1_OR_NEWER
            var setters = Object.FindObjectsByType<UIFocusSetter>(FindObjectsSortMode.None);
#else
            var setters = Object.FindObjectsOfType<UIFocusSetter>();
#endif
            foreach (var s in setters)
                if (s != null && s.isActiveAndEnabled && s.DefaultSelected != null)
                    return s;
            return null;
        }

        // === Input detection (Input System with legacy fallback) ===

        private bool MouseUsedThisFrame()
        {
            Vector3 pos = CurrentMousePosition();
            bool moved = (pos - _lastMousePosition).sqrMagnitude > MouseMoveThresholdSqr;
            _lastMousePosition = pos;

            bool clicked = false;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
                clicked = mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame;
#else
            clicked = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
#endif
            return moved || clicked;
        }

        private static Vector3 CurrentMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 p = mouse.position.ReadValue();
                return new Vector3(p.x, p.y, 0f);
            }
            return Vector3.zero;
#else
            return Input.mousePosition;
#endif
        }

        private static bool NavigationKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame ||
                    kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame ||
                    kb.wKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame ||
                    kb.sKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame ||
                    kb.tabKey.wasPressedThisFrame)
                    return true;
            }
            var gp = Gamepad.current;
            if (gp != null)
            {
                if (gp.dpad.up.wasPressedThisFrame || gp.dpad.down.wasPressedThisFrame ||
                    gp.dpad.left.wasPressedThisFrame || gp.dpad.right.wasPressedThisFrame)
                    return true;
                // Stick flick past a deadzone also counts.
                Vector2 ls = gp.leftStick.ReadValue();
                if (ls.sqrMagnitude > 0.5f * 0.5f) return true;
            }
            return false;
#else
            return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                   Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) ||
                   Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) ||
                   Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D) ||
                   Input.GetKeyDown(KeyCode.Tab);
#endif
        }
    }
}
