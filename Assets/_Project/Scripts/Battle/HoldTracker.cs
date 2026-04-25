using System;
using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Manages active hold notes after their initial tap is matched.
    /// 
    /// Lifecycle of a hold note:
    ///   1. NoteMatcher matches the hold start like a normal tap → fires OnNoteHit
    ///   2. HoldTracker receives the match, creates a HoldState, sets IsBeingHeld
    ///   3. Each frame: checks if the lane is still held via InputHandler.IsLaneHeld
    ///   4. Awards ticks as the Conductor crosses tick boundaries (quarter-beat grid)
    ///   5. On release: fires OnHoldFinished with partial credit, no combo reset
    ///   6. On completion: fires OnHoldFinished with full credit
    /// 
    /// Tick alignment:
    ///   Ticks are spaced at a configurable interval (default: 0.25 beats = 16th notes).
    ///   This aligns scoring to the musical grid so holds feel rhythmic.
    /// 
    /// Combo interaction:
    ///   - Hold START increments combo (handled by judgment system, not here)
    ///   - Hold TICKS do NOT increment combo
    ///   - Early release does NOT reset combo
    ///   - Missing the hold start entirely resets combo (handled by highway auto-miss)
    /// 
    /// SOLID breakdown:
    /// - S: Only tracks hold state and fires events. No scoring math.
    /// - O: New hold behaviors (e.g. wiggle holds) extend, not modify.
    /// - L: Consumers see OnHoldTick/OnHoldFinished events.
    /// - I: Focused events for tick and finish.
    /// - D: Depends on IConductor and InputHandler abstractions.
    /// </summary>
    [DisallowMultipleComponent]
    public class HoldTracker : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("Tick Settings")]
        [Tooltip("Beats between hold score ticks. 0.25 = every 16th note, " +
                 "0.5 = every 8th note, 1.0 = every quarter note.")]
        [SerializeField] private float _tickIntervalBeats = 0.25f;

        [Header("References")]
        [SerializeField] private InputHandler _inputHandler;
        [SerializeField] private NoteMatcher _noteMatcher;
        [SerializeField] private NoteHighway _highway;

        // =================================================================
        // EVENTS
        // =================================================================

        /// <summary>
        /// Fired each time a hold tick is awarded.
        /// Parameters: HoldState (current state of this hold).
        /// Consumers: damage system (apply tick damage), UI (update hold visual).
        /// </summary>
        public event Action<HoldState> OnHoldTick;

        /// <summary>
        /// Fired when a hold ends (completed or released early).
        /// Consumers: scoring system, UI feedback (burst on complete, fade on early).
        /// </summary>
        public event Action<HoldResult> OnHoldFinished;

        // =================================================================
        // STATE
        // =================================================================

        /// <summary>Active holds, one per lane max.</summary>
        private readonly HoldState[] _activeHolds = new HoldState[InputHandler.LaneCount];

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
            if (_noteMatcher != null)
                _noteMatcher.OnNoteHit += HandleNoteMatched;

            if (_inputHandler != null)
                _inputHandler.OnLaneReleased += HandleLaneReleased;
        }

        private void OnDisable()
        {
            if (_noteMatcher != null)
                _noteMatcher.OnNoteHit -= HandleNoteMatched;

            if (_inputHandler != null)
                _inputHandler.OnLaneReleased -= HandleLaneReleased;

            ClearAll();
        }

        private void Update()
        {
            if (!_conductor.IsPlaying || _conductor.IsPaused)
                return;

            float currentBeat = _conductor.SongPositionInBeats;

            for (int lane = 0; lane < InputHandler.LaneCount; lane++)
            {
                HoldState hold = _activeHolds[lane];
                if (hold == null || !hold.IsActive)
                    continue;

                // Check if the player is still holding the key
                if (!_inputHandler.IsLaneHeld(lane))
                {
                    // Released — this shouldn't normally happen here because
                    // OnLaneReleased fires first, but safety net
                    ReleaseHold(lane, false);
                    continue;
                }

                // Award ticks as we cross tick boundaries
                while (hold.NextTickBeat <= currentBeat && hold.IsActive)
                {
                    hold.TicksHeld++;
                    hold.NextTickBeat += _tickIntervalBeats;

                    OnHoldTick?.Invoke(hold);

                    // Check for completion
                    if (currentBeat >= hold.EndBeat)
                    {
                        CompleteHold(lane);
                        break;
                    }
                }

                // Also check completion even between ticks
                if (hold.IsActive && currentBeat >= hold.EndBeat)
                {
                    CompleteHold(lane);
                }
            }
        }

        // =================================================================
        // HOLD LIFECYCLE
        // =================================================================

        /// <summary>
        /// Called when NoteMatcher matches a note. If it's a hold note,
        /// start tracking it.
        /// </summary>
        private void HandleNoteMatched(NoteMatchResult result)
        {
            if (result.Note.Data.Type != NoteType.Hold)
                return;

            int lane = result.Lane;
            NoteView note = result.Note;

            // Calculate tick count for this hold
            float duration = note.Data.HoldDuration;
            int totalTicks = Mathf.Max(1, Mathf.FloorToInt(duration / _tickIntervalBeats));

            // First tick is at the start beat + one interval
            float firstTick = note.Data.BeatPosition + _tickIntervalBeats;

            var holdState = new HoldState(
                note, lane, note.Data.EndBeatPosition,
                duration, totalTicks, firstTick);

            // If there's already an active hold on this lane, release it
            if (_activeHolds[lane] != null && _activeHolds[lane].IsActive)
            {
                ReleaseHold(lane, false);
            }

            _activeHolds[lane] = holdState;

            // Set visual state on the note
            note.IsBeingHeld = true;
        }

        /// <summary>
        /// Called when the player releases a lane key.
        /// If there's an active hold on that lane, end it with partial credit.
        /// </summary>
        private void HandleLaneReleased(int lane)
        {
            if (lane < 0 || lane >= InputHandler.LaneCount)
                return;

            HoldState hold = _activeHolds[lane];
            if (hold == null || !hold.IsActive)
                return;

            // Check if we're close enough to the end to count as complete
            float currentBeat = _conductor.SongPositionInBeats;
            float remainingBeats = hold.EndBeat - currentBeat;

            // If within one tick of the end, count as complete
            if (remainingBeats <= _tickIntervalBeats)
            {
                CompleteHold(lane);
            }
            else
            {
                ReleaseHold(lane, false);
            }
        }

        /// <summary>
        /// End a hold with partial credit (early release).
        /// Does NOT reset combo per GDD design.
        /// </summary>
        private void ReleaseHold(int lane, bool completed)
        {
            HoldState hold = _activeHolds[lane];
            if (hold == null) return;

            hold.IsActive = false;
            hold.IsReleasedEarly = !completed;
            hold.Note.IsBeingHeld = false;

            var result = new HoldResult(
                hold.Note, lane,
                hold.TicksHeld, hold.TotalTicks,
                completed);

            OnHoldFinished?.Invoke(result);

            _activeHolds[lane] = null;
        }

        /// <summary>
        /// End a hold with full credit (held to completion).
        /// </summary>
        private void CompleteHold(int lane)
        {
            HoldState hold = _activeHolds[lane];
            if (hold == null) return;

            // Award any remaining ticks
            hold.TicksHeld = hold.TotalTicks;
            hold.IsCompleted = true;

            hold.Note.IsBeingHeld = false;
            hold.Note.IsHit = true;

            ReleaseHold(lane, true);
        }

        /// <summary>
        /// Clear all active holds. Called on stop/reset.
        /// </summary>
        public void ClearAll()
        {
            for (int i = 0; i < _activeHolds.Length; i++)
            {
                if (_activeHolds[i] != null)
                {
                    _activeHolds[i].IsActive = false;
                    if (_activeHolds[i].Note != null)
                        _activeHolds[i].Note.IsBeingHeld = false;
                    _activeHolds[i] = null;
                }
            }
        }

        // =================================================================
        // PUBLIC QUERIES
        // =================================================================

        /// <summary>
        /// Check if a lane has an active hold in progress.
        /// </summary>
        public bool HasActiveHold(int lane)
        {
            if (lane < 0 || lane >= InputHandler.LaneCount) return false;
            return _activeHolds[lane] != null && _activeHolds[lane].IsActive;
        }

        /// <summary>
        /// Get the active hold state for a lane, or null if none.
        /// </summary>
        public HoldState GetActiveHold(int lane)
        {
            if (lane < 0 || lane >= InputHandler.LaneCount) return null;
            return _activeHolds[lane];
        }

        // =================================================================
        // CLEANUP
        // =================================================================

        private void OnDestroy()
        {
            OnHoldTick = null;
            OnHoldFinished = null;
        }
    }
}
