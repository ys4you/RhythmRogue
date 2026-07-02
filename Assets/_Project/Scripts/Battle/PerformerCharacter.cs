using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Shared base for on-screen rhythm performers (player, enemy). Owns the CharacterAnimator and
    /// the idle-bob wiring: it steps the animator's idle on every Conductor beat. Subclasses decide
    /// when to sing, by watching input (player) or the enemy note stream (enemy).
    /// </summary>
    public abstract class PerformerCharacter : MonoBehaviour
    {
        protected CharacterAnimator Animator;
        private Conductor _conductor;
        private bool _beatWired;

        /// <summary>Wire the shared beat-driven idle. Subclasses call this from their Initialize.</summary>
        protected void InitBase(CharacterAnimator animator, Conductor conductor)
        {
            Animator = animator;
            _conductor = conductor;
            if (_conductor != null)
            {
                _conductor.OnBeat += OnBeat;
                _beatWired = true;
            }
        }

        private void OnBeat(int beat) => Animator?.BeatStep();

        /// <summary>Map a lane (0 Left, 1 Down, 2 Up, 3 Right) to its sing pose.</summary>
        protected static CharacterState LaneToState(int lane) => lane switch
        {
            0 => CharacterState.SingLeft,
            1 => CharacterState.SingDown,
            2 => CharacterState.SingUp,
            3 => CharacterState.SingRight,
            _ => CharacterState.Idle
        };

        protected virtual void OnDestroy()
        {
            if (_beatWired && _conductor != null) _conductor.OnBeat -= OnBeat;
        }
    }
}
