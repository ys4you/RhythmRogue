using UnityEngine;

namespace RhythmRogue.Util.Extensions
{
    /// <summary>
    /// Extension methods for Component and GameObject.
    /// Common operations for component management and
    /// GameObject state control.
    /// </summary>
    public static class ComponentExtensions
    {
        /// <summary>
        /// Get a component, or add it if missing.
        /// Avoids the "get then null-check then add" pattern.
        /// 
        ///   var rb = gameObject.GetOrAddComponent&lt;Rigidbody2D&gt;();
        /// </summary>
        public static T GetOrAddComponent<T>(this GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();

            if (component == null)
                component = go.AddComponent<T>();

            return component;
        }

        /// <summary>
        /// Get a component, or add it if missing. Called on Component.
        /// 
        ///   var collider = transform.GetOrAddComponent&lt;BoxCollider2D&gt;();
        /// </summary>
        public static T GetOrAddComponent<T>(this Component c) where T : Component
        {
            return c.gameObject.GetOrAddComponent<T>();
        }

        /// <summary>
        /// Enable or disable a GameObject with a bool.
        /// Reads more naturally in conditional contexts.
        /// 
        ///   // Show combo counter only during battle
        ///   comboUI.SetActive(battleActive);
        ///   
        ///   // Or on a component:
        ///   comboCounter.SetActive(battleActive);
        /// </summary>
        public static void SetActive(this Component c, bool active)
        {
            c.gameObject.SetActive(active);
        }

        /// <summary>
        /// Check if a GameObject or Component is null or has been destroyed.
        /// Unity's fake null makes standard null checks unreliable.
        /// 
        ///   if (enemy.IsDestroyed())
        ///       return;
        /// </summary>
        public static bool IsDestroyed(this Object obj)
        {
            return obj == null;
        }
    }
}
