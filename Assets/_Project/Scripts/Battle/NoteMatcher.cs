using System;
using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Matches player input to the nearest unprocessed note within the hit window.
    /// Hold notes fire OnNoteHit but stay unprocessed so HoldTracker can manage them.
    /// </summary>
    [DisallowMultipleComponent]
    public class NoteMatcher : MonoBehaviour
    {
        [Header("Hit Window")]
        [SerializeField] private float _maxHitWindowMs = 110f;

        [Header("References")]
        [SerializeField] private InputHandler _inputHandler;
        [SerializeField] private NoteHighway _highway;

        public event Action<NoteMatchResult> OnNoteHit;

        private Conductor _conductor;

        private void Awake() => _conductor = Conductor.Instance;
        private void OnEnable() { if (_inputHandler != null) _inputHandler.OnLanePressed += HandleLanePressed; }
        private void OnDisable() { if (_inputHandler != null) _inputHandler.OnLanePressed -= HandleLanePressed; }

        private void HandleLanePressed(int lane)
        {
            if (!_conductor.IsPlaying || _conductor.IsPaused) return;

            float currentBeat = _conductor.SongPositionInBeats;
            float windowBeats = MsToBeats(_maxHitWindowMs);
            NoteView closest = FindClosestNote(lane, currentBeat, windowBeats);
            if (closest == null) return;

            float deltaBeat = currentBeat - closest.Data.BeatPosition;
            float deltaMs = deltaBeat * _conductor.SecPerBeat * 1000f;

            // Only mark tap notes as hit. Hold notes stay unprocessed
            // so the highway keeps them alive for HoldTracker.
            if (closest.Data.Type != NoteType.Hold)
                closest.IsHit = true;

            OnNoteHit?.Invoke(new NoteMatchResult(closest, deltaMs, lane));
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
                if (note.Data.Lane != lane || note.IsProcessed || note.IsBeingHeld) continue;

                float distance = Mathf.Abs(beatDist);
                if (distance > windowBeats) continue;
                if (distance < closestDistance) { closestDistance = distance; closest = note; }
            }
            return closest;
        }

        private float MsToBeats(float ms)
        {
            if (_conductor.SecPerBeat <= 0f) return 0f;
            return (ms / 1000f) / _conductor.SecPerBeat;
        }

        private void OnDestroy() => OnNoteHit = null;
    }
}
