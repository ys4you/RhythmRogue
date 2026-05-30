using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace RhythmRogue.UI.Navigation
{
    /// <summary>
    /// Ensures a single EventSystem with the correct input module exists.
    ///
    /// Since the project uses Unity's new Input System for gameplay,
    /// the UI must use InputSystemUIInputModule (not StandaloneInputModule).
    /// This module routes Submit (Enter/Space/A), Cancel (Escape/B),
    /// and Navigate (arrows/D-pad/stick) actions to the EventSystem.
    ///
    /// Call EnsureEventSystem() from any screen's setup code instead
    /// of manually creating EventSystem + StandaloneInputModule.
    ///
    /// Safe to call multiple times — skips creation if one exists.
    /// </summary>
    public static class UIEventSystemProvider
    {
        /// <summary>
        /// Ensure a valid EventSystem exists in the scene.
        /// Creates one with InputSystemUIInputModule if missing.
        /// </summary>
        public static EventSystem EnsureEventSystem()
        {
            EventSystem existing = EventSystem.current;

            if (existing != null)
            {
                EnsureInputModule(existing.gameObject);
                UINavigationGate.Ensure();
                return existing;
            }

            // None found — create one
            GameObject go = new GameObject("EventSystem");
            EventSystem es = go.AddComponent<EventSystem>();
            EnsureInputModule(go);
            // Gate keyboard/gamepad navigation so it stays off until actually used.
            // Added after the input module so the gate can find and control it.
            UINavigationGate.Ensure();

            return es;
        }

        /// <summary>
        /// Ensure the EventSystem GameObject has the correct input module.
        /// Removes legacy StandaloneInputModule if present and adds
        /// InputSystemUIInputModule.
        /// </summary>
        private static void EnsureInputModule(GameObject go)
        {
#if ENABLE_INPUT_SYSTEM
            // Remove legacy module if present — it conflicts with new Input System
            var legacy = go.GetComponent<StandaloneInputModule>();
            if (legacy != null)
                Object.Destroy(legacy);

            // Add new Input System module if missing
            if (go.GetComponent<InputSystemUIInputModule>() == null)
                go.AddComponent<InputSystemUIInputModule>();
#else
            // Fallback: project doesn't have new Input System enabled
            if (go.GetComponent<StandaloneInputModule>() == null)
                go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
