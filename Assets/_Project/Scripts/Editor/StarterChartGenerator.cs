#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RhythmRogue.Data;

namespace RhythmRogue.Editor
{
    /// <summary>
    /// Generates starter PatternData assets, a PatternLibrary, and
    /// a default ChartTemplate for testing the dual highway system.
    /// 
    /// Run once via: RhythmRogue → Generate Starter Chart Assets
    /// 
    /// Creates assets under Assets/_Project/Data/Patterns/ and
    /// Assets/_Project/Data/ChartTemplates/.
    /// </summary>
    public static class StarterChartGenerator
    {
        private const string PatternPath = "Assets/_Project/Data/Patterns";
        private const string TemplatePath = "Assets/_Project/Data/ChartTemplates";
        private const string LibraryPath = "Assets/_Project/Data";

        [MenuItem("RhythmRogue/Generate Starter Chart Assets")]
        public static void Generate()
        {
            EnsureFolder(PatternPath);
            EnsureFolder(TemplatePath);
            EnsureFolder(LibraryPath);

            var allPatterns = new List<PatternData>();

            // ── SIMPLE PATTERNS (difficulty 1-2) ────────────────────

            allPatterns.Add(CreatePattern("quarter_basic", 1,
                PatternTag.Stream | PatternTag.Simple, 4f, new[]
                {
                    new PatternNote(0, 0f),
                    new PatternNote(2, 1f),
                    new PatternNote(1, 2f),
                    new PatternNote(3, 3f),
                }));

            allPatterns.Add(CreatePattern("quarter_lr", 1,
                PatternTag.Stream | PatternTag.Simple, 4f, new[]
                {
                    new PatternNote(0, 0f),
                    new PatternNote(3, 1f),
                    new PatternNote(0, 2f),
                    new PatternNote(3, 3f),
                }));

            allPatterns.Add(CreatePattern("quarter_stairs_up", 2,
                PatternTag.Stream | PatternTag.Simple, 4f, new[]
                {
                    new PatternNote(0, 0f),
                    new PatternNote(1, 1f),
                    new PatternNote(2, 2f),
                    new PatternNote(3, 3f),
                }));

            allPatterns.Add(CreatePattern("hold_basic", 2,
                PatternTag.Hold | PatternTag.Simple, 4f, new[]
                {
                    new PatternNote(0, 0f, 2f),
                    new PatternNote(3, 2f, 2f),
                }));

            // ── MEDIUM PATTERNS (difficulty 3-5) ────────────────────

            allPatterns.Add(CreatePattern("eighth_stream_lr", 3,
                PatternTag.Stream, 4f, new[]
                {
                    new PatternNote(0, 0f),
                    new PatternNote(3, 0.5f),
                    new PatternNote(0, 1f),
                    new PatternNote(3, 1.5f),
                    new PatternNote(0, 2f),
                    new PatternNote(3, 2.5f),
                    new PatternNote(0, 3f),
                    new PatternNote(3, 3.5f),
                }));

            allPatterns.Add(CreatePattern("eighth_stream_all", 4,
                PatternTag.Stream, 4f, new[]
                {
                    new PatternNote(0, 0f),
                    new PatternNote(1, 0.5f),
                    new PatternNote(2, 1f),
                    new PatternNote(3, 1.5f),
                    new PatternNote(0, 2f),
                    new PatternNote(1, 2.5f),
                    new PatternNote(2, 3f),
                    new PatternNote(3, 3.5f),
                }));

            allPatterns.Add(CreatePattern("jump_quarter", 3,
                PatternTag.Jump, 4f, new[]
                {
                    new PatternNote(0, 0f),
                    new PatternNote(3, 0f),  // simultaneous
                    new PatternNote(1, 2f),
                    new PatternNote(2, 2f),  // simultaneous
                }));

            allPatterns.Add(CreatePattern("hold_stream", 4,
                PatternTag.Hold | PatternTag.Stream, 4f, new[]
                {
                    new PatternNote(0, 0f, 3f),  // long hold left
                    new PatternNote(3, 0.5f),
                    new PatternNote(2, 1.5f),
                    new PatternNote(3, 2.5f),
                    new PatternNote(2, 3.5f),
                }));

            allPatterns.Add(CreatePattern("mixed_medium", 5,
                PatternTag.Mixed, 4f, new[]
                {
                    new PatternNote(0, 0f),
                    new PatternNote(1, 0.5f),
                    new PatternNote(2, 1f, 1f),  // short hold
                    new PatternNote(3, 2f),
                    new PatternNote(0, 2.5f),
                    new PatternNote(1, 3f),
                    new PatternNote(3, 3.5f),
                }));

            // ── HARD PATTERNS (difficulty 6-8) ──────────────────────

            allPatterns.Add(CreatePattern("sixteenth_burst", 6,
                PatternTag.Stream | PatternTag.Dense, 4f, new[]
                {
                    new PatternNote(0, 0f),
                    new PatternNote(1, 0.25f),
                    new PatternNote(2, 0.5f),
                    new PatternNote(3, 0.75f),
                    new PatternNote(2, 2f),
                    new PatternNote(1, 2.25f),
                    new PatternNote(0, 2.5f),
                    new PatternNote(3, 2.75f),
                }));

            allPatterns.Add(CreatePattern("tricky_swap", 7,
                PatternTag.Tricky | PatternTag.Stream, 4f, new[]
                {
                    new PatternNote(0, 0f),
                    new PatternNote(3, 0.5f),
                    new PatternNote(1, 1f),
                    new PatternNote(2, 1.25f),
                    new PatternNote(3, 1.75f),
                    new PatternNote(0, 2.25f),
                    new PatternNote(2, 2.75f),
                    new PatternNote(1, 3.25f),
                    new PatternNote(3, 3.75f),
                }));

            // ── REST PATTERN ────────────────────────────────────────

            allPatterns.Add(CreatePattern("rest_4beat", 1,
                PatternTag.Rest | PatternTag.Simple, 4f,
                System.Array.Empty<PatternNote>()));

            // ── PATTERN LIBRARY ─────────────────────────────────────

            var library = ScriptableObject.CreateInstance<PatternLibrary>();
            library.patterns = allPatterns;
            AssetDatabase.CreateAsset(library, $"{LibraryPath}/PatternLibrary.asset");

            // ── DEFAULT CHART TEMPLATES ─────────────────────────────

            // Turn-based: alternating enemy → player sections
            var turnBased = ScriptableObject.CreateInstance<ChartTemplate>();
            turnBased.templateName = "turn_based_basic";
            turnBased.leadInBeats = 4f;
            turnBased.tailBeats = 2f;
            turnBased.sections = new List<SectionSlot>
            {
                MakeSlot(SectionType.EnemyOnly, 4f, 3),
                MakeSlot(SectionType.PlayerOnly, 4f, 3),
                MakeSlot(SectionType.EnemyOnly, 4f, 4),
                MakeSlot(SectionType.PlayerOnly, 4f, 4),
                MakeSlot(SectionType.EnemyOnly, 4f, 5),
                MakeSlot(SectionType.PlayerOnly, 4f, 5),
                MakeSlot(SectionType.Both, 8f, 5),
            };
            AssetDatabase.CreateAsset(turnBased, $"{TemplatePath}/TurnBased_Basic.asset");

            // Simultaneous: both highways active throughout
            var simultaneous = ScriptableObject.CreateInstance<ChartTemplate>();
            simultaneous.templateName = "simultaneous_basic";
            simultaneous.leadInBeats = 4f;
            simultaneous.tailBeats = 2f;
            simultaneous.sections = new List<SectionSlot>
            {
                MakeSlot(SectionType.Both, 8f, 3),
                MakeSlot(SectionType.Both, 8f, 4),
                MakeSlot(SectionType.Both, 8f, 5),
            };
            AssetDatabase.CreateAsset(simultaneous, $"{TemplatePath}/Simultaneous_Basic.asset");

            // Escalating: starts turn-based, ends simultaneous
            var escalating = ScriptableObject.CreateInstance<ChartTemplate>();
            escalating.templateName = "escalating_boss";
            escalating.leadInBeats = 4f;
            escalating.tailBeats = 4f;
            escalating.sections = new List<SectionSlot>
            {
                MakeSlot(SectionType.EnemyOnly, 4f, 3),
                MakeSlot(SectionType.PlayerOnly, 4f, 3),
                MakeSlot(SectionType.EnemyOnly, 4f, 5),
                MakeSlot(SectionType.PlayerOnly, 4f, 5),
                MakeSlot(SectionType.Both, 8f, 6),
                MakeSlot(SectionType.Both, 8f, 7),
            };
            AssetDatabase.CreateAsset(escalating, $"{TemplatePath}/Escalating_Boss.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[StarterChartGenerator] Created {allPatterns.Count} patterns, " +
                      "1 library, 3 templates.");
        }

        private static PatternData CreatePattern(string name, int difficulty,
            PatternTag tags, float duration, PatternNote[] notes)
        {
            var pattern = ScriptableObject.CreateInstance<PatternData>();
            pattern.patternName = name;
            pattern.difficulty = difficulty;
            pattern.tags = tags;
            pattern.durationBeats = duration;
            pattern.weight = 1f;
            pattern.notes = new List<PatternNote>(notes);

            AssetDatabase.CreateAsset(pattern, $"{PatternPath}/{name}.asset");
            return pattern;
        }

        private static SectionSlot MakeSlot(SectionType type, float duration, int maxDiff,
            PatternTag tags = PatternTag.None)
        {
            return new SectionSlot
            {
                type = type,
                durationBeats = duration,
                maxDifficulty = maxDiff,
                requiredTags = tags,
                forcedPattern = null
            };
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
