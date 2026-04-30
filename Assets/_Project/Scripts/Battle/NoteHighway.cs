using System;
using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;
using RhythmRogue.Util;

namespace RhythmRogue.Battle
{
    [DisallowMultipleComponent]
    public class NoteHighway : HighwayBase
    {
        private LoadedChart _chart;
        private List<NoteData> _assembledNotes;
        private bool _useAssembledNotes;
        private int _nextSpawnIndex;
        private readonly List<NoteView> _activeNotes = new();
        private readonly List<NoteView> _pendingDespawn = new();

        public event Action<NoteView> OnNoteMissedEvent;
        public IReadOnlyList<NoteView> ActiveNotes => _activeNotes;
        public float HitLineY => _receptorY;
        public float BeatHeight => _beatHeight;

        protected override void Awake() => base.Awake();
        protected override string GetReceptorPrefix() => "Receptor";

        private void Update()
        {
            if (!_conductor.IsPlaying || _conductor.IsPaused) return;
            if (_chart == null && !_useAssembledNotes) return;

            float currentBeat = _conductor.SongPositionInBeats;
            SpawnUpcomingNotes(currentBeat);
            ScrollActiveNotes(currentBeat);
            DespawnPassedNotes(currentBeat);
        }

        public void LoadChart(LoadedChart chart)
        {
            if (chart == null) { GameLog.Error("[NoteHighway] Cannot load null chart."); return; }
            ClearAllNotes();
            _chart = chart;
            _assembledNotes = null;
            _useAssembledNotes = false;
            _nextSpawnIndex = 0;
            GameLog.Info($"[NoteHighway] Chart loaded: {chart.SongName} - {chart.NoteCount} notes");
        }

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
            foreach (NoteView note in _activeNotes) ReturnNoteView(note);
            _activeNotes.Clear();
            _pendingDespawn.Clear();
            _nextSpawnIndex = 0;
        }

        public void SetReceptorPressed(int lane, bool pressed)
        {
            if (_receptors == null || lane < 0 || lane >= _receptors.Length) return;
            if (_receptors[lane] != null)
                _receptors[lane].sprite = pressed ? _receptorPressedSprite : _receptorIdleSprite;
        }

        private void SpawnUpcomingNotes(float currentBeat)
        {
            float spawnThreshold = currentBeat + _beatsShownInAdvance;
            int noteCount = _useAssembledNotes ? (_assembledNotes?.Count ?? 0) : (_chart?.NoteCount ?? 0);

            while (_nextSpawnIndex < noteCount)
            {
                NoteData noteData = _useAssembledNotes ? _assembledNotes[_nextSpawnIndex] : _chart.Notes[_nextSpawnIndex];
                if (noteData.BeatPosition > spawnThreshold) break;

                _activeNotes.Add(SpawnNoteView(noteData, _nextSpawnIndex, currentBeat));
                _nextSpawnIndex++;
            }
        }

        private void ScrollActiveNotes(float currentBeat)
        {
            foreach (NoteView note in _activeNotes)
            {
                if (note.IsBeingHeld)
                {
                    // Pin head at receptor, shrink tail toward it
                    int lane = Mathf.Clamp(note.Data.Lane, 0, _laneX.Length - 1);
                    note.transform.position = new Vector3(_laneX[lane], _receptorY, 0f);

                    float remaining = Mathf.Max(0f, note.Data.EndBeatPosition - currentBeat);
                    note.UpdateHoldBody(remaining, EffectiveBeatHeight);
                }
                else
                {
                    PositionNote(note, currentBeat);
                }
            }
        }

        private void DespawnPassedNotes(float currentBeat)
        {
            _pendingDespawn.Clear();
            float despawnBeat = currentBeat - _beatsDespawnBehind;

            foreach (NoteView note in _activeNotes)
            {
                if (note.IsBeingHeld) continue;

                // Hold notes despawn based on end beat, not head beat
                float relevantBeat = note.Data.Type == NoteType.Hold
                    ? note.Data.EndBeatPosition
                    : note.Data.BeatPosition;

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

        private void OnDestroy() => OnNoteMissedEvent = null;
    }
}
