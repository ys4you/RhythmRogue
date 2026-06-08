using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Abstract base for note highways. Handles receptors, spawning, scrolling, despawning.
    /// Lane positions and note scale are read from receptor transforms at Awake.
    /// </summary>
    public abstract class HighwayBase : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] protected float _receptorY = -3f;

        [Header("Scrolling")]
        [Tooltip("Legacy base height per beat. Scroll speed now comes from ScrollSpeedSetting as a " +
                 "constant world-units-per-second velocity, so this no longer sets the speed; it is " +
                 "kept only for any older references.")]
        [SerializeField] protected float _beatHeight = 2f;
        [Tooltip("How far above/below the receptor, in WORLD UNITS, notes spawn. Keep this at least " +
                 "the visible height of the highway so notes never pop in on-screen. With constant-speed " +
                 "scrolling a note is then visible for (this / speed) seconds before it reaches the receptor.")]
        [SerializeField] protected float _spawnAheadUnits = DefaultSpawnAheadUnits;
        [Tooltip("How far past the receptor, in beats, a note travels before it is recycled.")]
        [SerializeField] protected float _beatsDespawnBehind = 2f;

        [Header("Scroll Direction")]
        [Tooltip("When upscroll is selected the whole playfield mirrors vertically. If true, it " +
                 "mirrors around the battle camera's Y, so the receptors land at the top of the same " +
                 "view the downscroll receptors sat at the bottom of (recommended). Turn off to use a " +
                 "fixed manual axis instead.")]
        [SerializeField] protected bool _flipAroundCamera = true;
        [Tooltip("Manual mirror axis (world Y), used only when 'Flip Around Camera' is off.")]
        [SerializeField] protected float _flipPivotY = 0f;

        [Header("Lane Colors")]
        [SerializeField] protected Color[] _laneColors =
        {
            new Color(1f, 0.3f, 0.3f),
            new Color(0.3f, 0.8f, 1f),
            new Color(0.3f, 1f, 0.3f),
            new Color(1f, 1f, 0.3f)
        };

        [Header("References")]
        [SerializeField] protected NotePool _notePool;

        [Header("Receptors")]
        [SerializeField] protected SpriteRenderer[] _receptors;
        [SerializeField] protected Sprite _receptorIdleSprite;
        [SerializeField] protected Sprite _receptorPressedSprite;

        [Header("Auto-Generation Fallback")]
        [SerializeField] protected float[] _fallbackLanePositions = { -1.5f, -0.5f, 0.5f, 1.5f };
        [SerializeField] protected float _fallbackScale = 0.3f;

        protected float[] _laneX;
        protected float _noteScale;
        protected Conductor _conductor;

        // Scroll-direction runtime state. _baseReceptorY is the scene-authored (downscroll)
        // receptor Y; the active _receptorY is mirrored from it for upscroll. _dirSign is +1
        // downscroll (upcoming notes sit above the receptor and fall) or -1 upscroll.
        protected float _baseReceptorY;
        protected float _dirSign = 1f;
        protected bool _appliedDownscroll = true;

        // Note travel in world units per beat, derived so the on-screen speed is a constant
        // ScrollSpeedSetting.UnitsPerSecond regardless of the song's BPM (units/beat = u/s * 60 / BPM).
        protected float EffectiveBeatHeight => ScrollSpeedSetting.UnitsPerSecond * 60f / CurrentBpm;

        // Current song BPM, with a safe fallback before the conductor starts (prevents div-by-zero).
        protected float CurrentBpm => (_conductor != null && _conductor.BPM > 1f) ? _conductor.BPM : 120f;

        // Spawn lead converted from a fixed world distance into beats for the current speed and BPM,
        // so notes always appear the same distance off-screen (no pop-in at low speeds or high BPM).
        protected float SpawnAheadBeats => _spawnAheadUnits / Mathf.Max(0.01f, EffectiveBeatHeight);

        // Default off-screen spawn distance in world units (matches the _spawnAheadUnits default).
        // Exposed so chart assembly can size the opening note-free lead-in to match it: the first
        // note must start at least this far out so it scrolls in cleanly instead of popping in.
        public const float DefaultSpawnAheadUnits = 18f;

        public IReadOnlyList<float> LanePositions => _laneX;
        public float ReceptorY => _receptorY;
        public IReadOnlyList<SpriteRenderer> Receptors => _receptors;

        protected static readonly Quaternion[] LaneRotations =
        {
            Quaternion.Euler(0f, 0f, 90f),
            Quaternion.Euler(0f, 0f, 180f),
            Quaternion.identity,
            Quaternion.Euler(0f, 0f, -90f)
        };

        protected virtual void Awake()
        {
            _conductor = Conductor.Instance;
            EnsureReceptors();
            ReadLaneDataFromReceptors();
            _baseReceptorY = _receptorY;
            ApplyScrollDirection();
        }

        private void EnsureReceptors()
        {
            if (HasPrePlacedReceptors()) return;
            if (_receptorIdleSprite == null) return;

            _receptors = new SpriteRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"{GetReceptorPrefix()}_L{i}");
                go.transform.SetParent(transform);
                float x = i < _fallbackLanePositions.Length ? _fallbackLanePositions[i] : i;
                go.transform.position = new Vector3(x, _receptorY, 0f);
                go.transform.localRotation = LaneRotations[i];
                go.transform.localScale = Vector3.one * _fallbackScale;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _receptorIdleSprite;
                sr.color = i < _laneColors.Length ? _laneColors[i] : Color.white;
                sr.sortingOrder = 1;
                _receptors[i] = sr;
            }
        }

        private bool HasPrePlacedReceptors()
        {
            if (_receptors == null || _receptors.Length < 4) return false;
            for (int i = 0; i < 4; i++) if (_receptors[i] == null) return false;
            return true;
        }

        private void ReadLaneDataFromReceptors()
        {
            _laneX = new float[4];
            _noteScale = 1f;
            if (_receptors == null || _receptors.Length < 4) return;

            for (int i = 0; i < 4; i++)
                if (_receptors[i] != null) _laneX[i] = _receptors[i].transform.position.x;

            if (_receptors[0] != null)
            {
                _receptorY = _receptors[0].transform.position.y;
                _noteScale = _receptors[0].transform.lossyScale.x;
            }
        }

        protected virtual string GetReceptorPrefix() => "Receptor";

        protected void PositionNote(NoteView note, float currentBeat)
        {
            float distanceInBeats = note.Data.BeatPosition - currentBeat;
            int lane = Mathf.Clamp(note.Data.Lane, 0, _laneX.Length - 1);
            note.transform.position = new Vector3(_laneX[lane], _receptorY + _dirSign * distanceInBeats * EffectiveBeatHeight, 0f);
        }

        protected NoteView SpawnNoteView(NoteData noteData, int noteIndex, float currentBeat)
        {
            NoteView view = _notePool.Get();
            int lane = Mathf.Clamp(noteData.Lane, 0, _laneColors.Length - 1);
            view.Setup(noteData, noteIndex, _laneColors[lane], EffectiveBeatHeight, _appliedDownscroll);
            view.transform.localScale = Vector3.one * _noteScale;
            PositionNote(view, currentBeat);
            return view;
        }

        protected void ReturnNoteView(NoteView view)
        {
            if (view != null && _notePool != null) _notePool.Release(view);
        }

        /// <summary>
        /// Read the global scroll-direction setting and apply it: pick the receptor Y (mirrored
        /// for upscroll) and the travel sign, then move the receptor sprites onto that line.
        /// Subclasses override to also re-place their live notes so a mid-battle flip is seamless.
        /// </summary>
        public virtual void ApplyScrollDirection()
        {
            bool down = ScrollDirectionSetting.Downscroll;
            _appliedDownscroll = down;
            _dirSign = down ? 1f : -1f;

            float pivot = _flipAroundCamera && Camera.main != null ? Camera.main.transform.position.y : _flipPivotY;
            _receptorY = down ? _baseReceptorY : (2f * pivot - _baseReceptorY);

            if (_receptors != null)
            {
                for (int i = 0; i < _receptors.Length; i++)
                {
                    if (_receptors[i] == null) continue;
                    Vector3 p = _receptors[i].transform.position;
                    _receptors[i].transform.position = new Vector3(p.x, _receptorY, p.z);
                }
            }
        }

        /// <summary>Re-apply the scroll direction if the global setting changed since last frame.</summary>
        protected void SyncScrollDirection()
        {
            if (ScrollDirectionSetting.Downscroll != _appliedDownscroll)
                ApplyScrollDirection();
        }
    }
}
