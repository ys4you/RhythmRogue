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
        [Header("Miss Timing")]
        [Tooltip("How long after a note's beat it stays hittable, in milliseconds. Should match the " +
                 "NoteMatcher hit window (110). Once a note is older than this without being hit it is " +
                 "judged a Miss immediately, so the combo break and damage land at the hit line instead " +
                 "of when the note finishes scrolling off (which read as misses registering too late).")]
        [SerializeField] private float _missWindowMs = 110f;

        private LoadedChart _chart;
        private List<NoteData> _assembledNotes;
        private bool _useAssembledNotes;
        private int _nextSpawnIndex;
        private readonly List<NoteView> _activeNotes = new();
        private readonly List<NoteView> _pendingDespawn = new();
        private readonly List<NoteView> _newlyMissed = new();

        public event Action<NoteView> OnNoteMissedEvent;
        public IReadOnlyList<NoteView> ActiveNotes => _activeNotes;
        public float HitLineY => _receptorY;
        public float BeatHeight => _beatHeight;

        // Hit window in beats at the current BPM. The ms window is a BPM-independent reaction time;
        // converting it each frame keeps the miss point a constant time past the line at any tempo.
        private float MissWindowBeats => (_conductor != null && _conductor.SecPerBeat > 0f)
            ? (_missWindowMs / 1000f) / _conductor.SecPerBeat
            : 0f;

        protected override void Awake() => base.Awake();
        protected override string GetReceptorPrefix() => "Receptor";

        private void Update()
        {
            SyncScrollDirection();
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
            _newlyMissed.Clear();
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
            float spawnThreshold = currentBeat + SpawnAheadBeats;
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
                // A hit tap note is about to be despawned this same frame; don't bother
                // repositioning it, so it can't visibly slide past the receptor for a frame.
                if (note.IsHit && !note.IsBeingHeld) continue;

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
            _newlyMissed.Clear();

            // A note is a confirmed miss the instant its hit window closes, MissWindowBeats past its
            // beat. That is far sooner than the despawn distance, so the combo break and the damage
            // land right at the hit line instead of ~2 beats later when the note is recycled (the old
            // behaviour, which read as "misses register too late"). The note keeps scrolling and is
            // recycled later by the despawn pass; only its judgment is brought forward to the line.
            float missBeat = currentBeat - MissWindowBeats;
            float despawnBeat = currentBeat - _beatsDespawnBehind;

            // Pass 1: classify only, fire NO events. A miss event can synchronously end the battle
            // (fatal miss -> player death -> EndBattle -> ClearAllNotes), which mutates _activeNotes;
            // firing inside this foreach is what threw "collection was modified". Events fire in pass
            // 2, recycling in pass 3.
            foreach (NoteView note in _activeNotes)
            {
                // A held note is owned by HoldTracker; leave it untouched.
                if (note.IsBeingHeld) continue;

                // Miss the moment the window closes without a hit, judged off the HEAD beat for taps
                // and holds alike (a hold cannot be started once its head is past the window).
                if (!note.IsProcessed && note.Data.BeatPosition < missBeat)
                {
                    note.IsMissed = true;
                    _newlyMissed.Add(note);
                }

                // Recycle a hit note at once (so it pops at the receptor, not past it), and any note
                // once it has scrolled the full despawn distance past the line. Holds measure that
                // from their end beat so the tail clears the screen first.
                float despawnRef = note.Data.Type == NoteType.Hold
                    ? note.Data.EndBeatPosition
                    : note.Data.BeatPosition;

                if ((note.IsHit && !note.IsBeingHeld) || despawnRef < despawnBeat)
                    _pendingDespawn.Add(note);
            }

            // Pass 2: fire the misses that fell due this frame. Index loop with a Count recheck so a
            // fatal miss that runs ClearAllNotes mid-loop (emptying the list) stops cleanly. Never
            // foreach here.
            for (int i = 0; i < _newlyMissed.Count; i++)
                OnNoteMissedEvent?.Invoke(_newlyMissed[i]);

            // Pass 3: remove from the active list and return to the pool. If a fatal miss already
            // tore the battle down, ClearAllNotes did this and both lists are now empty, so this is a
            // no-op and nothing is recycled twice.
            for (int i = 0; i < _pendingDespawn.Count; i++)
            {
                _activeNotes.Remove(_pendingDespawn[i]);
                ReturnNoteView(_pendingDespawn[i]);
            }

            _pendingDespawn.Clear();
            _newlyMissed.Clear();
        }

        private void OnDestroy() => OnNoteMissedEvent = null;

        /// <summary>
        /// Flip note travel direction live. Re-places every active note (and re-orients hold
        /// bodies) so switching upscroll/downscroll mid-song is seamless.
        /// </summary>
        public override void ApplyScrollDirection()
        {
            base.ApplyScrollDirection();
            if (_conductor == null) return;
            float currentBeat = _conductor.SongPositionInBeats;

            foreach (NoteView note in _activeNotes)
            {
                note.ReorientHold(_appliedDownscroll);
                if (note.IsHit && !note.IsBeingHeld) continue;

                if (note.IsBeingHeld)
                {
                    int lane = Mathf.Clamp(note.Data.Lane, 0, _laneX.Length - 1);
                    note.transform.position = new Vector3(_laneX[lane], _receptorY, 0f);
                    note.UpdateHoldBody(Mathf.Max(0f, note.Data.EndBeatPosition - currentBeat), EffectiveBeatHeight);
                }
                else
                {
                    PositionNote(note, currentBeat);
                    if (note.Data.Type == NoteType.Hold)
                        note.UpdateHoldBody(note.Data.HoldDuration, EffectiveBeatHeight);
                }
            }
        }
    }
}
