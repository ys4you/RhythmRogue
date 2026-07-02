using UnityEngine;
using RhythmRogue.Data;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Plays character animation clips on a SpriteRenderer. A pure clip player: it knows nothing
    /// about input or the beat, it just shows a state and advances frames. Idle is stepped
    /// externally on the beat (see <see cref="BeatStep"/>); sing/miss clips advance at their own
    /// fps in Update and hold the last frame when they end.
    ///
    /// The renderer lives on a child "Visual" transform so pose offsets never fight the root's
    /// placement. Both the player and (next pass) the enemy reuse this component.
    /// </summary>
    public class CharacterAnimator : MonoBehaviour
    {
        private CharacterVisualConfig _config;
        private SpriteRenderer _renderer;
        private Transform _visual;

        private Vector3 _baseLocalPos;
        private float _baseScale;

        private CharacterState _state = CharacterState.Idle;
        private CharacterClip _clip;
        private int _frameIndex;
        private float _frameTimer;

        /// <summary>Wire up the animator. Call once from the factory after adding it.</summary>
        public void Initialize(CharacterVisualConfig config, SpriteRenderer spriteRenderer, Transform visual)
        {
            _config = config;
            _renderer = spriteRenderer;
            _visual = visual;
            _baseLocalPos = visual != null ? visual.localPosition : Vector3.zero;
            _baseScale = config != null ? config.baseScale : 1f;

            SetState(CharacterState.Idle, force: true);
        }

        /// <summary>
        /// Switch to a pose. Re-triggers even if it's the same sing state (a new note re-pops the
        /// pose); calling Idle while already idle is a no-op so the beat bob keeps its phase.
        /// </summary>
        public void SetState(CharacterState state, bool force = false)
        {
            if (_config == null) return;
            if (!force && state == CharacterState.Idle && _state == CharacterState.Idle) return;

            _state = state;
            _clip = _config.GetClip(state);
            _frameIndex = 0;
            _frameTimer = 0f;
            ApplyFrame(0);
        }

        /// <summary>Advance the idle bop one step. Called on each Conductor beat by the performer.</summary>
        public void BeatStep()
        {
            if (_state != CharacterState.Idle || _clip == null || !_clip.loop) return;
            if (_clip.frames.Length <= 1) return;
            _frameIndex = (_frameIndex + 1) % _clip.frames.Length;
            ApplyFrame(_frameIndex);
        }

        private void Update()
        {
            // Idle is beat-driven; only sing/miss clips advance on fps here.
            if (_state == CharacterState.Idle || _clip == null || _clip.frames.Length <= 1) return;

            float fps = _clip.fps > 0.01f ? _clip.fps : 12f;
            float frameDuration = 1f / fps;
            _frameTimer += Time.deltaTime;

            while (_frameTimer >= frameDuration && _frameIndex < _clip.frames.Length - 1)
            {
                _frameTimer -= frameDuration;
                _frameIndex++;
                ApplyFrame(_frameIndex);
            }

            // Reached the end: loop back to the start, or hold the last frame (sustained pose).
            if (_frameIndex >= _clip.frames.Length - 1 && _clip.loop)
            {
                _frameIndex = 0;
                _frameTimer = 0f;
                ApplyFrame(0);
            }
        }

        private void ApplyFrame(int index)
        {
            if (_clip == null || !_clip.HasFrames || _renderer == null || _visual == null) return;
            index = Mathf.Clamp(index, 0, _clip.frames.Length - 1);
            CharacterFrame f = _clip.frames[index];
            if (f == null) return;

            if (f.sprite != null) _renderer.sprite = f.sprite;

            float scale = f.scale > 0f ? f.scale : 1f;
            _visual.localPosition = _baseLocalPos + new Vector3(f.offset.x, f.offset.y, 0f);
            _visual.localScale = Vector3.one * (_baseScale * scale);
            _renderer.flipX = (_config != null && _config.flip) ^ f.flipX;
        }
    }
}
