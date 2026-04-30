using System;
using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Manages active hold notes after initial tap. Awards ticks on a beat grid,
    /// handles early release (partial credit, no combo reset) and completion.
    /// </summary>
    [DisallowMultipleComponent]
    public class HoldTracker : MonoBehaviour
    {
        [Header("Tick Settings")]
        [SerializeField] private float _tickIntervalBeats = 0.25f;

        [Header("References")]
        [SerializeField] private InputHandler _inputHandler;
        [SerializeField] private NoteMatcher _noteMatcher;
        [SerializeField] private NoteHighway _highway;

        public event Action<HoldState> OnHoldTick;
        public event Action<HoldResult> OnHoldFinished;

        private readonly HoldState[] _activeHolds = new HoldState[InputHandler.LaneCount];
        private Conductor _conductor;

        private void Awake() => _conductor = Conductor.Instance;

        private void OnEnable()
        {
            if (_noteMatcher != null) _noteMatcher.OnNoteHit += HandleNoteMatched;
            if (_inputHandler != null) _inputHandler.OnLaneReleased += HandleLaneReleased;
        }

        private void OnDisable()
        {
            if (_noteMatcher != null) _noteMatcher.OnNoteHit -= HandleNoteMatched;
            if (_inputHandler != null) _inputHandler.OnLaneReleased -= HandleLaneReleased;
            ClearAll();
        }

        private void Update()
        {
            if (!_conductor.IsPlaying || _conductor.IsPaused) return;
            float currentBeat = _conductor.SongPositionInBeats;

            for (int lane = 0; lane < InputHandler.LaneCount; lane++)
            {
                HoldState hold = _activeHolds[lane];
                if (hold == null || !hold.IsActive) continue;

                if (!_inputHandler.IsLaneHeld(lane)) { ReleaseHold(lane, false); continue; }

                while (hold.NextTickBeat <= currentBeat && hold.IsActive)
                {
                    hold.TicksHeld++;
                    hold.NextTickBeat += _tickIntervalBeats;
                    OnHoldTick?.Invoke(hold);

                    if (currentBeat >= hold.EndBeat) { CompleteHold(lane); break; }
                }

                if (hold.IsActive && currentBeat >= hold.EndBeat) CompleteHold(lane);
            }
        }

        private void HandleNoteMatched(NoteMatchResult result)
        {
            if (result.Note.Data.Type != NoteType.Hold) return;

            int lane = result.Lane;
            NoteView note = result.Note;
            float duration = note.Data.HoldDuration;
            int totalTicks = Mathf.Max(1, Mathf.FloorToInt(duration / _tickIntervalBeats));
            float firstTick = note.Data.BeatPosition + _tickIntervalBeats;

            if (_activeHolds[lane] != null && _activeHolds[lane].IsActive)
                ReleaseHold(lane, false);

            _activeHolds[lane] = new HoldState(note, lane, note.Data.EndBeatPosition, duration, totalTicks, firstTick);
            note.IsBeingHeld = true;
        }

        private void HandleLaneReleased(int lane)
        {
            if (lane < 0 || lane >= InputHandler.LaneCount) return;
            HoldState hold = _activeHolds[lane];
            if (hold == null || !hold.IsActive) return;

            // Within one tick of the end counts as complete
            float remaining = hold.EndBeat - _conductor.SongPositionInBeats;
            if (remaining <= _tickIntervalBeats) CompleteHold(lane);
            else ReleaseHold(lane, false);
        }

        private void ReleaseHold(int lane, bool completed)
        {
            HoldState hold = _activeHolds[lane];
            if (hold == null) return;

            hold.IsActive = false;
            hold.IsReleasedEarly = !completed;
            hold.Note.IsBeingHeld = false;

            OnHoldFinished?.Invoke(new HoldResult(hold.Note, lane, hold.TicksHeld, hold.TotalTicks, completed));
            _activeHolds[lane] = null;
        }

        private void CompleteHold(int lane)
        {
            HoldState hold = _activeHolds[lane];
            if (hold == null) return;

            hold.TicksHeld = hold.TotalTicks;
            hold.IsCompleted = true;
            hold.Note.IsBeingHeld = false;
            hold.Note.IsHit = true;
            ReleaseHold(lane, true);
        }

        public void ClearAll()
        {
            for (int i = 0; i < _activeHolds.Length; i++)
            {
                if (_activeHolds[i] != null)
                {
                    _activeHolds[i].IsActive = false;
                    if (_activeHolds[i].Note != null) _activeHolds[i].Note.IsBeingHeld = false;
                    _activeHolds[i] = null;
                }
            }
        }

        public bool HasActiveHold(int lane) => lane >= 0 && lane < InputHandler.LaneCount && _activeHolds[lane] is { IsActive: true };
        public HoldState GetActiveHold(int lane) => lane >= 0 && lane < InputHandler.LaneCount ? _activeHolds[lane] : null;

        private void OnDestroy() { OnHoldTick = null; OnHoldFinished = null; }
    }
}
