using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RhythmRogue.Data;

namespace RhythmRogue.EditorTools
{
    /// <summary>
    /// Generates a starter set of NotePattern fragments plus a NotePatternLibrary that
    /// references them. These are deliberately varied (sparse to dense, with syncopation,
    /// gallops, streams, jacks, chords, and holds) so the new assembler has real groove to
    /// place from day one. They are a starting point: open them in the Inspector and tune
    /// by feel, or add your own. Re-running updates the assets in place, preserving GUIDs
    /// and the library's references.
    ///
    /// Menu: RhythmRogue > Generate Starter Note Patterns
    /// </summary>
    public static class StarterPatternGenerator
    {
        private const string Folder = "Assets/_Project/SCO/Patterns";
        private const string LibraryPath = Folder + "/StarterPatternLibrary.asset";

        // Lane key: 0 = Left, 1 = Down, 2 = Up, 3 = Right.
        private struct Def
        {
            public string name, family;
            public float length;
            public int diff;
            public ShapeTag tags;
            public float weight;
            public (float off, int lane, float hold)[] notes;
        }

        private static readonly Def[] Defs =
        {
            // --- Sparse / easy ---
            new Def { name = "Pulse Walk", family = "pulse", length = 4f, diff = 1, tags = ShapeTag.None, weight = 1.0f,
                notes = new[] { (0f,0,0f), (1f,1,0f), (2f,2,0f), (3f,3,0f) } },

            new Def { name = "Backbeat", family = "pulse", length = 4f, diff = 1, tags = ShapeTag.None, weight = 0.8f,
                notes = new[] { (1f,1,0f), (3f,2,0f) } },

            new Def { name = "Open Hold", family = "hold", length = 4f, diff = 2, tags = ShapeTag.None, weight = 0.7f,
                notes = new[] { (0f,0,0f), (2f,3,2f) } },

            // --- Medium ---
            new Def { name = "Stair Up", family = "stair", length = 2f, diff = 3, tags = ShapeTag.Staircase, weight = 1.0f,
                notes = new[] { (0f,0,0f), (0.5f,1,0f), (1f,2,0f), (1.5f,3,0f) } },

            new Def { name = "Stair Down", family = "stair", length = 2f, diff = 3, tags = ShapeTag.Staircase, weight = 1.0f,
                notes = new[] { (0f,3,0f), (0.5f,2,0f), (1f,1,0f), (1.5f,0,0f) } },

            new Def { name = "Trill DU", family = "trill", length = 2f, diff = 4, tags = ShapeTag.Trill, weight = 0.9f,
                notes = new[] { (0f,1,0f), (0.5f,2,0f), (1f,1,0f), (1.5f,2,0f) } },

            new Def { name = "Gallop", family = "gallop", length = 4f, diff = 4, tags = ShapeTag.Skip, weight = 0.9f,
                notes = new[] { (0f,0,0f), (0.75f,0,0f), (1f,1,0f), (2f,3,0f), (2.75f,3,0f), (3f,2,0f) } },

            new Def { name = "Sync Skip", family = "sync", length = 4f, diff = 5, tags = ShapeTag.Skip, weight = 0.8f,
                notes = new[] { (0f,0,0f), (0.75f,2,0f), (1.5f,1,0f), (2.5f,3,0f), (3f,1,0f) } },

            // --- Dense ---
            new Def { name = "Stream", family = "stream", length = 4f, diff = 6, tags = ShapeTag.Roll, weight = 0.8f,
                notes = new[] { (0f,0,0f), (0.5f,1,0f), (1f,2,0f), (1.5f,3,0f), (2f,0,0f), (2.5f,1,0f), (3f,2,0f), (3.5f,3,0f) } },

            new Def { name = "Burst", family = "burst", length = 2f, diff = 7, tags = ShapeTag.Roll, weight = 0.7f,
                notes = new[] { (0f,0,0f), (0.25f,1,0f), (0.5f,2,0f), (0.75f,3,0f), (1f,2,0f) } },

            new Def { name = "Jacks U", family = "jack", length = 2f, diff = 6, tags = ShapeTag.Jack, weight = 0.6f,
                notes = new[] { (0f,2,0f), (0.5f,2,0f), (1f,2,0f), (1.5f,2,0f) } },

            new Def { name = "Jumps", family = "jump", length = 4f, diff = 7, tags = ShapeTag.Jump, weight = 0.7f,
                notes = new[] { (0f,0,0f), (0f,3,0f), (1f,1,0f), (1f,2,0f), (2f,0,0f), (2f,3,0f), (3f,1,0f), (3f,2,0f) } },

            // --- Hard / chaotic ---
            new Def { name = "Chaos Sync", family = "chaos", length = 4f, diff = 8, tags = ShapeTag.Chaotic, weight = 0.5f,
                notes = new[] { (0f,0,0f), (0.5f,1,0f), (0.75f,3,0f), (1.5f,2,0f), (2f,0,0f), (2.5f,3,0f), (2.75f,1,0f), (3.5f,2,0f) } },

            new Def { name = "Hold Stream", family = "hold", length = 4f, diff = 8, tags = ShapeTag.Roll | ShapeTag.Jump, weight = 0.5f,
                notes = new[] { (0f,0,2f), (0.5f,3,0f), (1f,1,0f), (1.5f,2,0f), (2f,3,0f), (2.5f,1,0f), (3f,2,0f), (3.5f,0,0f) } },
        };

        [MenuItem("RhythmRogue/Generate Starter Note Patterns")]
        public static void Generate()
        {
            EnsureFolder();

            var built = new List<NotePattern>(Defs.Length);
            foreach (Def d in Defs) built.Add(BuildOrUpdate(d));

            // Library (update in place if it exists, to preserve its GUID and any references)
            var lib = AssetDatabase.LoadAssetAtPath<NotePatternLibrary>(LibraryPath);
            bool newLib = lib == null;
            if (newLib) lib = ScriptableObject.CreateInstance<NotePatternLibrary>();
            lib.patterns = new List<NotePattern>(built);
            EditorUtility.SetDirty(lib);
            if (newLib) AssetDatabase.CreateAsset(lib, LibraryPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(lib);
            Selection.activeObject = lib;
            Debug.Log($"[StarterPatternGenerator] {built.Count} fragments {(newLib ? "created" : "updated")} in {Folder}, " +
                      $"library at {LibraryPath}.");
        }

        private static NotePattern BuildOrUpdate(Def d)
        {
            string path = $"{Folder}/{d.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<NotePattern>(path);

            var p = ScriptableObject.CreateInstance<NotePattern>();
            p.patternName = d.name;
            p.familyId = d.family;
            p.lengthBeats = d.length;
            p.difficulty = d.diff;
            p.tags = d.tags;
            p.weight = d.weight;
            p.notes = new List<NotePattern.Note>(d.notes.Length);
            foreach (var n in d.notes) p.notes.Add(new NotePattern.Note(n.off, n.lane, n.hold));

            if (existing != null)
            {
                // Update in place so the GUID (and the library's reference) survives a re-run.
                EditorUtility.CopySerialized(p, existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(p, path);
            return p;
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(Folder)) return;
            if (!AssetDatabase.IsValidFolder("Assets/_Project/SCO"))
                AssetDatabase.CreateFolder("Assets/_Project", "SCO");
            AssetDatabase.CreateFolder("Assets/_Project/SCO", "Patterns");
        }
    }
}
