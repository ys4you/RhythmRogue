using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Enemy-side auto-playing highway. Inherits shared spawning/scrolling from HighwayBase.
    /// 
    /// Adds enemy-specific functionality:
    ///   - Auto-hit: flashes receptors when notes reach the line (visual only)
    ///   - No player input, no hit detection, no judgment
    ///   - Notes are purely visual/musical feedback
    /// </summary>
    public class EnemyHighway : HighwayBase
    {
        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("Flash")]
        [SerializeField] private float _flashDuration = 0.08f;

        // =================================================================
        // STATE
        // =================================================================

        private List<StampedNote> _notes;
        private int _nextSpawnIndex;
        private readonly List<ActiveNote> _activeNotes = new(32);
        private readonly float[] _flashTimers = new float[4];
        private bool _isActive;

        // =================================================================
        // LIFECYCLE
        // =================================================================

        protected override void Awake()
        {
            base.Awake();
        }

        protected override string GetReceptorPrefix() => "EReceptor";

        private void Update()
        {
            if (!_isActive || _conductor == null || !_conductor.IsPlaying)
                return;

            float currentBeat = _conductor.SongPositionInBeats;

            SpawnUpcomingNotes(currentBeat);
            UpdateActiveNotes(currentBeat);
            UpdateReceptorFlash();
        }

        // =================================================================
        // PUBLIC
        // =================================================================

        /// <summary>
        /// Load notes for auto-play. Call before the song starts.
        /// Notes must be sorted by beat (ascending).
        /// </summary>
        public void LoadNotes(IReadOnlyList<StampedNote> notes)
        {
            _notes = new List<StampedNote>(notes);
            _nextSpawnIndex = 0;
            _isActive = true;
        }

        /// <summary>
        /// Clear all active notes and reset state.
        /// </summary>
        public void Clear()
        {
            for (int i = _activeNotes.Count - 1; i >= 0; i--)
            {
                ReturnNoteView(_activeNotes[i].View);
            }
            _activeNotes.Clear();

            _isActive = false;
            _nextSpawnIndex = 0;
        }

        // =================================================================
        // SPAWNING
        // =================================================================

        private void SpawnUpcomingNotes(float currentBeat)
        {
            if (_notes == null) return;

            float spawnThreshold = currentBeat + _beatsShownInAdvance;

            while (_nextSpawnIndex < _notes.Count
                   && _notes[_nextSpawnIndex].Beat <= spawnThreshold)
            {
                SpawnNote(_notes[_nextSpawnIndex], currentBeat);
                _nextSpawnIndex++;
            }
        }

        private void SpawnNote(StampedNote note, float currentBeat)
        {
            int lane = Mathf.Clamp(note.Lane, 0, 3);

            NoteType type = note.IsTap ? NoteType.Tap : NoteType.Hold;
            var noteData = new NoteData(note.Beat, lane, type, note.HoldBeats);

            NoteView view = SpawnNoteView(noteData, lane, currentBeat);

            _activeNotes.Add(new ActiveNote
            {
                View = view,
                Beat = note.Beat,
                Lane = lane,
                AutoHit = false
            });
        }

        // =================================================================
        // SCROLLING + AUTO-HIT
        // =================================================================

        private void UpdateActiveNotes(float currentBeat)
        {
            for (int i = _activeNotes.Count - 1; i >= 0; i--)
            {
                var active = _activeNotes[i];

                PositionNote(active.View, currentBeat);

                // Auto-hit: flash receptor when note reaches the line
                float distanceInBeats = active.Beat - currentBeat;
                if (!active.AutoHit && distanceInBeats <= 0f)
                {
                    active.AutoHit = true;
                    _activeNotes[i] = active;
                    FlashReceptor(active.Lane);
                }

                // Despawn
                if (distanceInBeats < -_beatsDespawnBehind)
                {
                    ReturnNoteView(active.View);
                    _activeNotes.RemoveAt(i);
                }
            }
        }

        // =================================================================
        // RECEPTOR FLASH
        // =================================================================

        private void FlashReceptor(int lane)
        {
            if (lane < 0 || lane >= 4) return;
            _flashTimers[lane] = _flashDuration;

            if (_receptors != null && lane < _receptors.Length && _receptors[lane] != null)
                _receptors[lane].sprite = _receptorPressedSprite ?? _receptorIdleSprite;
        }

        private void UpdateReceptorFlash()
        {
            for (int i = 0; i < 4; i++)
            {
                if (_flashTimers[i] <= 0f) continue;

                _flashTimers[i] -= Time.deltaTime;

                if (_flashTimers[i] <= 0f)
                {
                    _flashTimers[i] = 0f;
                    if (_receptors != null && i < _receptors.Length && _receptors[i] != null)
                        _receptors[i].sprite = _receptorIdleSprite;
                }
            }
        }

        // =================================================================
        // CLEANUP
        // =================================================================

        private void OnDestroy()
        {
            Clear();
        }

        // =================================================================
        // INNER TYPE
        // =================================================================

        private struct ActiveNote
        {
            public NoteView View;
            public float Beat;
            public int Lane;
            public bool AutoHit;
        }
    }
}