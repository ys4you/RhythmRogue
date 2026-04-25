using System;
using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Matches player input to notes on the highway.
    /// 
    /// When a lane is pressed, searches the highway's active notes for
    /// the nearest unprocessed note in that lane within the maximum
    /// hit window. If found, calculates the timing delta and fires
    /// OnNoteHit for the judgment system to evaluate.
    /// 
    /// Hold notes: the initial tap fires OnNoteHit but does NOT set
    /// IsHit. The HoldTracker takes ownership and sets IsHit only
    /// when the hold is completed. This prevents the despawn logic
    /// from removing the note while the player is still holding.
    /// </summary>
    public class NoteMatcher : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("Hit Window")]
        [Tooltip("Maximum timing window in milliseconds. Notes beyond this are ignored. " +
                 "Matches the Bad window from the GDD (110ms).")]
        [SerializeField] private float _maxHitWindowMs = 110f;

        [Header("References")]
        [SerializeField] private InputHandler _inputHandler;
        [SerializeField] private NoteHighway _highway;

        // =================================================================
        // EVENTS
        // =================================================================

        /// <summary>
        /// Fired when a player input successfully matches a note.
        /// The judgment system subscribes to evaluate Perfect/Good/Bad.
        /// The HoldTracker subscribes to begin tracking hold notes.
        /// </summary>
        public event Action<NoteMatchResult> OnNoteHit;

        // =================================================================
        // STATE
        // =================================================================

        private Conductor _conductor;

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Awake()
        {
            _conductor = Conductor.Instance;
        }

        private void OnEnable()
        {
            if (_inputHandler != null)
                _inputHandler.OnLanePressed += HandleLanePressed;
        }

        private void OnDisable()
        {
            if (_inputHandler != null)
                _inputHandler.OnLanePressed -= HandleLanePressed;
        }

        // =================================================================
        // MATCHING
        // =================================================================

        private void HandleLanePressed(int lane)
        {
            if (!_conductor.IsPlaying || _conductor.IsPaused)
                return;

            float currentBeat = _conductor.SongPositionInBeats;
            float windowBeats = MsToBeats(_maxHitWindowMs);

            NoteView closest = FindClosestNote(lane, currentBeat, windowBeats);

            if (closest == null)
                return;

            float deltaBeat = currentBeat - closest.Data.BeatPosition;
            float deltaMs = deltaBeat * _conductor.SecPerBeat * 1000f;

            // Only mark tap notes as hit immediately.
            // Hold notes stay "unprocessed" so the highway keeps them alive.
            // HoldTracker sets IsHit when the hold completes.
            if (closest.Data.Type != NoteType.Hold)
            {
                closest.IsHit = true;
            }

            var result = new NoteMatchResult(closest, deltaMs, lane);
            OnNoteHit?.Invoke(result);
        }

        private NoteView FindClosestNote(int lane, float currentBeat, float windowBeats)
        {
            IReadOnlyList<NoteView> activeNotes = _highway.ActiveNotes;
            NoteView closest = null;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < activeNotes.Count; i++)
            {
                NoteView note = activeNotes[i];
                float beatDist = note.Data.BeatPosition - currentBeat;

                if (beatDist > windowBeats) break;

                if (note.Data.Lane != lane || note.IsProcessed) continue;

                // Also skip holds that are already being tracked
                if (note.IsBeingHeld) continue;

                float distance = Mathf.Abs(beatDist);
                if (distance > windowBeats) continue;

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = note;
                }
            }
            return closest;
        }

        // =================================================================
        // HELPERS
        // =================================================================

        private float MsToBeats(float ms)
        {
            if (_conductor.SecPerBeat <= 0f) return 0f;
            float seconds = ms / 1000f;
            return seconds / _conductor.SecPerBeat;
        }

        // =================================================================
        // CLEANUP
        // =================================================================

        private void OnDestroy()
        {
            OnNoteHit = null;
        }
    }
}
