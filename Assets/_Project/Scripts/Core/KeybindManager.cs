using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmRogue.Core
{
    /// <summary>
    /// Manages runtime keybinding overrides for the RhythmActions input asset.
    /// 
    /// Uses Unity Input System's binding override system — the .inputactions
    /// file is never modified. Overrides are stored as JSON in PlayerPrefs
    /// and applied on startup before any input is processed.
    /// 
    /// Supports:
    ///   - Loading/saving overrides to PlayerPrefs
    ///   - Interactive rebinding (one action at a time)
    ///   - Conflict detection (prevents same key on two lanes)
    ///   - Reset to defaults
    ///   - Keyboard-only rebinding (gamepad bindings stay fixed)
    /// 
    /// Usage:
    ///   KeybindManager.Initialize(rhythmActionsAsset);
    ///   KeybindManager.StartRebind(lane, bindingIndex, onComplete, onCancel);
    /// </summary>
    public static class KeybindManager
    {
        private const string PrefsKey = "rhythmBindingOverrides";

        private static InputActionAsset _asset;
        private static InputActionMap _rhythmMap;
        private static InputAction[] _laneActions;
        private static bool _initialized;

        /// <summary>Lane display names for UI.</summary>
        public static readonly string[] LaneNames = { "Left", "Down", "Up", "Right" };

        /// <summary>Number of rebindable lanes.</summary>
        public const int LaneCount = 4;

        // =================================================================
        // INITIALIZATION
        // =================================================================

        /// <summary>
        /// Initialize with the RhythmActions asset. Call once at startup
        /// (e.g. from InputHandler.Awake or a bootstrap script).
        /// Loads saved overrides from PlayerPrefs.
        /// </summary>
        public static void Initialize(InputActionAsset asset)
        {
            if (asset == null)
            {
                Debug.LogError("[KeybindManager] Null InputActionAsset.");
                return;
            }

            _asset = asset;
            _rhythmMap = _asset.FindActionMap("Rhythm");

            if (_rhythmMap == null)
            {
                Debug.LogError("[KeybindManager] 'Rhythm' action map not found.");
                return;
            }

            _laneActions = new InputAction[LaneCount];
            for (int i = 0; i < LaneCount; i++)
            {
                _laneActions[i] = _rhythmMap.FindAction($"Lane{i}");
            }

            _initialized = true;
            LoadBindings();
        }

        // =================================================================
        // SAVE / LOAD
        // =================================================================

        /// <summary>
        /// Save all current binding overrides to PlayerPrefs.
        /// </summary>
        public static void SaveBindings()
        {
            if (!_initialized) return;

            string json = _asset.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Load binding overrides from PlayerPrefs and apply them.
        /// Called automatically during Initialize.
        /// </summary>
        public static void LoadBindings()
        {
            if (!_initialized) return;

            string json = PlayerPrefs.GetString(PrefsKey, "");

            if (!string.IsNullOrEmpty(json))
            {
                _asset.LoadBindingOverridesFromJson(json);
            }
        }

        /// <summary>
        /// Remove all overrides and restore factory defaults.
        /// </summary>
        public static void ResetToDefaults()
        {
            if (!_initialized) return;

            _asset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        // =================================================================
        // BINDING QUERIES
        // =================================================================

        /// <summary>
        /// Get all keyboard binding indices for a lane action.
        /// Returns indices into the action's bindings array where
        /// the binding belongs to the "Keyboard" group.
        /// </summary>
        public static List<int> GetKeyboardBindingIndices(int lane)
        {
            var result = new List<int>();
            if (!IsValidLane(lane)) return result;

            var action = _laneActions[lane];
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isComposite || binding.isPartOfComposite) continue;

                if (IsKeyboardBinding(binding))
                {
                    result.Add(i);
                }
            }

            return result;
        }

        /// <summary>
        /// Get the display string for a specific binding of a lane.
        /// </summary>
        public static string GetBindingDisplayString(int lane, int bindingIndex)
        {
            if (!IsValidLane(lane)) return "???";

            var action = _laneActions[lane];
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                return "???";

            return action.GetBindingDisplayString(bindingIndex,
                InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
        }

        /// <summary>
        /// Get the effective path for a specific binding (with override applied).
        /// </summary>
        public static string GetEffectivePath(int lane, int bindingIndex)
        {
            if (!IsValidLane(lane)) return "";

            var action = _laneActions[lane];
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                return "";

            return action.bindings[bindingIndex].effectivePath;
        }

        // =================================================================
        // INTERACTIVE REBINDING
        // =================================================================

        /// <summary>
        /// Active rebinding operation. Dispose to cancel.
        /// </summary>
        private static InputActionRebindingExtensions.RebindingOperation _activeRebind;

        /// <summary>Whether a rebinding is currently in progress.</summary>
        public static bool IsRebinding => _activeRebind != null;

        /// <summary>
        /// Start an interactive rebind for a specific lane and binding index.
        /// The next keyboard key pressed becomes the new binding.
        /// 
        /// Only allows keyboard rebinding — gamepad bindings are excluded.
        /// </summary>
        /// <param name="lane">Lane index (0-3).</param>
        /// <param name="bindingIndex">Index into the action's bindings array.</param>
        /// <param name="onComplete">Called with the new display string on success.</param>
        /// <param name="onCancel">Called if rebinding is cancelled (Escape).</param>
        public static void StartRebind(int lane, int bindingIndex,
            Action<string> onComplete, Action onCancel = null)
        {
            if (!IsValidLane(lane)) return;
            if (_activeRebind != null)
            {
                _activeRebind.Cancel();
                _activeRebind.Dispose();
                _activeRebind = null;
            }

            var action = _laneActions[lane];

            // Disable the action during rebinding
            action.Disable();

            _activeRebind = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("Mouse")
                .WithControlsExcluding("Gamepad")
                .WithControlsExcluding("Touchscreen")
                .WithControlsExcluding("XRController")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(op =>
                {
                    op.Dispose();
                    _activeRebind = null;
                    action.Enable();

                    // Check for conflicts
                    string newPath = action.bindings[bindingIndex].effectivePath;
                    string conflict = FindConflict(lane, newPath);

                    if (conflict != null)
                    {
                        // Revert the binding
                        action.RemoveBindingOverride(bindingIndex);
                        Debug.LogWarning($"[KeybindManager] Conflict: {newPath} already bound to {conflict}");
                        onCancel?.Invoke();
                        return;
                    }

                    SaveBindings();

                    string display = action.GetBindingDisplayString(bindingIndex,
                        InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
                    onComplete?.Invoke(display);
                })
                .OnCancel(op =>
                {
                    op.Dispose();
                    _activeRebind = null;
                    action.Enable();
                    onCancel?.Invoke();
                })
                .Start();
        }

        /// <summary>
        /// Cancel any active rebinding operation.
        /// </summary>
        public static void CancelRebind()
        {
            if (_activeRebind != null)
            {
                _activeRebind.Cancel();
                // OnCancel callback handles disposal
            }
        }

        // =================================================================
        // CONFLICT DETECTION
        // =================================================================

        /// <summary>
        /// Check if a binding path is already used by another lane.
        /// Returns the conflicting lane name, or null if no conflict.
        /// </summary>
        public static string FindConflict(int excludeLane, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            for (int i = 0; i < LaneCount; i++)
            {
                if (i == excludeLane) continue;
                if (_laneActions[i] == null) continue;

                for (int b = 0; b < _laneActions[i].bindings.Count; b++)
                {
                    var binding = _laneActions[i].bindings[b];
                    if (binding.isComposite || binding.isPartOfComposite) continue;

                    if (string.Equals(binding.effectivePath, path,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return LaneNames[i];
                    }
                }
            }

            return null;
        }

        // =================================================================
        // HELPERS
        // =================================================================

        private static bool IsValidLane(int lane)
        {
            if (!_initialized)
            {
                Debug.LogError("[KeybindManager] Not initialized. Call Initialize first.");
                return false;
            }

            if (lane < 0 || lane >= LaneCount)
            {
                Debug.LogError($"[KeybindManager] Invalid lane: {lane}");
                return false;
            }

            return _laneActions[lane] != null;
        }

        private static bool IsKeyboardBinding(InputBinding binding)
        {
            string groups = binding.groups ?? "";
            string path = binding.effectivePath ?? "";

            return groups.Contains("Keyboard") || path.StartsWith("<Keyboard>");
        }
    }
}
