#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RhythmRogue.Data;

namespace RhythmRogue.Editor
{
    /// <summary>
    /// Generates PatternData assets across 4 difficulty tiers and a PatternLibrary.
    /// 
    /// Tier 1 (diff 1-2): Quarter notes, one per beat, simple shapes.
    /// Tier 2 (diff 3-4): Eighth notes, basic holds, more lane movement.
    /// Tier 3 (diff 5-6): Mixed note types, holds + taps, denser streams.
    /// Tier 4 (diff 7-8): Sixteenth bursts, jumps, tricky timing.
    /// 
    /// Each tier is stored in its own subfolder for easy browsing.
    /// 
    /// Run via: RhythmRogue > Generate Starter Chart Assets
    /// </summary>
    public static class StarterChartGenerator
    {
        private const string BasePath = "Assets/_Project/Data/Patterns";
        private const string Tier1Path = "Assets/_Project/Data/Patterns/Tier1_Easy";
        private const string Tier2Path = "Assets/_Project/Data/Patterns/Tier2_Medium";
        private const string Tier3Path = "Assets/_Project/Data/Patterns/Tier3_Hard";
        private const string Tier4Path = "Assets/_Project/Data/Patterns/Tier4_Expert";
        private const string LibraryPath = "Assets/_Project/Data";

        [MenuItem("RhythmRogue/Generate Starter Chart Assets")]
        public static void Generate()
        {
            EnsureFolder(Tier1Path);
            EnsureFolder(Tier2Path);
            EnsureFolder(Tier3Path);
            EnsureFolder(Tier4Path);
            EnsureFolder(LibraryPath);

            var allPatterns = new List<PatternData>();

            GenerateTier1(allPatterns);
            GenerateTier2(allPatterns);
            GenerateTier3(allPatterns);
            GenerateTier4(allPatterns);

            // ── PATTERN LIBRARY ─────────────────────────────────────

            var library = ScriptableObject.CreateInstance<PatternLibrary>();
            library.patterns = allPatterns;
            AssetDatabase.CreateAsset(library, $"{LibraryPath}/PatternLibrary.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[StarterChartGenerator] Created {allPatterns.Count} patterns across 4 tiers, 1 library.");
        }

        // =================================================================
        // TIER 1: EASY (difficulty 1-2)
        // Quarter notes, one per beat, simple shapes.
        // =================================================================

        private static void GenerateTier1(List<PatternData> all)
        {
            // Stairs up: L D U R
            all.Add(Create(Tier1Path, "t1_stairs_up", 1,
                PatternTag.Stream | PatternTag.Simple, "t1_stairs", new[]
                {
                    N(0, 0f), N(1, 1f), N(2, 2f), N(3, 3f),
                }));

            // Stairs down: R U D L
            all.Add(Create(Tier1Path, "t1_stairs_down", 1,
                PatternTag.Stream | PatternTag.Simple, "t1_stairs", new[]
                {
                    N(3, 0f), N(2, 1f), N(1, 2f), N(0, 3f),
                }));

            // Outer bounce: L R L R
            all.Add(Create(Tier1Path, "t1_outer_bounce", 1,
                PatternTag.Stream | PatternTag.Simple, "t1_outer_bounce", new[]
                {
                    N(0, 0f), N(3, 1f), N(0, 2f), N(3, 3f),
                }));

            // Inner bounce: D U D U
            all.Add(Create(Tier1Path, "t1_inner_bounce", 1,
                PatternTag.Stream | PatternTag.Simple, "t1_inner_bounce", new[]
                {
                    N(1, 0f), N(2, 1f), N(1, 2f), N(2, 3f),
                }));

            // Jacks left: L L L L
            all.Add(Create(Tier1Path, "t1_jacks_left", 2,
                PatternTag.Stream | PatternTag.Simple, "t1_jacks_left", new[]
                {
                    N(0, 0f), N(0, 1f), N(0, 2f), N(0, 3f),
                }));

            // Jacks right: R R R R
            all.Add(Create(Tier1Path, "t1_jacks_right", 2,
                PatternTag.Stream | PatternTag.Simple, "t1_jacks_right", new[]
                {
                    N(3, 0f), N(3, 1f), N(3, 2f), N(3, 3f),
                }));

            // Zigzag: L U R D
            all.Add(Create(Tier1Path, "t1_zigzag", 2,
                PatternTag.Stream | PatternTag.Simple, "t1_zigzag", new[]
                {
                    N(0, 0f), N(2, 1f), N(3, 2f), N(1, 3f),
                }));

            // Diamond: D L U R
            all.Add(Create(Tier1Path, "t1_diamond", 2,
                PatternTag.Stream | PatternTag.Simple, "t1_diamond", new[]
                {
                    N(1, 0f), N(0, 1f), N(2, 2f), N(3, 3f),
                }));
        }

        // =================================================================
        // TIER 2: MEDIUM (difficulty 3-4)
        // Eighth notes, simple holds, wider lane movement.
        // =================================================================

        private static void GenerateTier2(List<PatternData> all)
        {
            // Eighth stream outer: L R L R L R L R
            all.Add(Create(Tier2Path, "t2_eighth_outer", 3,
                PatternTag.Stream, "t2_eighth_outer", new[]
                {
                    N(0, 0f), N(3, 0.5f), N(0, 1f), N(3, 1.5f),
                    N(0, 2f), N(3, 2.5f), N(0, 3f), N(3, 3.5f),
                }));

            // Eighth stream inner: D U D U D U D U
            all.Add(Create(Tier2Path, "t2_eighth_inner", 3,
                PatternTag.Stream, "t2_eighth_inner", new[]
                {
                    N(1, 0f), N(2, 0.5f), N(1, 1f), N(2, 1.5f),
                    N(1, 2f), N(2, 2.5f), N(1, 3f), N(2, 3.5f),
                }));

            // Eighth stairs up: ascending pairs
            all.Add(Create(Tier2Path, "t2_eighth_stairs_up", 3,
                PatternTag.Stream, "t2_eighth_stairs", new[]
                {
                    N(0, 0f), N(1, 0.5f), N(1, 1f), N(2, 1.5f),
                    N(2, 2f), N(3, 2.5f), N(3, 3f), N(0, 3.5f),
                }));

            // Eighth stairs down: descending pairs
            all.Add(Create(Tier2Path, "t2_eighth_stairs_down", 3,
                PatternTag.Stream, "t2_eighth_stairs", new[]
                {
                    N(3, 0f), N(2, 0.5f), N(2, 1f), N(1, 1.5f),
                    N(1, 2f), N(0, 2.5f), N(0, 3f), N(3, 3.5f),
                }));

            // Simple hold left + taps right
            all.Add(Create(Tier2Path, "t2_hold_left", 4,
                PatternTag.Hold | PatternTag.Simple, "t2_hold_side", new[]
                {
                    N(0, 0f, 2f),
                    N(3, 0.5f), N(3, 1.5f),
                    N(3, 2f, 2f),
                }));

            // Simple hold right + taps left
            all.Add(Create(Tier2Path, "t2_hold_right", 4,
                PatternTag.Hold | PatternTag.Simple, "t2_hold_side", new[]
                {
                    N(3, 0f, 2f),
                    N(0, 0.5f), N(0, 1.5f),
                    N(0, 2f, 2f),
                }));

            // Wave: L D R U (eighth notes, rolling motion)
            all.Add(Create(Tier2Path, "t2_wave", 4,
                PatternTag.Stream, "t2_wave", new[]
                {
                    N(0, 0f), N(1, 0.5f), N(3, 1f), N(2, 1.5f),
                    N(0, 2f), N(1, 2.5f), N(3, 3f), N(2, 3.5f),
                }));

            // Skip step: L _ D _ R _ U _ (quarter notes with gaps, feels bouncy)
            all.Add(Create(Tier2Path, "t2_skip_step", 3,
                PatternTag.Stream | PatternTag.Simple, "t2_skip_step", new[]
                {
                    N(0, 0f), N(1, 1f), N(3, 2f), N(2, 3f),
                }));
        }

        // =================================================================
        // TIER 3: HARD (difficulty 5-6)
        // Mixed holds + taps, denser streams, syncopation.
        // =================================================================

        private static void GenerateTier3(List<PatternData> all)
        {
            // Full eighth stream ascending: L D U R L D U R
            all.Add(Create(Tier3Path, "t3_full_stream_up", 5,
                PatternTag.Stream, "t3_full_stream", new[]
                {
                    N(0, 0f), N(1, 0.5f), N(2, 1f), N(3, 1.5f),
                    N(0, 2f), N(1, 2.5f), N(2, 3f), N(3, 3.5f),
                }));

            // Full eighth stream descending: R U D L R U D L
            all.Add(Create(Tier3Path, "t3_full_stream_down", 5,
                PatternTag.Stream, "t3_full_stream", new[]
                {
                    N(3, 0f), N(2, 0.5f), N(1, 1f), N(0, 1.5f),
                    N(3, 2f), N(2, 2.5f), N(1, 3f), N(0, 3.5f),
                }));

            // Hold + stream: hold left, stream right side
            all.Add(Create(Tier3Path, "t3_hold_stream_left", 5,
                PatternTag.Hold | PatternTag.Stream, "t3_hold_stream", new[]
                {
                    N(0, 0f, 3.5f),
                    N(2, 0.5f), N(3, 1f), N(2, 1.5f),
                    N(3, 2f), N(2, 2.5f), N(3, 3f),
                }));

            // Hold + stream: hold right, stream left side
            all.Add(Create(Tier3Path, "t3_hold_stream_right", 5,
                PatternTag.Hold | PatternTag.Stream, "t3_hold_stream", new[]
                {
                    N(3, 0f, 3.5f),
                    N(1, 0.5f), N(0, 1f), N(1, 1.5f),
                    N(0, 2f), N(1, 2.5f), N(0, 3f),
                }));

            // Syncopated: off-beat emphasis
            all.Add(Create(Tier3Path, "t3_syncopated", 6,
                PatternTag.Tricky | PatternTag.Stream, "t3_syncopated", new[]
                {
                    N(0, 0f), N(2, 0.75f), N(1, 1.5f), N(3, 2f),
                    N(0, 2.75f), N(2, 3.25f), N(1, 3.75f),
                }));

            // Zigzag dense: rapid lane switching
            all.Add(Create(Tier3Path, "t3_zigzag_dense", 6,
                PatternTag.Stream | PatternTag.Tricky, "t3_zigzag_dense", new[]
                {
                    N(0, 0f), N(2, 0.5f), N(1, 1f), N(3, 1.5f),
                    N(1, 2f), N(3, 2.5f), N(0, 3f), N(2, 3.5f),
                }));

            // Double hold: two simultaneous holds
            all.Add(Create(Tier3Path, "t3_double_hold", 5,
                PatternTag.Hold, "t3_double_hold", new[]
                {
                    N(0, 0f, 1.5f), N(3, 0f, 1.5f),
                    N(1, 2f, 1.5f), N(2, 2f, 1.5f),
                }));

            // Gallop: short-short-long rhythm
            all.Add(Create(Tier3Path, "t3_gallop", 6,
                PatternTag.Stream, "t3_gallop", new[]
                {
                    N(0, 0f), N(0, 0.25f), N(2, 0.5f),
                    N(3, 1f), N(3, 1.25f), N(1, 1.5f),
                    N(0, 2f), N(0, 2.25f), N(2, 2.5f),
                    N(3, 3f), N(3, 3.25f), N(1, 3.5f),
                }));
        }

        // =================================================================
        // TIER 4: EXPERT (difficulty 7-8)
        // Sixteenth bursts, jumps, tricky timing, high density.
        // =================================================================

        private static void GenerateTier4(List<PatternData> all)
        {
            // Sixteenth burst up: fast 4-note run
            all.Add(Create(Tier4Path, "t4_16th_burst_up", 7,
                PatternTag.Stream | PatternTag.Dense, "t4_16th_burst", new[]
                {
                    N(0, 0f), N(1, 0.25f), N(2, 0.5f), N(3, 0.75f),
                    N(2, 2f), N(1, 2.25f), N(0, 2.5f), N(3, 2.75f),
                }));

            // Sixteenth burst down: fast 4-note run descending
            all.Add(Create(Tier4Path, "t4_16th_burst_down", 7,
                PatternTag.Stream | PatternTag.Dense, "t4_16th_burst", new[]
                {
                    N(3, 0f), N(2, 0.25f), N(1, 0.5f), N(0, 0.75f),
                    N(1, 2f), N(2, 2.25f), N(3, 2.5f), N(0, 2.75f),
                }));

            // Jump stream: alternating single + double
            all.Add(Create(Tier4Path, "t4_jump_stream", 7,
                PatternTag.Jump | PatternTag.Stream, "t4_jump_stream", new[]
                {
                    N(0, 0f), N(3, 0f),
                    N(1, 0.5f),
                    N(1, 1f), N(2, 1f),
                    N(3, 1.5f),
                    N(0, 2f), N(3, 2f),
                    N(2, 2.5f),
                    N(1, 3f), N(2, 3f),
                    N(0, 3.5f),
                }));

            // Tricky swap: irregular lane switching
            all.Add(Create(Tier4Path, "t4_tricky_swap", 8,
                PatternTag.Tricky | PatternTag.Stream, "t4_tricky_swap", new[]
                {
                    N(0, 0f), N(3, 0.5f),
                    N(1, 1f), N(2, 1.25f),
                    N(3, 1.75f), N(0, 2.25f),
                    N(2, 2.75f), N(1, 3.25f),
                    N(3, 3.75f),
                }));

            // Roll: rapid same-direction sweep repeated
            all.Add(Create(Tier4Path, "t4_roll", 7,
                PatternTag.Stream | PatternTag.Dense, "t4_roll", new[]
                {
                    N(0, 0f), N(1, 0.25f), N(2, 0.5f), N(3, 0.75f),
                    N(0, 1f), N(1, 1.25f), N(2, 1.5f), N(3, 1.75f),
                    N(0, 2f), N(1, 2.25f), N(2, 2.5f), N(3, 2.75f),
                    N(0, 3f), N(1, 3.25f), N(2, 3.5f), N(3, 3.75f),
                }));

            // Hold + jumps: hold one lane, jump the other two
            all.Add(Create(Tier4Path, "t4_hold_jumps", 8,
                PatternTag.Hold | PatternTag.Jump, "t4_hold_jumps", new[]
                {
                    N(0, 0f, 3.5f),
                    N(2, 0.5f), N(3, 0.5f),
                    N(1, 1.5f), N(2, 1.5f),
                    N(2, 2.5f), N(3, 2.5f),
                    N(1, 3.5f),
                }));

            // Stutter: eighth notes with a sixteenth hiccup
            all.Add(Create(Tier4Path, "t4_stutter", 7,
                PatternTag.Tricky | PatternTag.Stream, "t4_stutter", new[]
                {
                    N(0, 0f), N(1, 0.5f), N(2, 0.75f), N(3, 1f),
                    N(2, 1.5f), N(1, 2f), N(0, 2.25f), N(3, 2.5f),
                    N(0, 3f), N(2, 3.5f), N(1, 3.75f),
                }));

            // Chaos: no predictable pattern, all lanes
            all.Add(Create(Tier4Path, "t4_chaos", 8,
                PatternTag.Tricky | PatternTag.Dense, "t4_chaos", new[]
                {
                    N(2, 0f), N(0, 0.25f), N(3, 0.75f), N(1, 1f),
                    N(3, 1.5f), N(2, 1.75f), N(0, 2f), N(1, 2.5f),
                    N(3, 2.75f), N(0, 3f), N(2, 3.25f), N(1, 3.75f),
                }));
        }

        // =================================================================
        // UTILITIES
        // =================================================================

        [MenuItem("RhythmRogue/Recalculate Pattern Hints")]
        public static void RecalculateAllHints()
        {
            var guids = AssetDatabase.FindAssets("t:PatternData");
            foreach (string guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var pattern = AssetDatabase.LoadAssetAtPath<PatternData>(path);
                if (pattern == null) continue;
                pattern.RecalculateLaneHints();
                EditorUtility.SetDirty(pattern);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[StarterChartGenerator] Recalculated hints for {guids.Length} patterns.");
        }

        /// <summary>Shorthand for PatternNote constructor.</summary>
        private static PatternNote N(int lane, float beat, float hold = 0f)
        {
            return new PatternNote(lane, beat, hold);
        }

        private static PatternData Create(string folder, string name, int difficulty,
            PatternTag tags, string family, PatternNote[] notes)
        {
            var pattern = ScriptableObject.CreateInstance<PatternData>();
            pattern.patternName = name;
            pattern.difficulty = difficulty;
            pattern.tags = tags;
            pattern.durationBeats = 4f;
            pattern.weight = 1f;
            pattern.familyId = family;
            pattern.notes = new List<PatternNote>(notes);
            pattern.RecalculateLaneHints();

            AssetDatabase.CreateAsset(pattern, $"{folder}/{name}.asset");
            return pattern;
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
                string folder = System.IO.Path.GetFileName(path);

                if (!AssetDatabase.IsValidFolder(parent))
                    EnsureFolder(parent);

                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
#endif