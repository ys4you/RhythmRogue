using System;
using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;
using RhythmRogue.Util;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Player-side note highway. Inherits shared spawning/scrolling from HighwayBase.
    /// 
    /// Adds player-specific functionality:
    ///   - Receptor press feedback (swap idle/pressed sprites on input)
    ///   - Miss detection (fires OnNoteMissedEvent when notes pass the despawn window)
    ///   - Dual chart source: LoadChart (legacy JSON) or LoadNotes (pattern assembler)
    ///   - ActiveNotes list exposed for hit detection by NoteMatcher/JudgmentSystem
    ///   - Hold note pinning: held notes stay at the receptor while the tail shrinks
    /// </summary>
    [DisallowMultipleComponent]

    public class NoteHighway : HighwayBase
    {
        // =================================================================
        // RUNTIME STATE
        // =================================================================

        private LoadedChart _chart;
        private List<NoteData> _assembledNotes;
        private bool _useAssembledNotes;
        private int _nextSpawnIndex;
        private readonly List<NoteView> _activeNotes = new();
        private readonly List<NoteView> _pendingDespawn = new();

        // =================================================================
        // EVENTS
        // =================================================================

        /// <summary>
        /// Fired when a note is auto-missed by passing the despawn window.
        /// JudgmentSystem subscribes to this.
        /// </summary>
        public event Action<NoteView> OnNoteMissedEvent;

        // =================================================================
        // PUBLIC PROPERTIES
        // =================================================================

        /// <summary>All currently visible notes. Hit detection iterates this.</summary>
        public IReadOnlyList<NoteView> ActiveNotes => _activeNotes;

        /// <summary>World Y of the hit line.</summary>
        public float HitLineY => _receptorY;

        /// <summary>World units per beat.</summary>
        public float BeatHeight => _beatHeight;

        // =================================================================
        // LIFECYCLE
        // =================================================================

        protected override void Awake()
        {
            base.Awake();
        }

        protected override string GetReceptorPrefix() => "Receptor";

        private void Update()
        {
            if (!_conductor.IsPlaying || _conductor.IsPaused)
                return;

            if (_chart == null && !_useAssembledNotes)
                return;

            float currentBeat = _conductor.SongPositionInBeats;

            SpawnUpcomingNotes(currentBeat);
            ScrollActiveNotes(currentBeat);
            DespawnPassedNotes(currentBeat);
        }

        // =================================================================
        // PUBLIC - chart loading (legacy)
        // =================================================================

        public void LoadChart(LoadedChart chart)
        {
            if (chart == null)
            {
                GameLog.Error("[NoteHighway] Cannot load null chart.");
                return;
            }

            ClearAllNotes();

            _chart = chart;
            _assembledNotes = null;
            _useAssembledNotes = false;
            _nextSpawnIndex = 0;

            GameLog.Info($"[NoteHighway] Chart loaded: {chart.SongName} - {chart.NoteCount} notes");
        }

        // =================================================================
        // PUBLIC - chart loading (pattern assembler)
        // =================================================================

        public void LoadNotes(IReadOnlyList<StampedNote> stampedNotes)
        {
            if (stampedNotes == null || stampedNotes.Count == 0)
            {
                GameLog.Warn("[NoteHighway] LoadNotes called with empty note list.");
                ClearAllNotes();
                return;
            }

            ClearAllNotes();

            _chart = null;
            _assembledNotes = new List<NoteData>(stampedNotes.Count);

            for (int i = 0; i < stampedNotes.Count; i++)
            {
                StampedNote s = stampedNotes[i];
                NoteType type = s.IsTap ? NoteType.Tap : NoteType.Hold;
                _assembledNotes.Add(new NoteData(s.Beat, s.Lane, type, s.HoldBeats));
            }

            _useAssembledNotes = true;
            _nextSpawnIndex = 0;

            GameLog.Info($"[NoteHighway] Assembled notes loaded: {_assembledNotes.Count} notes");
        }

        public void ClearAllNotes()
        {
            foreach (NoteView note in _activeNotes)
            {
                ReturnNoteView(note);
            }

            _activeNotes.Clear();
            _pendingDespawn.Clear();
            _nextSpawnIndex = 0;
        }

        // =================================================================
        // PUBLIC - receptor feedback (called by input system)
        // =================================================================

        public void SetReceptorPressed(int lane, bool pressed)
        {
            if (_receptors == null || lane < 0 || lane >= _receptors.Length)
                return;

            if (_receptors[lane] != null)
            {
                _receptors[lane].sprite = pressed ? _receptorPressedSprite : _receptorIdleSprite;
            }
        }

        // =================================================================
        // SPAWNING
        // =================================================================

        private void SpawnUpcomingNotes(float currentBeat)
        {
            float spawnThreshold = currentBeat + _beatsShownInAdvance;
            int noteCount = GetNoteCount();

            while (_nextSpawnIndex < noteCount)
            {
                NoteData noteData = GetNoteAt(_nextSpawnIndex);

                if (noteData.BeatPosition > spawnThreshold)
                    break;

                NoteView view = SpawnNoteView(noteData, _nextSpawnIndex, currentBeat);
                _activeNotes.Add(view);
                _nextSpawnIndex++;
            }
        }

        // =================================================================
        // NOTE ACCESS
        // =================================================================

        private int GetNoteCount()
        {
            if (_useAssembledNotes)
                return _assembledNotes?.Count ?? 0;

            return _chart?.NoteCount ?? 0;
        }

        private NoteData GetNoteAt(int index)
        {
            if (_useAssembledNotes)
                return _assembledNotes[index];

            return _chart.Notes[index];
        }

        // =================================================================
        // SCROLLING
        // =================================================================

        private void ScrollActiveNotes(float currentBeat)
        {
            foreach (NoteView note in _activeNotes)
            {
                if (note.IsBeingHeld)
                {
                    // Pin the head at the receptor position while held.
                    // The head stays on the hit line, the tail shrinks toward it.
                    int lane = Mathf.Clamp(note.Data.Lane, 0, _laneX.Length - 1);
                    note.transform.position = new Vector3(_laneX[lane], _receptorY, 0f);

                    float remaining = note.Data.EndBeatPosition - currentBeat;
                    remaining = Mathf.Max(0f, remaining);
                    note.UpdateHoldBody(remaining, EffectiveBeatHeight);
                }
                else
                {
                    // Normal scrolling for unheld notes
                    PositionNote(note, currentBeat);
                }
            }
        }

        // =================================================================
        // DESPAWNING
        // =================================================================

        private void DespawnPassedNotes(float currentBeat)
        {
            _pendingDespawn.Clear();
            float despawnBeat = currentBeat - _beatsDespawnBehind;

            foreach (NoteView note in _activeNotes)
            {
                // Never despawn a note that is actively being held
                if (note.IsBeingHeld) continue;

                float relevantBeat;

                if (note.Data.Type == NoteType.Hold)
                {
                    // Hold notes despawn based on their END beat,
                    // not the head beat. This keeps them alive for
                    // the full hold duration even if missed.
                    relevantBeat = note.Data.EndBeatPosition;
                }
                else
                {
                    relevantBeat = note.Data.BeatPosition;
                }

                if (relevantBeat < despawnBeat)
                {
                    if (!note.IsProcessed)
                    {
                        note.IsMissed = true;
                        OnNoteMissedEvent?.Invoke(note);
                    }

                    _pendingDespawn.Add(note);
                }
            }

            foreach (NoteView note in _pendingDespawn)
            {
                _activeNotes.Remove(note);
                ReturnNoteView(note);
            }
        }

        // =================================================================
        // CLEANUP
        // =================================================================

        private void OnDestroy()
        {
            OnNoteMissedEvent = null;
        }
    }
}
