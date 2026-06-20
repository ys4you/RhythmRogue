using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmRogue.Core
{
    /// <summary>
    /// Runtime keybinding overrides via Unity Input System. Stored in PlayerPrefs.
    /// Supports interactive rebinding with conflict detection.
    /// </summary>
    public static class KeybindManager
    {
        private const string PrefsKey = "rhythmBindingOverrides";

        private static InputActionAsset _asset;
        private static InputActionMap _rhythmMap;
        private static InputAction[] _laneActions;
        private static bool _initialized;

        public static readonly string[] LaneNames = { "Left", "Down", "Up", "Right" };
        public const int LaneCount = 4;

        public static void Initialize(InputActionAsset asset)
        {
            if (asset == null) { Debug.LogError("[KeybindManager] Null InputActionAsset."); return; }
            _asset = asset;
            _rhythmMap = _asset.FindActionMap("Rhythm");
            if (_rhythmMap == null) { Debug.LogError("[KeybindManager] 'Rhythm' action map not found."); return; }

            _laneActions = new InputAction[LaneCount];
            for (int i = 0; i < LaneCount; i++) _laneActions[i] = _rhythmMap.FindAction($"Lane{i}");

            _initialized = true;
            LoadBindings();
        }

        public static void SaveBindings()
        {
            if (!_initialized) return;
            PlayerPrefs.SetString(PrefsKey, _asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public static void LoadBindings()
        {
            if (!_initialized) return;
            string json = PlayerPrefs.GetString(PrefsKey, "");
            if (!string.IsNullOrEmpty(json)) _asset.LoadBindingOverridesFromJson(json);
        }

        public static void ResetToDefaults()
        {
            if (!_initialized) return;
            _asset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        public static List<int> GetKeyboardBindingIndices(int lane)
        {
            var result = new List<int>();
            if (!IsValidLane(lane)) return result;
            var action = _laneActions[lane];
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (!b.isComposite && !b.isPartOfComposite && IsKeyboardBinding(b)) result.Add(i);
            }
            return result;
        }

        public static string GetBindingDisplayString(int lane, int bindingIndex, bool shortNames = false)
        {
            if (!IsValidLane(lane)) return "???";
            var action = _laneActions[lane];
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count) return "???";
            // shortNames uses the control's short display name where it has one, so an arrow key reads
            // as the glyph the player presses rather than the word "Left/Down/Up/Right" (which looks
            // like a lane name). Settings keeps the long form by default for clarity.
            var options = shortNames
                ? default(InputBinding.DisplayStringOptions)
                : InputBinding.DisplayStringOptions.DontUseShortDisplayNames;
            return action.GetBindingDisplayString(bindingIndex, options);
        }

        public static string GetEffectivePath(int lane, int bindingIndex)
        {
            if (!IsValidLane(lane)) return "";
            var action = _laneActions[lane];
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count) return "";
            return action.bindings[bindingIndex].effectivePath;
        }

        private static InputActionRebindingExtensions.RebindingOperation _activeRebind;
        public static bool IsRebinding => _activeRebind != null;

        public static void StartRebind(int lane, int bindingIndex, Action<string> onComplete, Action onCancel = null)
        {
            if (!IsValidLane(lane)) return;
            if (_activeRebind != null) { _activeRebind.Cancel(); _activeRebind.Dispose(); _activeRebind = null; }

            var action = _laneActions[lane];
            action.Disable();

            _activeRebind = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("Mouse")
                .WithControlsExcluding("Gamepad")
                .WithControlsExcluding("Touchscreen")
                .WithControlsExcluding("XRController")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(op =>
                {
                    op.Dispose(); _activeRebind = null; action.Enable();

                    string newPath = action.bindings[bindingIndex].effectivePath;
                    string conflict = FindConflict(lane, newPath);
                    if (conflict != null)
                    {
                        action.RemoveBindingOverride(bindingIndex);
                        onCancel?.Invoke();
                        return;
                    }

                    SaveBindings();
                    onComplete?.Invoke(action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontUseShortDisplayNames));
                })
                .OnCancel(op => { op.Dispose(); _activeRebind = null; action.Enable(); onCancel?.Invoke(); })
                .Start();
        }

        public static void CancelRebind()
        {
            if (_activeRebind != null) _activeRebind.Cancel();
        }

        public static string FindConflict(int excludeLane, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            for (int i = 0; i < LaneCount; i++)
            {
                if (i == excludeLane || _laneActions[i] == null) continue;
                for (int b = 0; b < _laneActions[i].bindings.Count; b++)
                {
                    var binding = _laneActions[i].bindings[b];
                    if (binding.isComposite || binding.isPartOfComposite) continue;
                    if (string.Equals(binding.effectivePath, path, StringComparison.OrdinalIgnoreCase)) return LaneNames[i];
                }
            }
            return null;
        }

        private static bool IsValidLane(int lane)
        {
            if (!_initialized) { Debug.LogError("[KeybindManager] Not initialized."); return false; }
            return lane >= 0 && lane < LaneCount && _laneActions[lane] != null;
        }

        private static bool IsKeyboardBinding(InputBinding binding)
        {
            return (binding.groups ?? "").Contains("Keyboard") || (binding.effectivePath ?? "").StartsWith("<Keyboard>");
        }
    }
}
