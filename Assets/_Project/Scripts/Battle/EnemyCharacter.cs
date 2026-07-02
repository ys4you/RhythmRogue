using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Enemy performer: sings the lane of each enemy note as it lands, driven by
    /// <see cref="EnemyHighway.OnAutoHit"/>, then eases back to idle a beat-fraction later unless
    /// another note re-triggers it. The idle bop and beat wiring come from
    /// <see cref="PerformerCharacter"/>. The FNF opponent side of the call-and-response.
    /// </summary>
    public class EnemyCharacter : PerformerCharacter
    {
        // Minimum time a sung pose holds after an enemy note lands before easing back to idle.
        // Taps use this short floor so dense passages stay expressive; holds (sliders) override it
        // with their own sustain length so the pose stays up for the whole note.
        private const float SingHoldSeconds = 0.18f;

        private EnemyHighway _highway;
        private bool _hitWired;
        private float _singHoldRemaining;

        /// <summary>Wire the character to the enemy note stream and the beat. Call once from the factory.</summary>
        public void Initialize(CharacterAnimator animator, Conductor conductor, EnemyHighway highway)
        {
            InitBase(animator, conductor);
            _highway = highway;
            if (_highway != null)
            {
                _highway.OnAutoHit += OnAutoHit;
                _hitWired = true;
            }
        }

        private void OnAutoHit(int lane, float holdSeconds)
        {
            Animator?.SetState(LaneToState(lane));
            // Hold the pose for the length of a sustain (slider); taps pass 0 and fall back to
            // the short ease. Fixes the pose snapping back to idle partway through a hold.
            _singHoldRemaining = Mathf.Max(SingHoldSeconds, holdSeconds);
        }

        private void Update()
        {
            if (_singHoldRemaining <= 0f) return;
            _singHoldRemaining -= Time.deltaTime;
            if (_singHoldRemaining <= 0f) Animator?.SetState(CharacterState.Idle);
        }

        protected override void OnDestroy()
        {
            if (_hitWired && _highway != null) _highway.OnAutoHit -= OnAutoHit;
            base.OnDestroy();
        }
    }
}
