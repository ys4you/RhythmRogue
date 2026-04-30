using System;
using RhythmRogue.Core;
using RhythmRogue.Util;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Reads 4-lane rhythm input via Unity Input System callbacks for precise timing.
    /// </summary>
    public class InputHandler : MonoBehaviour
    {
        [Header("Input Actions")]
        [SerializeField] private InputActionAsset _inputActions;

        public const int LaneCount = 4;

        private readonly bool[] _isHeld = new bool[LaneCount];
        private InputAction[] _laneActions;
        private InputActionMap _rhythmMap;

        public event Action<int> OnLanePressed;
        public event Action<int> OnLaneReleased;

        public bool IsLaneHeld(int lane) => lane >= 0 && lane < LaneCount && _isHeld[lane];

        private void Awake()
        {
            if (_inputActions == null) { GameLog.Error("[InputHandler] No InputActionAsset!"); return; }

            // Load saved binding overrides before resolving actions
            KeybindManager.Initialize(_inputActions);
            _rhythmMap = _inputActions.FindActionMap("Rhythm");
            if (_rhythmMap == null) { GameLog.Error("[InputHandler] 'Rhythm' action map not found."); return; }

            _laneActions = new InputAction[LaneCount];
            for (int i = 0; i < LaneCount; i++)
            {
                _laneActions[i] = _rhythmMap.FindAction($"Lane{i}");
                if (_laneActions[i] == null) GameLog.Error($"[InputHandler] Action 'Lane{i}' not found.");
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

        private void HandlePress(int lane) { _isHeld[lane] = true; OnLanePressed?.Invoke(lane); }
        private void HandleRelease(int lane) { _isHeld[lane] = false; OnLaneReleased?.Invoke(lane); }
        private void OnDestroy() { OnLanePressed = null; OnLaneReleased = null; }
    }
}
