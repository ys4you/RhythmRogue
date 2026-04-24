#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RhythmRogue.Data;

namespace RhythmRogue.Editor
{
    /// <summary>
    /// Generates LaneShape assets with beginner-first design philosophy.
    /// 
    /// Tier 1 (diff 1-2): ONLY smooth directional movement. No jacks.
    ///   Short shapes (2-4 lanes). Predictable, easy to read.
    ///   A new player should instantly understand what to press.
    /// 
    /// Tier 2 (diff 3-4): Slightly longer shapes, wider movement.
    ///   Still no jacks. Introduces skipping lanes.
    /// 
    /// Tier 3 (diff 5-6): Longer shapes, mixed movement, first jacks.
    ///   Introduces irregular patterns.
    /// 
    /// Tier 4 (diff 7-8): Complex, fast, chaotic for experts.
    /// 
    /// Run via: RhythmRogue > Generate Shape Library
    /// </summary>
    public static class StarterShapeGenerator
    {
        private const string Tier1Path = "Assets/_Project/Data/Shapes/Tier1_Easy";
        private const string Tier2Path = "Assets/_Project/Data/Shapes/Tier2_Medium";
        private const string Tier3Path = "Assets/_Project/Data/Shapes/Tier3_Hard";
        private const string Tier4Path = "Assets/_Project/Data/Shapes/Tier4_Expert";
        private const string LibraryPath = "Assets/_Project/Data";

        [MenuItem("RhythmRogue/Generate Shape Library")]
        public static void Generate()
        {
            EnsureFolder(Tier1Path);
            EnsureFolder(Tier2Path);
            EnsureFolder(Tier3Path);
            EnsureFolder(Tier4Path);
            EnsureFolder(LibraryPath);

            var all = new List<LaneShape>();

            GenerateTier1(all);
            GenerateTier2(all);
            GenerateTier3(all);
            GenerateTier4(all);

            var library = ScriptableObject.CreateInstance<ShapeLibrary>();
            library.shapes = all;
            AssetDatabase.CreateAsset(library, $"{LibraryPath}/ShapeLibrary.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[StarterShapeGenerator] Created {all.Count} shapes across 4 tiers.");
        }

        // =================================================================
        // TIER 1: BEGINNER (difficulty 1-2)
        // 
        // Rules:
        //   - NO jacks (no same lane repeated)
        //   - Only 2-4 notes per shape
        //   - Only adjacent lane movement (no skipping)
        //   - Every shape should be instantly readable
        // =================================================================

        private static void GenerateTier1(List<LaneShape> all)
        {
            // 2-note pairs (simplest possible)
            all.Add(Create(Tier1Path, "pair_ld", 1, ShapeTag.Staircase,
                "pair_left_start", new[] { 0, 1 }));

            all.Add(Create(Tier1Path, "pair_du", 1, ShapeTag.Staircase,
                "pair_inner", new[] { 1, 2 }));

            all.Add(Create(Tier1Path, "pair_ur", 1, ShapeTag.Staircase,
                "pair_right_start", new[] { 2, 3 }));

            all.Add(Create(Tier1Path, "pair_rd", 1, ShapeTag.Staircase,
                "pair_right_start", new[] { 3, 2 }));

            all.Add(Create(Tier1Path, "pair_ud", 1, ShapeTag.Staircase,
                "pair_inner", new[] { 2, 1 }));

            all.Add(Create(Tier1Path, "pair_dl", 1, ShapeTag.Staircase,
                "pair_left_start", new[] { 1, 0 }));

            // 3-note staircases (clear direction)
            all.Add(Create(Tier1Path, "stair3_up", 2, ShapeTag.Staircase,
                "stair3", new[] { 0, 1, 2 }));

            all.Add(Create(Tier1Path, "stair3_down", 2, ShapeTag.Staircase,
                "stair3", new[] { 3, 2, 1 }));

            all.Add(Create(Tier1Path, "stair3_mid_up", 2, ShapeTag.Staircase,
                "stair3_mid", new[] { 1, 2, 3 }));

            all.Add(Create(Tier1Path, "stair3_mid_down", 2, ShapeTag.Staircase,
                "stair3_mid", new[] { 2, 1, 0 }));

            // 4-note full staircase (the classic)
            all.Add(Create(Tier1Path, "stair4_up", 2, ShapeTag.Staircase,
                "stair4", new[] { 0, 1, 2, 3 }));

            all.Add(Create(Tier1Path, "stair4_down", 2, ShapeTag.Staircase,
                "stair4", new[] { 3, 2, 1, 0 }));

            // Simple alternation (trill between adjacent lanes)
            all.Add(Create(Tier1Path, "trill_ld", 2, ShapeTag.Trill,
                "trill_outer_left", new[] { 0, 1, 0, 1 }));

            all.Add(Create(Tier1Path, "trill_ur", 2, ShapeTag.Trill,
                "trill_outer_right", new[] { 2, 3, 2, 3 }));

            all.Add(Create(Tier1Path, "trill_du", 2, ShapeTag.Trill,
                "trill_inner", new[] { 1, 2, 1, 2 }));
        }

        // =================================================================
        // TIER 2: MEDIUM (difficulty 3-4)
        // 
        // Rules:
        //   - Still no jacks
        //   - 3-5 notes per shape
        //   - Can skip one lane (L to U, D to R)
        //   - Introduces wider alternation
        // =================================================================

        private static void GenerateTier2(List<LaneShape> all)
        {
            // Wide alternation (skipping middle lanes)
            all.Add(Create(Tier2Path, "bounce_lr", 3, ShapeTag.Trill,
                "bounce_wide", new[] { 0, 3, 0, 3 }));

            all.Add(Create(Tier2Path, "bounce_dl_ur", 3, ShapeTag.Trill,
                "bounce_cross", new[] { 1, 3, 1, 3 }));

            all.Add(Create(Tier2Path, "bounce_lu_rd", 3, ShapeTag.Trill,
                "bounce_cross2", new[] { 0, 2, 0, 2 }));

            // Wave: up and back
            all.Add(Create(Tier2Path, "wave_up_back", 3, ShapeTag.Roll,
                "wave_up", new[] { 0, 1, 2, 1 }));

            all.Add(Create(Tier2Path, "wave_down_back", 3, ShapeTag.Roll,
                "wave_down", new[] { 3, 2, 1, 2 }));

            // 5-note shapes: staircase + turn
            all.Add(Create(Tier2Path, "stair_turn_up", 4, ShapeTag.Staircase | ShapeTag.Roll,
                "stair_turn", new[] { 0, 1, 2, 3, 2 }));

            all.Add(Create(Tier2Path, "stair_turn_down", 4, ShapeTag.Staircase | ShapeTag.Roll,
                "stair_turn", new[] { 3, 2, 1, 0, 1 }));

            // Zigzag: non-adjacent but smooth
            all.Add(Create(Tier2Path, "zigzag_a", 4, ShapeTag.Skip,
                "zigzag", new[] { 0, 2, 1, 3 }));

            all.Add(Create(Tier2Path, "zigzag_b", 4, ShapeTag.Skip,
                "zigzag", new[] { 3, 1, 2, 0 }));

            // Triangle shapes
            all.Add(Create(Tier2Path, "triangle_left", 3, ShapeTag.Roll,
                "triangle", new[] { 0, 2, 1 }));

            all.Add(Create(Tier2Path, "triangle_right", 3, ShapeTag.Roll,
                "triangle", new[] { 3, 1, 2 }));
        }

        // =================================================================
        // TIER 3: HARD (difficulty 5-6)
        // 
        // Rules:
        //   - Jacks allowed (sparingly)
        //   - 4-6 notes per shape
        //   - Irregular movement patterns
        //   - More demanding lane switches
        // =================================================================

        private static void GenerateTier3(List<LaneShape> all)
        {
            // Full roll
            all.Add(Create(Tier3Path, "roll_right", 5, ShapeTag.Roll,
                "roll", new[] { 0, 1, 2, 3, 0, 1 }));

            all.Add(Create(Tier3Path, "roll_left", 5, ShapeTag.Roll,
                "roll", new[] { 3, 2, 1, 0, 3, 2 }));

            // Gallop: double tap then move
            all.Add(Create(Tier3Path, "gallop_up", 6, ShapeTag.Jack | ShapeTag.Staircase,
                "gallop", new[] { 0, 0, 1, 1, 2, 2 }));

            all.Add(Create(Tier3Path, "gallop_down", 6, ShapeTag.Jack | ShapeTag.Staircase,
                "gallop", new[] { 3, 3, 2, 2, 1, 1 }));

            // Wide zigzag
            all.Add(Create(Tier3Path, "zigzag_wide", 5, ShapeTag.Skip,
                "zigzag_wide", new[] { 0, 3, 1, 2, 3, 0 }));

            // Syncopated: breaks expectation
            all.Add(Create(Tier3Path, "syncopated_a", 6, ShapeTag.Skip,
                "syncopated", new[] { 0, 2, 3, 1, 0, 3 }));

            all.Add(Create(Tier3Path, "syncopated_b", 6, ShapeTag.Skip,
                "syncopated", new[] { 3, 1, 0, 2, 3, 0 }));

            // Anchor: one lane repeats while others change
            all.Add(Create(Tier3Path, "anchor_left", 5, ShapeTag.Jack,
                "anchor", new[] { 0, 2, 0, 3, 0, 1 }));

            all.Add(Create(Tier3Path, "anchor_right", 5, ShapeTag.Jack,
                "anchor", new[] { 3, 1, 3, 0, 3, 2 }));
        }

        // =================================================================
        // TIER 4: EXPERT (difficulty 7-8)
        // =================================================================

        private static void GenerateTier4(List<LaneShape> all)
        {
            // Rapid roll extended
            all.Add(Create(Tier4Path, "rapid_roll_r", 7, ShapeTag.Roll,
                "rapid_roll", new[] { 0, 1, 2, 3, 0, 1, 2, 3 }));

            all.Add(Create(Tier4Path, "rapid_roll_l", 7, ShapeTag.Roll,
                "rapid_roll", new[] { 3, 2, 1, 0, 3, 2, 1, 0 }));

            // Chaos
            all.Add(Create(Tier4Path, "chaos_a", 8, ShapeTag.Chaotic,
                "chaos_a", new[] { 2, 0, 3, 1, 3, 0, 2, 1 }));

            all.Add(Create(Tier4Path, "chaos_b", 8, ShapeTag.Chaotic,
                "chaos_b", new[] { 1, 3, 0, 2, 0, 3, 1, 2 }));

            // Jack stream
            all.Add(Create(Tier4Path, "jack_stream", 7, ShapeTag.Jack | ShapeTag.Staircase,
                "jack_stream", new[] { 0, 0, 1, 1, 3, 3, 2, 2 }));

            // Tricky skip
            all.Add(Create(Tier4Path, "tricky_skip", 8, ShapeTag.Skip | ShapeTag.Chaotic,
                "tricky_skip", new[] { 0, 3, 0, 2, 3, 1, 0, 3 }));

            // Split stream
            all.Add(Create(Tier4Path, "split_stream", 7, ShapeTag.Trill | ShapeTag.Skip,
                "split_stream", new[] { 0, 3, 1, 2, 0, 3, 1, 2 }));

            // Mirror chaos
            all.Add(Create(Tier4Path, "mirror_chaos", 8, ShapeTag.Chaotic,
                "mirror_chaos", new[] { 3, 0, 1, 2, 3, 1, 0, 2 }));
        }

        // =================================================================
        // UTILITY
        // =================================================================

        private static LaneShape Create(string folder, string name, int difficulty,
            ShapeTag tags, string family, int[] lanes)
        {
            var shape = ScriptableObject.CreateInstance<LaneShape>();
            shape.shapeName = name;
            shape.difficulty = difficulty;
            shape.tags = tags;
            shape.familyId = family;
            shape.weight = 1f;
            shape.lanes = new List<int>(lanes);

            AssetDatabase.CreateAsset(shape, $"{folder}/{name}.asset");
            return shape;
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