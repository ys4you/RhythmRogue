using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Drives a <see cref="CharacterAnimator"/> from the player's inputs: the idle bop steps on
    /// the Conductor beat, a lane press shows that lane's sing pose, and the pose is held while
    /// the key is held (so hold notes read correctly), falling back to any other held lane or to
    /// idle on release. A thin translator between input and animation; reactions like a hit flash
    /// can hang off the same events later.
    ///
    /// Intended to share a base with a future EnemyCharacter (same beat wiring, different sing
    /// source). Kept concrete for now to avoid abstracting before the second user exists.
    /// </summary>
    public class PlayerCharacter : MonoBehaviour
    {
        private CharacterAnimator _animator;
        private InputHandler _input;
        private Conductor _conductor;
        private bool _wired;

        /// <summary>Wire the character to its input and beat sources. Call once from the factory.</summary>
        public void Initialize(CharacterAnimator animator, InputHandler input, Conductor conductor)
        {
            _animator = animator;
            _input = input;
            _conductor = conductor;

            if (_input != null)
            {
                _input.OnLanePressed += OnLanePressed;
                _input.OnLaneReleased += OnLaneReleased;
            }
            if (_conductor != null)
                _conductor.OnBeat += OnBeat;

            _wired = true;
        }

        private void OnBeat(int beat) => _animator?.BeatStep();

        private void OnLanePressed(int lane) => _animator?.SetState(LaneToState(lane));

        private void OnLaneReleased(int lane)
        {
            if (_animator == null) return;
            int held = FirstHeldLane();
            _animator.SetState(held >= 0 ? LaneToState(held) : CharacterState.Idle);
        }

        private int FirstHeldLane()
        {
            if (_input == null) return -1;
            for (int i = 0; i < InputHandler.LaneCount; i++)
                if (_input.IsLaneHeld(i)) return i;
            return -1;
        }

        private static CharacterState LaneToState(int lane) => lane switch
        {
            0 => CharacterState.SingLeft,
            1 => CharacterState.SingDown,
            2 => CharacterState.SingUp,
            3 => CharacterState.SingRight,
            _ => CharacterState.Idle
        };

        private void OnDestroy()
        {
            if (!_wired) return;
            if (_input != null)
            {
                _input.OnLanePressed -= OnLanePressed;
                _input.OnLaneReleased -= OnLaneReleased;
            }
            if (_conductor != null)
                _conductor.OnBeat -= OnBeat;
        }
    }
}
