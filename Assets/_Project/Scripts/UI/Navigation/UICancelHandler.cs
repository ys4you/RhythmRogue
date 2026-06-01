using System;
using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Core.Audio;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RhythmRogue.UI.Navigation
{
    /// <summary>
    /// Routes the Cancel action (Escape / Gamepad B) to registered callbacks.
    ///
    /// Supports a stack of handlers for nested UI panels:
    ///   1. Open settings → push settings close handler
    ///   2. Press Escape → pops and invokes settings close
    ///   3. Press Escape again → invokes the base handler (e.g. quit confirm)
    ///
    /// Uses Unity's Input System Cancel action via the EventSystem.
    /// Falls back to legacy Input.GetKeyDown(Escape) for compatibility.
    ///
    /// Attach to the same GameObject as UIFocusSetter on each screen.
    ///
    /// Usage:
    ///   var cancel = screenRoot.AddComponent&lt;UICancelHandler&gt;();
    ///   cancel.SetBaseAction(OnQuitRequested);
    ///   // When opening a sub-panel:
    ///   cancel.Push(OnCloseSettingsPanel);
    /// </summary>
    [DisallowMultipleComponent]
    public class UICancelHandler : MonoBehaviour
    {
        private Action _baseAction;
        private readonly Stack<Action> _stack = new();

        // Frame index up to which Cancel input is ignored. Used to prevent the same key
        // press that OPENED a menu from being consumed again by this handler in the same
        // (or next) frame, which would immediately cancel the menu. Set via SuppressForOneFrame.
        private int _suppressUntilFrame = -1;

        /// <summary>
        /// Ignore Cancel input for the current and next frame. Call this right after showing
        /// a panel whose open was triggered by the same Cancel/Escape key, so that key press
        /// doesn't immediately bubble into a cancel here.
        /// </summary>
        public void SuppressForOneFrame()
        {
            _suppressUntilFrame = Time.frameCount + 1;
        }

        /// <summary>
        /// Set the root cancel behavior (e.g. quit or go to main menu).
        /// This is always the last handler after all pushed handlers are popped.
        /// </summary>
        public void SetBaseAction(Action action)
        {
            _baseAction = action;
        }

        /// <summary>
        /// Push a cancel handler for a sub-panel.
        /// It will be invoked (and removed) on the next Cancel press.
        /// </summary>
        public void Push(Action onCancel)
        {
            if (onCancel != null)
                _stack.Push(onCancel);
        }

        /// <summary>
        /// Remove the top handler without invoking it.
        /// Call when a sub-panel is closed by other means (e.g. confirm button).
        /// </summary>
        public void Pop()
        {
            if (_stack.Count > 0)
                _stack.Pop();
        }

        /// <summary>
        /// Clear all pushed handlers. Call on screen teardown.
        /// </summary>
        public void ClearStack()
        {
            _stack.Clear();
        }

        private void Update()
        {
            // Skip while suppressed (e.g. the frame the menu opened from the same key press).
            if (Time.frameCount <= _suppressUntilFrame) return;

            // Check for Cancel input - Escape key or Gamepad B/Circle.
            // Read through the Input System because this project runs "Input System Only"
            // (legacy UnityEngine.Input is disabled, so Input.GetKeyDown would never fire).
            if (CancelPressedThisFrame())
            {
                HandleCancel();
            }
        }

        private static bool CancelPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            if (gp != null && gp.buttonEast.wasPressedThisFrame) return true; // B / Circle
            return false;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private void HandleCancel()
        {
            var mgr = AudioManager.Instance;
            if (mgr != null) mgr.Play(SfxId.UiBack);

            if (_stack.Count > 0)
            {
                Action top = _stack.Pop();
                top?.Invoke();
            }
            else
            {
                _baseAction?.Invoke();
            }
        }
    }
}
