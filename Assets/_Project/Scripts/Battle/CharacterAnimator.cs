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
    /// Clips come from an <see cref="ICharacterVisual"/> (the authored CharacterVisualConfig for the
    /// player, or a RuntimeCharacterVisual posing one sprite for the enemy), so the same animator
    /// drives any performer. Pose offsets are relative to the visual transform's starting position
    /// and scale, so attaching straight onto an already-placed, already-scaled renderer works.
    /// </summary>
    public class CharacterAnimator : MonoBehaviour
    {
        private ICharacterVisual _source;
        private bool _flip;
        private float _designScale;

        private SpriteRenderer _renderer;
        private Transform _visual;
        private Vector3 _baseLocalPos;
        private Vector3 _baseVisualScale;

        private CharacterState _state = CharacterState.Idle;
        private CharacterClip _clip;
        private int _frameIndex;
        private float _frameTimer;

        /// <summary>Wire the animator to a visual source. Call once from the factory.</summary>
        public void Initialize(ICharacterVisual source, SpriteRenderer spriteRenderer, Transform visual)
        {
            _source = source;
            _flip = source != null && source.Flip;
            _designScale = source != null ? source.BaseScale : 1f;
            _renderer = spriteRenderer;
            _visual = visual;
            _baseLocalPos = visual != null ? visual.localPosition : Vector3.zero;
            _baseVisualScale = visual != null ? visual.localScale : Vector3.one;
            SetState(CharacterState.Idle, force: true);
        }

        /// <summary>
        /// Switch to a pose. Re-triggers even if it's the same sing state (a new note re-pops the
        /// pose); calling Idle while already idle is a no-op so the beat bob keeps its phase.
        /// </summary>
        public void SetState(CharacterState state, bool force = false)
        {
            if (!force && state == CharacterState.Idle && _state == CharacterState.Idle) return;

            _state = state;
            _clip = ResolveClip(state);
            _frameIndex = 0;
            _frameTimer = 0f;
            ApplyFrame(0);
        }

        private CharacterClip ResolveClip(CharacterState state)
        {
            if (_source != null) return _source.GetClip(state);
            return SpritePoser.GetClip(PlaceholderCharacterArt.PlaceholderSprite, state);
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
            _visual.localScale = _baseVisualScale * (_designScale * scale);
            _renderer.flipX = _flip ^ f.flipX;
        }
    }
}
