using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Player performer: a lane press shows that lane's sing pose, held while the key is held (so
    /// hold notes read correctly), falling back to any other held lane or to idle on release. The
    /// idle bop and beat wiring come from <see cref="PerformerCharacter"/>.
    /// </summary>
    public class PlayerCharacter : PerformerCharacter
    {
        private InputHandler _input;
        private bool _inputWired;

        /// <summary>Wire the character to input and the beat. Call once from the factory.</summary>
        public void Initialize(CharacterAnimator animator, InputHandler input, Conductor conductor)
        {
            InitBase(animator, conductor);
            _input = input;
            if (_input != null)
            {
                _input.OnLanePressed += OnLanePressed;
                _input.OnLaneReleased += OnLaneReleased;
                _inputWired = true;
            }
        }

        private void OnLanePressed(int lane) => Animator?.SetState(LaneToState(lane));

        private void OnLaneReleased(int lane)
        {
            if (Animator == null) return;
            int held = FirstHeldLane();
            Animator.SetState(held >= 0 ? LaneToState(held) : CharacterState.Idle);
        }

        private int FirstHeldLane()
        {
            if (_input == null) return -1;
            for (int i = 0; i < InputHandler.LaneCount; i++)
                if (_input.IsLaneHeld(i)) return i;
            return -1;
        }

        protected override void OnDestroy()
        {
            if (_inputWired && _input != null)
            {
                _input.OnLanePressed -= OnLanePressed;
                _input.OnLaneReleased -= OnLaneReleased;
            }
            base.OnDestroy();
        }
    }
}
