using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// A short, hand-authored rhythm fragment: a sequence of notes carrying their own
    /// timing, lanes, and holds, spanning <see cref="lengthBeats"/> beats.
    ///
    /// This is the unit of authored feel. The assembler places whole fragments on a
    /// bar-aligned timeline and stamps their rhythm verbatim; it chooses when, which, and
    /// how dense, but never the individual note times. Contrast with the older LaneShape,
    /// which authored lanes only and let the onset detector decide every note time (the
    /// thing that read as robotic).
    ///
    /// Notes use <see cref="ShapeTag"/>, currently defined alongside LaneShape; that enum
    /// moves to its own file when LaneShape is retired.
    ///
    /// Create via: Assets > Create > RhythmRogue > Note Pattern
    /// </summary>
    [CreateAssetMenu(fileName = "New Note Pattern", menuName = "RhythmRogue/Note Pattern", order = 29)]
    public class NotePattern : ScriptableObject
    {
        [Serializable]
        public struct Note
        {
            [Tooltip("Beats from the start of the fragment.")]
            [Min(0f)] public float offsetBeats;

            [Tooltip("0 = Left, 1 = Down, 2 = Up, 3 = Right.")]
            [Range(0, 3)] public int lane;

            [Tooltip("Hold length in beats. 0 = tap note.")]
            [Min(0f)] public float holdBeats;

            public Note(float offsetBeats, int lane, float holdBeats = 0f)
            {
                this.offsetBeats = offsetBeats;
                this.lane = lane;
                this.holdBeats = holdBeats;
            }
        }

        [Header("Identity")]
        public string patternName;

        [Tooltip("Family group for repeat prevention. Falls back to the asset name when empty.")]
        public string familyId;

        [Header("Rhythm")]
        [Tooltip("Total length of the fragment in beats, usually a whole number of bars. " +
                 "The assembler reserves this much of the timeline per placement.")]
        [Min(0.25f)] public float lengthBeats = 4f;

        [Tooltip("The notes, in time order. Offsets are relative to the fragment start. " +
                 "Two notes sharing an offset on different lanes form a chord (jump).")]
        public List<Note> notes = new();

        [Header("Selection")]
        [Range(1, 10)] public int difficulty = 1;
        public ShapeTag tags = ShapeTag.None;
        [Min(0.1f)] public float weight = 1f;

        // --- Derived ---

        public int NoteCount => notes?.Count ?? 0;

        /// <summary>Notes per beat. The assembler matches this against a section's target density.</summary>
        public float Density => lengthBeats > 0f ? NoteCount / lengthBeats : 0f;

        public int StartLane => notes is { Count: > 0 } ? notes[0].lane : -1;
        public int EndLane => notes is { Count: > 0 } ? notes[^1].lane : -1;
        public string EffectiveFamily => !string.IsNullOrEmpty(familyId) ? familyId : name;

        public int TransitionDistance(int previousEndLane)
        {
            if (previousEndLane < 0 || StartLane < 0) return 0;
            return Mathf.Abs(StartLane - previousEndLane);
        }

        public bool HasTags(ShapeTag required) => required == ShapeTag.None || (tags & required) == required;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (lengthBeats < 0.25f) lengthBeats = 0.25f;
            if (notes == null) return;
            for (int i = 0; i < notes.Count; i++)
            {
                Note n = notes[i];
                n.lane = Mathf.Clamp(n.lane, 0, 3);
                if (n.offsetBeats < 0f) n.offsetBeats = 0f;
                if (n.holdBeats < 0f) n.holdBeats = 0f;
                notes[i] = n;
            }
        }
#endif
    }
}
