#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using RhythmRogue.Data;

namespace RhythmRogue.Editor
{
    /// <summary>
    /// Imports a beat map JSON (from beat_analyzer.py) into a SongBeatMap
    /// ScriptableObject asset.
    /// 
    /// Usage:
    ///   1. Run beat_analyzer.py on your audio file
    ///   2. Copy the output JSON into your Unity project (anywhere)
    ///   3. RhythmRogue > Import Beat Map JSON
    ///   4. Select the JSON file
    ///   5. A SongBeatMap asset is created next to the JSON
    /// 
    /// Also supports drag-and-drop: just drop a .json file with
    /// "beatmap" in the name into Assets/_Project/Data/ and reimport.
    /// </summary>
    public static class BeatMapImporter
    {
        [MenuItem("RhythmRogue/Import Beat Map JSON")]
        public static void ImportFromDialog()
        {
            string path = EditorUtility.OpenFilePanel(
                "Select Beat Map JSON", Application.dataPath, "json");

            if (string.IsNullOrEmpty(path))
                return;

            Import(path);
        }

        public static void Import(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"[BeatMapImporter] File not found: {jsonPath}");
                return;
            }

            string json = File.ReadAllText(jsonPath);
            BeatMapJson data;

            try
            {
                data = JsonUtility.FromJson<BeatMapJson>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BeatMapImporter] Failed to parse JSON: {e.Message}");
                return;
            }

            if (data == null || data.markers == null || data.markers.Count == 0)
            {
                Debug.LogError("[BeatMapImporter] JSON contains no markers.");
                return;
            }

            // Create SongBeatMap asset
            var beatMap = ScriptableObject.CreateInstance<SongBeatMap>();
            beatMap.songName = data.songName;
            beatMap.bpm = data.bpm;
            beatMap.audioOffsetSeconds = data.audioOffsetSeconds;
            beatMap.totalBeats = data.totalBeats; // stored so TotalBeats is correct without loading the clip
            beatMap.clip = FindAudioClip(data.songName); // the beat map owns its audio; auto-link by name

            // Convert markers
            beatMap.markers = new List<BeatMarker>(data.markers.Count);
            foreach (var m in data.markers)
            {
                beatMap.markers.Add(new BeatMarker(
                    beat: m.beat,
                    type: ParseEnum(m.type, MarkerType.Accent),
                    intensity: m.intensity,
                    direction: m.direction,
                    holdBeats: m.holdBeats,
                    instrument: ParseEnum(m.instrument, ChartInstrument.All)
                ));
            }

            // Convert sections
            if (data.sections != null && data.sections.Count > 0)
            {
                beatMap.sections = new List<SongSection>(data.sections.Count);
                foreach (var s in data.sections)
                {
                    beatMap.sections.Add(new SongSection
                    {
                        label = s.label,
                        startBeat = s.startBeat,
                        endBeat = s.endBeat,
                        type = ParseEnum(s.type, SongSectionType.Verse),
                        highway = ParseEnum(s.highway, SectionType.PlayerOnly),
                        intensityScale = s.intensityScale,
                    });
                }
            }

            // Determine output path
            string fileName = Path.GetFileNameWithoutExtension(jsonPath);
            string outputDir = "Assets/_Project/Data/BeatMaps";

            if (!AssetDatabase.IsValidFolder(outputDir))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Data"))
                    AssetDatabase.CreateFolder("Assets/_Project", "Data");
                AssetDatabase.CreateFolder("Assets/_Project/Data", "BeatMaps");
            }

            string assetPath = $"{outputDir}/{fileName}.asset";

            // Check for existing
            var existing = AssetDatabase.LoadAssetAtPath<SongBeatMap>(assetPath);
            SongBeatMap saved;
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog(
                    "Overwrite?",
                    $"A beat map already exists at:\n{assetPath}\n\nUpdate it? It is rewritten in " +
                    "place, so its GUID and any enemy references to it are preserved.",
                    "Update", "Cancel"))
                {
                    return;
                }
                // Update in place (CopySerialized) instead of delete + recreate, so the asset keeps
                // its GUID. Deleting and recreating mints a new GUID and breaks every enemy's
                // reference to this beat map.
                EditorUtility.CopySerialized(beatMap, existing);
                EditorUtility.SetDirty(existing);
                saved = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(beatMap, assetPath);
                saved = beatMap;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Select in project
            EditorGUIUtility.PingObject(saved);
            Selection.activeObject = saved;

            string clipMsg = saved.clip != null ? saved.clip.name : "NONE FOUND (assign manually on the asset)";
            Debug.Log($"[BeatMapImporter] Imported: {assetPath}\n" +
                      $"  BPM: {data.bpm}, Markers: {saved.markers.Count}, " +
                      $"Sections: {saved.sections?.Count ?? 0}, Clip: {clipMsg}");
        }

        // =================================================================
        // AUDIO LINKING
        // =================================================================

        /// <summary>
        /// Find the AudioClip this beat map was generated from, by name, so the clip travels
        /// with the beat map. Prefers an exact name match; otherwise takes the first AudioClip
        /// the search returns. Returns null if none found (the caller logs it).
        /// </summary>
        private static AudioClip FindAudioClip(string songName)
        {
            if (string.IsNullOrEmpty(songName)) return null;

            string[] guids = AssetDatabase.FindAssets($"{songName} t:AudioClip");
            AudioClip firstAny = null;
            foreach (string guid in guids)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip == null) continue;
                if (clip.name == songName) return clip; // exact match wins
                firstAny ??= clip;
            }
            return firstAny;
        }

        // =================================================================
        // ENUM PARSING
        // =================================================================

        /// <summary>
        /// Parse an enum from its string name (case-insensitive). Falls back to a
        /// numeric string ("2") for older data, then to <paramref name="fallback"/>.
        /// JsonUtility cannot deserialize a string value into an enum/int field (it
        /// silently leaves 0), which previously made every imported section EnemyOnly
        /// and every marker Kick. Reading the raw strings and parsing here fixes that.
        /// </summary>
        private static T ParseEnum<T>(string value, T fallback) where T : struct, System.Enum
        {
            if (string.IsNullOrEmpty(value)) return fallback;
            if (System.Enum.TryParse<T>(value, ignoreCase: true, out var parsed)) return parsed;
            if (int.TryParse(value, out int idx) && System.Enum.IsDefined(typeof(T), idx))
                return (T)System.Enum.ToObject(typeof(T), idx);
            Debug.LogWarning($"[BeatMapImporter] Unknown {typeof(T).Name} value '{value}', using {fallback}.");
            return fallback;
        }

        // =================================================================
        // JSON DATA CLASSES (for JsonUtility deserialization)
        // =================================================================

        [System.Serializable]
        private class BeatMapJson
        {
            public string songName;
            public float bpm;
            public float totalBeats;
            public float audioOffsetSeconds;
            public List<MarkerJson> markers;
            public List<SectionJson> sections;
        }

        [System.Serializable]
        private class MarkerJson
        {
            public float beat;
            public string type;       // enum NAME e.g. "Kick" - JsonUtility can't read a string into an int field, so keep it a string and parse
            public float intensity;
            public float direction;
            public float holdBeats;
            public string instrument; // enum NAME e.g. "Drums"; absent on older JSON -> defaults to All
        }

        [System.Serializable]
        private class SectionJson
        {
            public string label;
            public float startBeat;
            public float endBeat;
            public string type;       // enum NAME e.g. "Chorus"
            public string highway;    // enum NAME e.g. "PlayerOnly"
            public float intensityScale;
        }
    }
}
#endif
