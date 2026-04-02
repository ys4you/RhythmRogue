using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Abstract base for note highways (player and enemy).
    /// 
    /// Handles all shared functionality:
    ///   - Receptor management (pre-placed in scene or auto-generated)
    ///   - Note spawning from pool, scrolling by beat position, despawning
    ///   - Lane X positions and note scale read directly from receptor transforms
    ///     so what you see in the scene is exactly what you get at runtime
    /// 
    /// Subclasses only implement what's different:
    ///   - NoteHighway: player input feedback, miss detection, legacy chart support
    ///   - EnemyHighway: auto-hit flash, no input, no judgment
    /// </summary>
    public abstract class HighwayBase : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("Layout")]
        [Tooltip("Y position of the receptor / hit line.")]
        [SerializeField] protected float _receptorY = -3f;

        [Header("Scrolling")]
        [Tooltip("World units per beat. Controls visual scroll speed.")]
        [SerializeField] protected float _beatHeight = 2f;

        [Tooltip("How many beats ahead to spawn notes.")]
        [SerializeField] protected float _beatsShownInAdvance = 8f;

        [Tooltip("How many beats past the receptor before despawning.")]
        [SerializeField] protected float _beatsDespawnBehind = 2f;

        [Tooltip("True = notes scroll top-to-bottom (hit line at bottom).")]
        [SerializeField] protected bool _downscroll = true;

        [Header("Lane Colors")]
        [SerializeField] protected Color[] _laneColors =
        {
            new Color(1f, 0.3f, 0.3f),   // Left  — red
            new Color(0.3f, 0.8f, 1f),    // Down  — cyan
            new Color(0.3f, 1f, 0.3f),    // Up    — green
            new Color(1f, 1f, 0.3f)       // Right — yellow
        };

        [Header("References")]
        [SerializeField] protected NotePool _notePool;

        [Header("Receptors (drag from scene, or leave empty to auto-generate)")]
        [Tooltip("Pre-placed receptor SpriteRenderers, one per lane. Lane X and note scale are read from these transforms.")]
        [SerializeField] protected SpriteRenderer[] _receptors;

        [Header("Receptor Sprites (used for auto-generation and feedback)")]
        [SerializeField] protected Sprite _receptorIdleSprite;
        [SerializeField] protected Sprite _receptorPressedSprite;

        [Header("Auto-Generation Fallback")]
        [Tooltip("Lane X positions — only used if receptors are NOT pre-placed.")]
        [SerializeField] protected float[] _fallbackLanePositions = { -1.5f, -0.5f, 0.5f, 1.5f };

        [Tooltip("Note/receptor scale — only used if receptors are NOT pre-placed.")]
        [SerializeField] protected float _fallbackScale = 0.3f;

        // =================================================================
        // RUNTIME — derived from receptors at Awake
        // =================================================================

        /// <summary>World X per lane, read from receptor transforms.</summary>
        protected float[] _laneX;

        /// <summary>Scale for spawned notes, read from receptor transforms.</summary>
        protected float _noteScale;

        /// <summary>Cached Conductor reference.</summary>
        protected Conductor _conductor;

        // =================================================================
        // PUBLIC READ-ONLY — for HitFeedback, ReceptorAnimator, etc.
        // =================================================================

        /// <summary>World X per lane (read-only). Use to position feedback.</summary>
        public IReadOnlyList<float> LanePositions => _laneX;

        /// <summary>World Y of the receptor / hit line.</summary>
        public float ReceptorY => _receptorY;

        /// <summary>Receptor SpriteRenderers (read-only). Used by ReceptorAnimator.</summary>
        public IReadOnlyList<SpriteRenderer> Receptors => _receptors;

        // =================================================================
        // LANE ROTATION
        // =================================================================

        protected static readonly Quaternion[] LaneRotations =
        {
            Quaternion.Euler(0f, 0f, 90f),   // Left
            Quaternion.Euler(0f, 0f, 180f),  // Down
            Quaternion.identity,              // Up
            Quaternion.Euler(0f, 0f, -90f)   // Right
        };

        // =================================================================
        // LIFECYCLE
        // =================================================================

        protected virtual void Awake()
        {
            _conductor = Conductor.Instance;
            EnsureReceptors();
            ReadLaneDataFromReceptors();
        }

        // =================================================================
        // RECEPTOR SETUP
        // =================================================================

        /// <summary>
        /// If receptors are pre-placed in the scene, use them as-is.
        /// Otherwise auto-generate from the idle sprite using fallback values.
        /// </summary>
        private void EnsureReceptors()
        {
            if (HasPrePlacedReceptors())
                return;

            if (_receptorIdleSprite == null) return;

            _receptors = new SpriteRenderer[4];

            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"{GetReceptorPrefix()}_L{i}");
                go.transform.SetParent(transform);

                float x = (i < _fallbackLanePositions.Length) ? _fallbackLanePositions[i] : i;
                go.transform.position = new Vector3(x, _receptorY, 0f);
                go.transform.localRotation = LaneRotations[i];
                go.transform.localScale = Vector3.one * _fallbackScale;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _receptorIdleSprite;
                sr.color = (i < _laneColors.Length) ? _laneColors[i] : Color.white;
                sr.sortingOrder = 1;

                _receptors[i] = sr;
            }
        }

        private bool HasPrePlacedReceptors()
        {
            if (_receptors == null || _receptors.Length < 4) return false;

            for (int i = 0; i < 4; i++)
            {
                if (_receptors[i] == null) return false;
            }

            return true;
        }

        /// <summary>
        /// Read lane X, receptor Y, and note scale directly from receptor transforms.
        /// This is the source of truth — scene placement = runtime behavior.
        /// </summary>
        private void ReadLaneDataFromReceptors()
        {
            _laneX = new float[4];
            _noteScale = 1f;

            if (_receptors == null || _receptors.Length < 4) return;

            for (int i = 0; i < 4; i++)
            {
                if (_receptors[i] != null)
                {
                    _laneX[i] = _receptors[i].transform.position.x;
                }
            }

            // Read Y and scale from first receptor (they should all match)
            if (_receptors[0] != null)
            {
                _receptorY = _receptors[0].transform.position.y;
                _noteScale = _receptors[0].transform.lossyScale.x;
            }
        }

        /// <summary>Prefix for auto-generated receptor names.</summary>
        protected virtual string GetReceptorPrefix() => "Receptor";

        // =================================================================
        // SHARED — note positioning
        // =================================================================

        /// <summary>
        /// Position a note in world space based on beat distance from current beat.
        /// Lane X comes from the receptor transforms — always correct.
        /// </summary>
        protected void PositionNote(NoteView note, float currentBeat)
        {
            float distanceInBeats = note.Data.BeatPosition - currentBeat;
            float worldY = _receptorY + (distanceInBeats * _beatHeight);

            int lane = Mathf.Clamp(note.Data.Lane, 0, _laneX.Length - 1);
            float worldX = _laneX[lane];

            note.transform.position = new Vector3(worldX, worldY, 0f);
        }

        /// <summary>
        /// Spawn a NoteView from the pool, set it up, scale it, and position it.
        /// Returns the view for subclasses to track.
        /// </summary>
        protected NoteView SpawnNoteView(NoteData noteData, int noteIndex, float currentBeat)
        {
            NoteView view = _notePool.Get();

            int lane = Mathf.Clamp(noteData.Lane, 0, _laneColors.Length - 1);
            Color color = _laneColors[lane];

            view.Setup(noteData, noteIndex, color, _beatHeight, _downscroll);
            view.transform.localScale = Vector3.one * _noteScale;
            PositionNote(view, currentBeat);

            return view;
        }

        /// <summary>
        /// Return a NoteView to the pool.
        /// </summary>
        protected void ReturnNoteView(NoteView view)
        {
            if (view != null && _notePool != null)
                _notePool.Release(view);
        }
    }
}