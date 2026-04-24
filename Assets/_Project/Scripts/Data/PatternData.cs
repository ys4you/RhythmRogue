using System.Collections.Generic;
using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// A hand-authored rhythm pattern - a short sequence of notes
    /// (typically 1-4 bars) designed to be musical and fun to play.
    /// 
    /// Patterns are BPM-agnostic: all beat offsets are relative to
    /// the pattern start. The same pattern works at any tempo because
    /// the Conductor scales timing automatically.
    /// 
    /// The ChartAssembler and HybridAssembler pick patterns from a
    /// PatternLibrary based on difficulty, density, tags, and the run
    /// seed, then stamp them into absolute beat positions to produce
    /// a BattleChart.
    /// 
    /// Family ID: Patterns that are variants of each other (mirrored,
    /// rotated, inverted) should share the same familyId string. The
    /// assembler treats patterns in the same family as "the same shape"
    /// for repeat prevention. e.g. "staircase_up" and "staircase_down"
    /// both get familyId "staircase". If left empty, the pattern is
    /// treated as its own unique family.
    /// 
    /// StartLane / EndLane are auto-derived from notes for transition-
    /// aware selection.
    /// 
    /// Create via: Assets > Create > RhythmRogue > Pattern Data
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

        [Tooltip("Family group for repeat prevention. Patterns sharing a familyId " +
                 "(e.g. a pattern and its mirror) are treated as the same shape. " +
                 "Leave empty to treat this pattern as unique.")]
        public string familyId;

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

        [Tooltip("Density category for hybrid assembler matching. Auto-suggested in OnValidate based on notes per bar.")]
        public DensityCategory density = DensityCategory.Medium;

        [Tooltip("Tags describing this pattern's character. Used for filtering during assembly.")]
        public PatternTag tags = PatternTag.None;

        [Tooltip("Selection weight - higher = more likely to be picked. Default 1.")]
        [Min(0.1f)]
        public float weight = 1f;

        // =================================================================
        // TRANSITION HINTS (auto-calculated, used by transition-aware pick)
        // =================================================================

        [Header("Transition Hints (auto-calculated)")]
        [Tooltip("Lane of the first note. -1 if pattern has no notes.")]
        [SerializeField] private int _startLane = -1;

        [Tooltip("Lane of the last note. -1 if pattern has no notes.")]
        [SerializeField] private int _endLane = -1;

        /// <summary>Lane of the first note in this pattern. -1 if empty.</summary>
        public int StartLane => _startLane;

        /// <summary>Lane of the last note in this pattern. -1 if empty.</summary>
        public int EndLane => _endLane;

        /// <summary>
        /// Effective family identifier for repeat prevention.
        /// Returns familyId if set, otherwise falls back to the asset name
        /// so every pattern is at least its own family.
        /// </summary>
        public string EffectiveFamily =>
            !string.IsNullOrEmpty(familyId) ? familyId : name;

        // =================================================================
        // QUERIES
        // =================================================================

        /// <summary>Number of notes in this pattern.</summary>
        public int NoteCount => notes?.Count ?? 0;

        /// <summary>Notes per beat - a rough measure for diagnostics.</summary>
        public float NotesPerBeat => durationBeats > 0f ? NoteCount / durationBeats : 0f;

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

        // =================================================================
        // FAMILY CHECK
        // =================================================================

        /// <summary>
        /// Returns true if this pattern belongs to the same family as another.
        /// Used by the assembler to prevent mirror/variant repetition.
        /// </summary>
        public bool IsSameFamily(PatternData other)
        {
            if (other == null) return false;
            return EffectiveFamily == other.EffectiveFamily;
        }

        // =================================================================
        // LANE DISTANCE
        // =================================================================

        /// <summary>
        /// Manhattan distance between a previous pattern's end lane and
        /// this pattern's start lane. Returns 0 if either is unknown (-1).
        /// Used by the assembler to score transition smoothness.
        /// </summary>
        public int TransitionDistance(int previousEndLane)
        {
            if (previousEndLane < 0 || _startLane < 0) return 0;
            int diff = _startLane - previousEndLane;
            return diff < 0 ? -diff : diff;
        }

        // =================================================================
        // VALIDATION
        // =================================================================

#if UNITY_EDITOR
        private void OnValidate()
        {
            RecalculateLaneHints();
            AutoSuggestDensity();

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

        /// <summary>
        /// Recalculate StartLane and EndLane from the notes list.
        /// Called automatically in OnValidate. Can also be called at
        /// runtime if notes are modified programmatically.
        /// </summary>
        public void RecalculateLaneHints()
        {
            if (notes == null || notes.Count == 0)
            {
                _startLane = -1;
                _endLane = -1;
                return;
            }

            float earliestBeat = float.MaxValue;
            int startLane = -1;
            for (int i = 0; i < notes.Count; i++)
            {
                if (notes[i].BeatOffset < earliestBeat)
                {
                    earliestBeat = notes[i].BeatOffset;
                    startLane = notes[i].Lane;
                }
            }

            float latestBeat = float.MinValue;
            int endLane = -1;
            for (int i = 0; i < notes.Count; i++)
            {
                float noteEnd = notes[i].BeatOffset + notes[i].HoldBeats;
                if (noteEnd > latestBeat)
                {
                    latestBeat = noteEnd;
                    endLane = notes[i].Lane;
                }
            }

            _startLane = startLane;
            _endLane = endLane;
        }

        /// <summary>
        /// Auto-suggest density category from notes-per-bar ratio.
        /// Runs in editor via OnValidate. Authors can override after.
        /// </summary>
        private void AutoSuggestDensity()
        {
            if (notes == null || notes.Count == 0 || durationBeats <= 0f)
                return;

            float bars = durationBeats / 4f;
            float notesPerBar = bars > 0f ? notes.Count / bars : 0f;

            if (notesPerBar <= 2f)       density = DensityCategory.Sparse;
            else if (notesPerBar <= 4f)  density = DensityCategory.Light;
            else if (notesPerBar <= 6f)  density = DensityCategory.Medium;
            else if (notesPerBar <= 8f)  density = DensityCategory.Dense;
            else                         density = DensityCategory.VeryDense;
        }
    }
}