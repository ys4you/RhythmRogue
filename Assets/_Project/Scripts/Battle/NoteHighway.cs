using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Manages the 4-lane note highway: spawning, scrolling, and despawning
    /// notes based on the Conductor's beat position.
    /// 
    /// Notes are spawned from an object pool when they enter the visible
    /// window (beatsShownInAdvance ahead of the current beat) and despawned
    /// when they pass beyond the miss window behind the hit line.
    /// 
    /// Scroll position formula:
    ///   distanceInBeats = note.beatPosition - conductor.SongPositionInBeats
    ///   worldY = hitLineY + (distanceInBeats * beatHeight)
    /// 
    /// Notes at the current beat sit exactly on the hit line.
    /// Notes in the future are above it (positive Y).
    /// Notes in the past are below it (negative Y).
    /// 
    /// Uses FloatExtensions.Remap where useful for position mapping.
    /// 
    /// SOLID breakdown:
    /// - S: Only manages note visuals and lifecycle. No input, no scoring.
    /// - O: Note types are handled via NoteView setup, not by modifying this class.
    /// - L: Consumers read ActiveNotes for hit detection without knowing internals.
    /// - I: Exposes minimal public surface for hit detection to query.
    /// - D: Depends on IConductor and NotePool abstractions.
    /// </summary>
    public class NoteHighway : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR — tuning values
        // =================================================================

        [Header("Layout")]
        [Tooltip("World Y position of the hit line / receptor row.")]
        [SerializeField] private float _hitLineY = -3f;

        [Tooltip("World X positions for each lane (Left, Down, Up, Right).")]
        [SerializeField] private float[] _lanePositions = { -1.5f, -0.5f, 0.5f, 1.5f };

        [Header("Scrolling")]
        [Tooltip("World units per beat. Controls visual scroll speed. Higher = more spread out.")]
        [SerializeField] private float _beatHeight = 2f;

        [Tooltip("How many beats ahead of current time to spawn notes.")]
        [SerializeField] private float _beatsShownInAdvance = 8f;

        [Tooltip("How many beats past the hit line before despawning (should exceed miss window).")]
        [SerializeField] private float _beatsDespawnBehind = 2f;

        [Header("Lane Colors")]
        [SerializeField] private Color[] _laneColors =
        {
            new Color(1f, 0.3f, 0.3f),   // Left  — red
            new Color(0.3f, 0.8f, 1f),    // Down  — cyan
            new Color(0.3f, 1f, 0.3f),    // Up    — green
            new Color(1f, 1f, 0.3f)       // Right — yellow
        };

        [Header("References")]
        [SerializeField] private NotePool _notePool;

        [Tooltip("Receptor sprites (one per lane). Assign in order: Left, Down, Up, Right.")]
        [SerializeField] private SpriteRenderer[] _receptors;

        [Header("Receptor Visuals")]
        [SerializeField] private Color _receptorIdleColor = new Color(0.3f, 0.3f, 0.3f);
        [SerializeField] private Color _receptorPressedColor = Color.white;

        // =================================================================
        // RUNTIME STATE
        // =================================================================

        /// <summary>The loaded chart currently being played.</summary>
        private LoadedChart _chart;

        /// <summary>Index of the next note in the chart to spawn.</summary>
        private int _nextSpawnIndex;

        /// <summary>Currently active (visible) notes on the highway.</summary>
        private readonly List<NoteView> _activeNotes = new();

        /// <summary>Notes pending removal this frame (avoids mutating during iteration).</summary>
        private readonly List<NoteView> _pendingDespawn = new();

        /// <summary>Cached Conductor reference.</summary>
        private Conductor _conductor;

        // =================================================================
        // PUBLIC — for hit detection and other consumers
        // =================================================================

        /// <summary>
        /// All currently visible notes. Hit detection iterates this
        /// to find the nearest unprocessed note per lane.
        /// </summary>
        public IReadOnlyList<NoteView> ActiveNotes => _activeNotes;

        /// <summary>
        /// World Y position of the hit line. Hit detection uses this
        /// to know where notes should be when they're "on time."
        /// </summary>
        public float HitLineY => _hitLineY;

        /// <summary>
        /// World units per beat. Used by hit detection to convert
        /// beat distances to visual positions if needed.
        /// </summary>
        public float BeatHeight => _beatHeight;

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Awake()
        {
            _conductor = Conductor.Instance;
        }

        private void Update()
        {
            if (_chart == null || !_conductor.IsPlaying || _conductor.IsPaused)
                return;

            float currentBeat = _conductor.SongPositionInBeats;

            SpawnUpcomingNotes(currentBeat);
            ScrollActiveNotes(currentBeat);
            DespawnPassedNotes(currentBeat);
        }

        // =================================================================
        // PUBLIC — chart loading
        // =================================================================

        /// <summary>
        /// Load a chart and prepare the highway for playback.
        /// Call before Conductor.Play().
        /// </summary>
        public void LoadChart(LoadedChart chart)
        {
            if (chart == null)
            {
                Debug.LogError("[NoteHighway] Cannot load null chart.");
                return;
            }

            // Clear any existing notes
            ClearAllNotes();

            _chart = chart;
            _nextSpawnIndex = 0;

            Debug.Log($"[NoteHighway] Chart loaded: {chart.SongName} — {chart.NoteCount} notes");
        }

        /// <summary>
        /// Clear all active notes and reset state.
        /// Called on chart load and when stopping playback.
        /// </summary>
        public void ClearAllNotes()
        {
            foreach (NoteView note in _activeNotes)
            {
                _notePool.Release(note);
            }

            _activeNotes.Clear();
            _pendingDespawn.Clear();
            _nextSpawnIndex = 0;
        }

        // =================================================================
        // PUBLIC — receptor feedback (called by input system)
        // =================================================================

        /// <summary>
        /// Flash a receptor to show the player pressed that lane.
        /// Call from the input system on key down.
        /// </summary>
        /// <param name="lane">Lane index (0-3).</param>
        /// <param name="pressed">True on press, false on release.</param>
        public void SetReceptorPressed(int lane, bool pressed)
        {
            if (_receptors == null || lane < 0 || lane >= _receptors.Length)
                return;

            if (_receptors[lane] != null)
            {
                _receptors[lane].color = pressed ? _receptorPressedColor : _receptorIdleColor;
            }
        }

        // =================================================================
        // SPAWNING
        // =================================================================

        /// <summary>
        /// Spawn notes that have entered the visible window.
        /// Iterates forward through the sorted chart from _nextSpawnIndex.
        /// </summary>
        private void SpawnUpcomingNotes(float currentBeat)
        {
            float spawnThreshold = currentBeat + _beatsShownInAdvance;

            while (_nextSpawnIndex < _chart.NoteCount)
            {
                NoteData noteData = _chart.Notes[_nextSpawnIndex];

                // Stop if the next note is beyond the visible window
                if (noteData.BeatPosition > spawnThreshold)
                    break;

                SpawnNote(noteData, _nextSpawnIndex, currentBeat);
                _nextSpawnIndex++;
            }
        }

        /// <summary>
        /// Spawn a single note from the pool and position it.
        /// </summary>
        private void SpawnNote(NoteData noteData, int noteIndex, float currentBeat)
        {
            NoteView note = _notePool.Get();

            // Lane color (clamped for safety)
            int lane = Mathf.Clamp(noteData.Lane, 0, _laneColors.Length - 1);
            Color color = _laneColors[lane];

            note.Setup(noteData, noteIndex, color, _beatHeight);
            PositionNote(note, currentBeat);

            _activeNotes.Add(note);
        }

        // =================================================================
        // SCROLLING
        // =================================================================

        /// <summary>
        /// Update the Y position of all active notes based on their
        /// beat distance from the current beat.
        /// </summary>
        private void ScrollActiveNotes(float currentBeat)
        {
            foreach (NoteView note in _activeNotes)
            {
                PositionNote(note, currentBeat);

                // Update hold note body while being held
                if (note.Data.Type == NoteType.Hold && note.IsBeingHeld)
                {
                    // Shrink the body as the hold progresses
                    float remaining = note.Data.EndBeatPosition - currentBeat;
                    remaining = Mathf.Max(0f, remaining);
                    note.UpdateHoldBody(remaining, _beatHeight);
                }
            }
        }

        /// <summary>
        /// Position a note in world space based on its beat distance
        /// from the current beat.
        /// 
        /// Notes at currentBeat sit exactly on the hit line.
        /// Notes in the future are above (positive Y offset).
        /// Notes in the past are below (negative Y offset).
        /// </summary>
        private void PositionNote(NoteView note, float currentBeat)
        {
            float distanceInBeats = note.Data.BeatPosition - currentBeat;
            float worldY = _hitLineY + (distanceInBeats * _beatHeight);

            int lane = Mathf.Clamp(note.Data.Lane, 0, _lanePositions.Length - 1);
            float worldX = _lanePositions[lane];

            note.transform.position = new Vector3(worldX, worldY, 0f);
        }

        // =================================================================
        // DESPAWNING
        // =================================================================

        /// <summary>
        /// Remove notes that have passed beyond the despawn window.
        /// Notes that haven't been hit are auto-marked as missed.
        /// </summary>
        private void DespawnPassedNotes(float currentBeat)
        {
            _pendingDespawn.Clear();

            float despawnBeat = currentBeat - _beatsDespawnBehind;

            foreach (NoteView note in _activeNotes)
            {
                // For hold notes, use the end beat position
                float relevantBeat = note.Data.Type == NoteType.Hold
                    ? note.Data.EndBeatPosition
                    : note.Data.BeatPosition;

                if (relevantBeat < despawnBeat)
                {
                    // Auto-miss if not yet processed
                    if (!note.IsProcessed)
                    {
                        note.IsMissed = true;
                        OnNoteMissed(note);
                    }

                    _pendingDespawn.Add(note);
                }
            }

            foreach (NoteView note in _pendingDespawn)
            {
                _activeNotes.Remove(note);
                _notePool.Release(note);
            }
        }

        /// <summary>
        /// Called when a note is auto-missed by passing the despawn window.
        /// Override point for the hit detection system to hook into.
        /// 
        /// TODO (PROTO-007): Fire NoteJudgedEvent with Miss judgment.
        /// </summary>
        private void OnNoteMissed(NoteView note)
        {
            Debug.Log($"[NoteHighway] Missed: {note.Data}");
        }

        // =================================================================
        // GIZMOS — visual debug for hit line and lane positions
        // =================================================================

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Draw hit line
            Gizmos.color = Color.red;
            float lineHalfWidth = 3f;
            Gizmos.DrawLine(
                new Vector3(-lineHalfWidth, _hitLineY, 0f),
                new Vector3(lineHalfWidth, _hitLineY, 0f));

            // Draw lane positions
            Gizmos.color = new Color(1f, 1f, 1f, 0.2f);

            if (_lanePositions != null)
            {
                foreach (float x in _lanePositions)
                {
                    Gizmos.DrawLine(
                        new Vector3(x, _hitLineY - 1f, 0f),
                        new Vector3(x, _hitLineY + 10f, 0f));
                }
            }
        }
#endif
    }
}
