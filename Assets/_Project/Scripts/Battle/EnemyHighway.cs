using System;
using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Enemy-side auto-playing highway. Notes scroll, flash receptors on hit,
    /// and hold notes pin/shrink like the player highway.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyHighway : HighwayBase
    {
        [Header("Flash")]
        [SerializeField] private float _flashDuration = 0.08f;

        public event Action<int, float> OnAutoHit;

        private List<StampedNote> _notes;
        private int _nextSpawnIndex;
        private readonly List<ActiveNote> _activeNotes = new(32);
        private readonly float[] _flashTimers = new float[4];
        private bool _isActive;

        protected override void Awake() => base.Awake();
        protected override string GetReceptorPrefix() => "EReceptor";

        private void Update()
        {
            SyncScrollDirection();
            if (!_isActive || _conductor == null || !_conductor.IsPlaying) return;

            float currentBeat = _conductor.SongPositionInBeats;
            SpawnUpcomingNotes(currentBeat);
            UpdateActiveNotes(currentBeat);
            UpdateReceptorFlash();
        }

        public void LoadNotes(IReadOnlyList<StampedNote> notes)
        {
            _notes = new List<StampedNote>(notes);
            _nextSpawnIndex = 0;
            _isActive = true;
        }

        public void Clear()
        {
            for (int i = _activeNotes.Count - 1; i >= 0; i--) ReturnNoteView(_activeNotes[i].View);
            _activeNotes.Clear();
            _isActive = false;
            _nextSpawnIndex = 0;
        }

        private void SpawnUpcomingNotes(float currentBeat)
        {
            if (_notes == null) return;
            float spawnThreshold = currentBeat + SpawnAheadBeats;

            while (_nextSpawnIndex < _notes.Count && _notes[_nextSpawnIndex].Beat <= spawnThreshold)
            {
                StampedNote note = _notes[_nextSpawnIndex];
                int lane = Mathf.Clamp(note.Lane, 0, 3);
                NoteType type = note.IsTap ? NoteType.Tap : NoteType.Hold;
                var noteData = new NoteData(note.Beat, lane, type, note.HoldBeats);
                NoteView view = SpawnNoteView(noteData, lane, currentBeat);

                _activeNotes.Add(new ActiveNote
                {
                    View = view, Beat = note.Beat, EndBeat = noteData.EndBeatPosition,
                    Lane = lane, IsHold = type == NoteType.Hold, AutoHit = false, HoldActive = false
                });
                _nextSpawnIndex++;
            }
        }

        private void UpdateActiveNotes(float currentBeat)
        {
            for (int i = _activeNotes.Count - 1; i >= 0; i--)
            {
                var active = _activeNotes[i];
                float headDistance = active.Beat - currentBeat;

                if (!active.AutoHit && headDistance <= 0f)
                {
                    active.AutoHit = true;
                    FlashReceptor(active.Lane);
                    // Pass the sustain length so the enemy holds its sing pose for the whole
                    // slider; taps have EndBeat == Beat, so this is 0 and the pose uses the ease.
                    float holdSeconds = active.IsHold
                        ? Mathf.Max(0f, active.EndBeat - active.Beat) * _conductor.SecPerBeat
                        : 0f;
                    OnAutoHit?.Invoke(active.Lane, holdSeconds);
                    if (active.IsHold) active.HoldActive = true;
                    _activeNotes[i] = active;
                }

                if (active.HoldActive)
                {
                    // Pin head at receptor, shrink tail
                    int lane = Mathf.Clamp(active.Lane, 0, _laneX.Length - 1);
                    active.View.transform.position = new Vector3(_laneX[lane], _receptorY, 0f);
                    float remaining = Mathf.Max(0f, active.EndBeat - currentBeat);
                    active.View.UpdateHoldBody(remaining, EffectiveBeatHeight);

                    if (remaining <= 0f) { active.HoldActive = false; _activeNotes[i] = active; }
                }
                else if (!active.AutoHit)
                {
                    PositionNote(active.View, currentBeat);
                }
                else
                {
                    // Tap note that has already auto-hit: snap it exactly onto the receptor
                    // line. Without this the note keeps the position from its last pre-hit
                    // frame, which is a few pixels ABOVE the receptor (the frame before
                    // headDistance crossed zero), so the flash and the note never visually
                    // line up. Pinning to _receptorY makes the hit land dead-center.
                    int lane = Mathf.Clamp(active.Lane, 0, _laneX.Length - 1);
                    active.View.transform.position = new Vector3(_laneX[lane], _receptorY, 0f);
                }

                float despawnBeat = active.IsHold ? active.EndBeat : active.Beat;
                if (currentBeat - despawnBeat > _beatsDespawnBehind)
                {
                    ReturnNoteView(active.View);
                    _activeNotes.RemoveAt(i);
                }
            }
        }

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
                if (_flashTimers[i] <= 0f && _receptors != null && i < _receptors.Length && _receptors[i] != null)
                    _receptors[i].sprite = _receptorIdleSprite;
            }
        }

        private void OnDestroy() { Clear(); OnAutoHit = null; }

        /// <summary>
        /// Flip note travel direction live, re-placing every active note so a mid-song switch
        /// between upscroll and downscroll is seamless on the enemy highway too.
        /// </summary>
        public override void ApplyScrollDirection()
        {
            base.ApplyScrollDirection();
            if (_conductor == null) return;
            float currentBeat = _conductor.SongPositionInBeats;

            for (int i = 0; i < _activeNotes.Count; i++)
            {
                ActiveNote a = _activeNotes[i];
                a.View.ReorientHold(_appliedDownscroll);

                if (a.HoldActive)
                {
                    int lane = Mathf.Clamp(a.Lane, 0, _laneX.Length - 1);
                    a.View.transform.position = new Vector3(_laneX[lane], _receptorY, 0f);
                    a.View.UpdateHoldBody(Mathf.Max(0f, a.EndBeat - currentBeat), EffectiveBeatHeight);
                }
                else if (!a.AutoHit)
                {
                    PositionNote(a.View, currentBeat);
                    if (a.IsHold) a.View.UpdateHoldBody(a.View.Data.HoldDuration, EffectiveBeatHeight);
                }
                else
                {
                    int lane = Mathf.Clamp(a.Lane, 0, _laneX.Length - 1);
                    a.View.transform.position = new Vector3(_laneX[lane], _receptorY, 0f);
                }
            }
        }

        private struct ActiveNote
        {
            public NoteView View;
            public float Beat, EndBeat;
            public int Lane;
            public bool IsHold, AutoHit, HoldActive;
        }
    }
}
