#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RhythmRogue.DevTools
{
    /// <summary>
    /// Robustly locates a ScriptableObject asset for dev tooling, even when no currently-open scene
    /// references it (so Resources.FindObjectsOfTypeAll alone would miss it, e.g. the relic pool on
    /// the map scene). Resolution order:
    ///   1. an already-loaded instance (referenced by an open scene, or loaded earlier), so we use
    ///      the live instance the game is actually mutating when there is one;
    ///   2. (editor only) the project asset via AssetDatabase, regardless of scene references.
    /// In a non-editor development build only step 1 applies, which is fine since the relevant
    /// assets are loaded once a run is underway.
    /// </summary>
    public static class DevAssets
    {
        public static T FindScriptableObject<T>() where T : ScriptableObject
        {
            var loaded = Resources.FindObjectsOfTypeAll<T>();
            if (loaded != null && loaded.Length > 0) return loaded[0];

#if UNITY_EDITOR
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            if (guids != null && guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) return asset;
            }
#endif
            return null;
        }
    }
}
#endif
