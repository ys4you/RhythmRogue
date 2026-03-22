using System;
using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Core;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Matches player input to notes on the highway.
    /// 
    /// When a lane is pressed, searches the highway's active notes for
    /// the nearest unprocessed note in that lane within the maximum
    /// hit window (±110ms Bad window). If found, calculates the timing
    /// delta using AudioSettings.dspTime (same clock as the Conductor)
    /// and fires OnNoteHit for the judgment system to evaluate.
    /// 
    /// If no note is within range, the input is silently ignored —
    /// no ghost miss penalty, matching the GDD's design.
    /// 
    /// Timing precision:
    ///   Input time is captured from AudioSettings.dspTime at the moment
    ///   of the key press. Since Input.GetKeyDown is polled per-frame,
    ///   precision is ~16.6ms at 60 FPS, which is within the ±35ms
    ///   Perfect window. Post-prototype can switch to Unity Input System
    ///   for event-based timing.
    /// 
    /// SOLID breakdown:
    /// - S: Only matches input to notes and calculates timing. No judgment logic.
    /// - O: New note types (PROTO-006 holds) hook in via the same match pipeline.
    /// - L: Consumers of OnNoteHit don't know how matching works internally.
    /// - I: One event out (OnNoteHit), minimal public surface.
    /// - D: Depends on IConductor and NoteHighway abstractions.
    /// </summary>
    public class NoteMatcher : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("Hit Window")]
        [Tooltip("Maximum timing window in milliseconds. Notes beyond this are ignored. " +
                 "Matches the Bad window from the GDD (±110ms).")]
        [SerializeField] private float _maxHitWindowMs = 110f;

        [Header("References")]
        [SerializeField] private InputHandler _inputHandler;
        [SerializeField] private NoteHighway _highway;

        // =================================================================
        // EVENTS
        // =================================================================

        /// <summary>
        /// Fired when a player input successfully matches a note.
        /// The judgment system (PROTO-007) subscribes to this to
        /// evaluate Perfect/Good/Bad based on the offset.
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

        /// <summary>
        /// Called when a lane key is pressed. Finds the nearest unprocessed
        /// note in that lane, calculates timing offset, and fires OnNoteHit.
        /// </summary>
        private void HandleLanePressed(int lane)
        {
            if (!_conductor.IsPlaying || _conductor.IsPaused)
                return;

            // Current time in beats, using the audio clock
            float currentBeat = _conductor.SongPositionInBeats;

            // Convert max window from ms to beats
            float windowBeats = MsToBeats(_maxHitWindowMs);

            // Find the nearest unprocessed note in this lane within the window
            NoteView closest = FindClosestNote(lane, currentBeat, windowBeats);

            if (closest == null)
                return; // No note in range — ignore input, no ghost penalty

            // Calculate timing offset in milliseconds
            float deltaBeat = currentBeat - closest.Data.BeatPosition;
            float deltaMs = deltaBeat * _conductor.SecPerBeat * 1000f;

            // Mark as hit to prevent double-matching
            closest.IsHit = true;

            // Fire result for judgment system
            var result = new NoteMatchResult(closest, deltaMs, lane);
            OnNoteHit?.Invoke(result);
        }

        /// <summary>
        /// Search the highway's active notes for the closest unprocessed
        /// note in the given lane within ±windowBeats of currentBeat.
        /// 
        /// If multiple notes are in range (unlikely but possible with
        /// dense charts), picks the one closest to currentBeat.
        /// </summary>
        private NoteView FindClosestNote(int lane, float currentBeat, float windowBeats)
        {
            IReadOnlyList<NoteView> activeNotes = _highway.ActiveNotes;
            NoteView closest = null;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < activeNotes.Count; i++)
            {
                NoteView note = activeNotes[i];
                float beatDist = note.Data.BeatPosition - currentBeat;
                
                // Notes are sorted — if we're past the window, stop
                if (beatDist > windowBeats) break;
                
                if (note.Data.Lane != lane || note.IsProcessed) continue;
                
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

        /// <summary>
        /// Convert a millisecond timing window to beats at the current BPM.
        /// </summary>
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