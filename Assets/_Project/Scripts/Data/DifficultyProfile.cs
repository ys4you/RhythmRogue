using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Maps a difficulty float (0.0–1.0) to concrete chart constraints.
    /// 
    /// Instead of a single intensity threshold, difficulty controls
    /// multiple parameters simultaneously:
    ///   - How many markers become notes (intensity threshold)
    ///   - Maximum notes per beat (density cap)
    ///   - Minimum gap between consecutive notes
    ///   - Whether jumps (simultaneous notes) are allowed
    ///   - Which lane patterns are available (complexity ceiling)
    ///   - Jump probability when allowed
    /// 
    /// This is what creates the range from "beginner" (1 note per bar,
    /// same lane) to "expert" (dense streams with jumps).
    /// 
    /// Computed once per chart assembly. Passed to PhrasePlanner
    /// so it can enforce all constraints.
    /// 
    /// Pure data, no logic beyond the mapping curves.
    /// </summary>
    public readonly struct DifficultyProfile
    {
        /// <summary>
        /// Markers below this intensity are filtered out.
        /// Beginner: 0.85 (only the loudest hits)
        /// Expert:   0.05 (almost everything)
        /// </summary>
        public readonly float IntensityThreshold;

        /// <summary>
        /// Maximum notes per beat. Caps density regardless of how many
        /// markers pass the intensity filter.
        /// Beginner: 0.5 (1 note every 2 beats)
        /// Expert:   4.0 (16th notes)
        /// </summary>
        public readonly float MaxNotesPerBeat;

        /// <summary>
        /// Minimum beats between consecutive notes. Notes closer than
        /// this are thinned out.
        /// Beginner: 2.0 (very sparse)
        /// Expert:   0.125 (32nd note gap minimum)
        /// </summary>
        public readonly float MinNoteGapBeats;

        /// <summary>
        /// Whether jump notes (two simultaneous lanes) are allowed at all.
        /// Disabled below difficulty 0.5.
        /// </summary>
        public readonly bool JumpsEnabled;

        /// <summary>
        /// Probability of a jump when intensity is high enough.
        /// 0.0 at normal, scaling to 0.4 at expert.
        /// </summary>
        public readonly float JumpChance;

        /// <summary>
        /// Maximum pattern complexity tier (0–3).
        /// 0 = jacks only (same lane repeated)
        /// 1 = trills (two lane alternation)
        /// 2 = streams and staircases (multi-lane movement)
        /// 3 = zigzags, mirrors, rolls (full complexity)
        /// </summary>
        public readonly int MaxPatternComplexity;

        /// <summary>The raw difficulty value this was computed from.</summary>
        public readonly float RawDifficulty;

        private DifficultyProfile(
            float intensityThreshold,
            float maxNotesPerBeat,
            float minNoteGapBeats,
            bool jumpsEnabled,
            float jumpChance,
            int maxPatternComplexity,
            float rawDifficulty)
        {
            IntensityThreshold = intensityThreshold;
            MaxNotesPerBeat = maxNotesPerBeat;
            MinNoteGapBeats = minNoteGapBeats;
            JumpsEnabled = jumpsEnabled;
            JumpChance = jumpChance;
            MaxPatternComplexity = maxPatternComplexity;
            RawDifficulty = rawDifficulty;
        }

        /// <summary>
        /// Compute a DifficultyProfile from a 0.0–1.0 difficulty value.
        /// All curves are hand-tuned for rhythm game feel.
        /// </summary>
        public static DifficultyProfile FromDifficulty(float difficulty)
        {
            difficulty = Mathf.Clamp01(difficulty);

            // Intensity threshold: high at beginner (strict filter), low at expert
            // Non-linear: drops fast in the middle range where most gameplay happens
            float threshold = Mathf.Lerp(0.85f, 0.05f, EaseOutCubic(difficulty));

            // Max notes per beat: very sparse at beginner, dense at expert
            float maxNPB = Mathf.Lerp(0.5f, 4f, EaseInQuad(difficulty));

            // Min gap: wide at beginner, tiny at expert
            float minGap = Mathf.Lerp(2.0f, 0.125f, EaseOutQuad(difficulty));

            // Jumps: disabled below 0.5, then scaling probability
            bool jumps = difficulty >= 0.5f;
            float jumpChance = difficulty >= 0.5f
                ? Mathf.Lerp(0f, 0.4f, (difficulty - 0.5f) * 2f)
                : 0f;

            // Pattern complexity: stepped tiers
            int complexity;
            if (difficulty < 0.25f)      complexity = 0; // Jacks only
            else if (difficulty < 0.45f) complexity = 1; // + Trills
            else if (difficulty < 0.7f)  complexity = 2; // + Streams, staircases
            else                         complexity = 3; // Everything

            return new DifficultyProfile(
                threshold, maxNPB, minGap, jumps, jumpChance, complexity, difficulty);
        }

        // =================================================================
        // EASING CURVES
        // =================================================================

        private static float EaseOutCubic(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
        private static float EaseInQuad(float t) => t * t;
        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        // =================================================================
        // DEBUG
        // =================================================================

        public override string ToString()
        {
            string tier = RawDifficulty switch
            {
                < 0.25f => "Beginner",
                < 0.45f => "Easy",
                < 0.65f => "Normal",
                < 0.85f => "Hard",
                _ => "Expert"
            };

            return $"[{tier} ({RawDifficulty:F2}): threshold={IntensityThreshold:F2}, " +
                   $"maxNPB={MaxNotesPerBeat:F1}, gap={MinNoteGapBeats:F2}b, " +
                   $"jumps={JumpsEnabled}, complexity={MaxPatternComplexity}]";
        }
    }
}
