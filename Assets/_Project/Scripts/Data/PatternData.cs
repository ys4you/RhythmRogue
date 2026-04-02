using System.Collections.Generic;
using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// A hand-authored rhythm pattern — a short sequence of notes
    /// (typically 1–4 bars) designed to be musical and fun to play.
    /// 
    /// Patterns are BPM-agnostic: all beat offsets are relative to
    /// the pattern start. The same pattern works at any tempo because
    /// the Conductor scales timing automatically.
    /// 
    /// The ChartAssembler picks patterns from a PatternLibrary based
    /// on difficulty, tags, and the run seed, then stamps them into
    /// absolute beat positions to produce a BattleChart.
    /// 
    /// Create via: Assets → Create → RhythmRogue → Pattern Data
    /// </summary>
    [CreateAssetMenu(
        fileName = "New Pattern",
        menuName = "RhythmRogue/Pattern Data",
        order = 20)]
    public class PatternData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Human-readable name for this pattern (e.g. 'quarter_basic', 'eighth_stream_lr').")]
        public string patternName;

        [Header("Notes")]
        [Tooltip("The notes in this pattern. Beat offsets are relative to pattern start.")]
        public List<PatternNote> notes = new();

        [Header("Metadata")]
        [Tooltip("Duration of this pattern in beats. Must be >= the last note's beat offset.")]
        [Min(1f)]
        public float durationBeats = 4f;

        [Tooltip("Difficulty tier (1 = easiest, 10 = hardest). Used by the assembler to match patterns to encounter difficulty.")]
        [Range(1, 10)]
        public int difficulty = 1;

        [Tooltip("Tags describing this pattern's character. Used for filtering during assembly.")]
        public PatternTag tags = PatternTag.None;

        [Tooltip("Selection weight — higher = more likely to be picked. Default 1.")]
        [Min(0.1f)]
        public float weight = 1f;

        /// <summary>Number of notes in this pattern.</summary>
        public int NoteCount => notes?.Count ?? 0;

        /// <summary>Notes per beat — a rough density measure.</summary>
        public float Density => durationBeats > 0f ? NoteCount / durationBeats : 0f;

        /// <summary>Whether this pattern contains any hold notes.</summary>
        public bool HasHolds
        {
            get
            {
                if (notes == null) return false;
                for (int i = 0; i < notes.Count; i++)
                {
                    if (!notes[i].IsTap) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Check if this pattern matches ALL of the required tags.
        /// </summary>
        public bool HasTags(PatternTag required)
        {
            if (required == PatternTag.None) return true;
            return (tags & required) == required;
        }

        /// <summary>
        /// Check if this pattern matches ANY of the given tags.
        /// </summary>
        public bool HasAnyTag(PatternTag any)
        {
            if (any == PatternTag.None) return true;
            return (tags & any) != 0;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-calculate duration from notes if it's too short
            if (notes != null && notes.Count > 0)
            {
                float maxBeat = 0f;
                for (int i = 0; i < notes.Count; i++)
                {
                    float end = notes[i].BeatOffset + notes[i].HoldBeats;
                    if (end > maxBeat) maxBeat = end;
                }

                if (durationBeats < maxBeat)
                    durationBeats = Mathf.Ceil(maxBeat);
            }
        }
#endif
    }
}
