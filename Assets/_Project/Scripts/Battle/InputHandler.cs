using System;
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
    /// Bindings are defined in the RhythmActions.inputactions asset:
    ///   Lane 0 (Left):  Arrow Left  / D-pad Left  / Gamepad West (X/□)
    ///   Lane 1 (Down):  Arrow Down  / D-pad Down  / Gamepad South (A/✕)
    ///   Lane 2 (Up):    Arrow Up    / D-pad Up    / Gamepad North (Y/△)
    ///   Lane 3 (Right): Arrow Right / D-pad Right / Gamepad East (B/○)
    /// 
    /// Players can rebind keys at runtime using Unity's built-in rebinding
    /// API on the InputActionAsset — no code changes needed.
    /// 
    /// SOLID breakdown:
    /// - S: Only reads input and fires events. No note matching, no scoring.
    /// - O: Add new devices by editing the InputActions asset, not this code.
    /// - L: Consumers subscribe to C# events regardless of input source.
    /// - I: Two focused events, one held-state query.
    /// - D: No dependencies on gameplay systems.
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

        /// <summary>Per-lane held state for hold note tracking.</summary>
        private readonly bool[] _isHeld = new bool[LaneCount];

        /// <summary>Resolved action references, one per lane.</summary>
        private InputAction[] _laneActions;

        /// <summary>The Rhythm action map from the asset.</summary>
        private InputActionMap _rhythmMap;

        // =================================================================
        // EVENTS
        // =================================================================

        /// <summary>
        /// Fired when a lane key is pressed.
        /// Parameter: lane index (0-3).
        /// Consumers: NoteMatcher (find nearest note), NoteHighway (receptor flash).
        /// </summary>
        public event Action<int> OnLanePressed;

        /// <summary>
        /// Fired when a lane key is released.
        /// Parameter: lane index (0-3).
        /// Consumers: Hold note detection (PROTO-006).
        /// </summary>
        public event Action<int> OnLaneReleased;

        // =================================================================
        // PUBLIC QUERIES
        // =================================================================

        /// <summary>
        /// Check if a lane is currently held down.
        /// Used by hold note detection to track sustained input.
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
                Debug.LogError("[InputHandler] No InputActionAsset assigned!");
                return;
            }

            // Find the Rhythm action map
            _rhythmMap = _inputActions.FindActionMap("Rhythm");

            if (_rhythmMap == null)
            {
                Debug.LogError("[InputHandler] 'Rhythm' action map not found in InputActionAsset.");
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
                    Debug.LogError($"[InputHandler] Action '{actionName}' not found in Rhythm map.");
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
