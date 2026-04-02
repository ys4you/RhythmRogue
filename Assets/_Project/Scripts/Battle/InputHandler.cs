using System;
using RhythmRogue.Core;
using RhythmRogue.Util;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Reads player input for the 4 rhythm lanes using Unity's Input System.
    /// 
    /// Event-driven: subscribes to InputAction callbacks (started/canceled)
    /// rather than polling every frame. This gives better timing precision
    /// than Input.GetKeyDown — the callback fires at the moment the input
    /// event is processed, not on the next frame.
    /// 
    /// On Awake, initializes KeybindManager to load any saved binding
    /// overrides before input is processed.
    /// </summary>
    public class InputHandler : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("Input Actions")]
        [Tooltip("Drag the RhythmActions.inputactions asset here.")]
        [SerializeField] private InputActionAsset _inputActions;

        /// <summary>Number of lanes.</summary>
        public const int LaneCount = 4;

        // =================================================================
        // STATE
        // =================================================================

        private readonly bool[] _isHeld = new bool[LaneCount];
        private InputAction[] _laneActions;
        private InputActionMap _rhythmMap;

        // =================================================================
        // EVENTS
        // =================================================================

        /// <summary>
        /// Fired when a lane key is pressed.
        /// Parameter: lane index (0-3).
        /// </summary>
        public event Action<int> OnLanePressed;

        /// <summary>
        /// Fired when a lane key is released.
        /// Parameter: lane index (0-3).
        /// </summary>
        public event Action<int> OnLaneReleased;

        // =================================================================
        // PUBLIC QUERIES
        // =================================================================

        /// <summary>
        /// Check if a lane is currently held down.
        /// </summary>
        public bool IsLaneHeld(int lane)
        {
            if (lane < 0 || lane >= LaneCount) return false;
            return _isHeld[lane];
        }

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Awake()
        {
            if (_inputActions == null)
            {
                GameLog.Error("[InputHandler] No InputActionAsset assigned!");
                return;
            }

            // Initialize keybind manager FIRST — loads saved overrides
            // before we resolve actions, so overrides are already applied
            KeybindManager.Initialize(_inputActions);

            // Find the Rhythm action map
            _rhythmMap = _inputActions.FindActionMap("Rhythm");

            if (_rhythmMap == null)
            {
                GameLog.Error("[InputHandler] 'Rhythm' action map not found in InputActionAsset.");
                return;
            }

            // Resolve lane actions by name
            _laneActions = new InputAction[LaneCount];

            for (int i = 0; i < LaneCount; i++)
            {
                string actionName = $"Lane{i}";
                _laneActions[i] = _rhythmMap.FindAction(actionName);

                if (_laneActions[i] == null)
                {
                    GameLog.Error($"[InputHandler] Action '{actionName}' not found in Rhythm map.");
                }
            }
        }

        private void OnEnable()
        {
            if (_laneActions == null) return;

            for (int i = 0; i < LaneCount; i++)
            {
                if (_laneActions[i] == null) continue;

                int lane = i;
                _laneActions[i].started += _ => HandlePress(lane);
                _laneActions[i].canceled += _ => HandleRelease(lane);
                _laneActions[i].Enable();
            }
        }

        private void OnDisable()
        {
            if (_laneActions == null) return;

            for (int i = 0; i < LaneCount; i++)
            {
                if (_laneActions[i] == null) continue;

                _laneActions[i].Disable();
                _isHeld[i] = false;
            }
        }

        // =================================================================
        // CALLBACKS
        // =================================================================

        private void HandlePress(int lane)
        {
            _isHeld[lane] = true;
            OnLanePressed?.Invoke(lane);
        }

        private void HandleRelease(int lane)
        {
            _isHeld[lane] = false;
            OnLaneReleased?.Invoke(lane);
        }

        // =================================================================
        // CLEANUP
        // =================================================================

        private void OnDestroy()
        {
            OnLanePressed = null;
            OnLaneReleased = null;
        }
    }
}