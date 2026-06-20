using UnityEngine;

namespace RhythmRogue.Util
{
    /// <summary>
    /// Process-wide "is the application shutting down" flag for singletons. Kept OFF the generic
    /// Singleton&lt;T&gt; so it can be reset by a RuntimeInitializeOnLoadMethod: that attribute
    /// cannot live on an open generic type (Unity cannot invoke it and throws "containing type is
    /// not fully instantiated" once per closed singleton at startup, which is what spammed the
    /// player log). A single global flag is also more correct than one per T, since quitting is
    /// process-wide. The per-T instance itself needs no reset: a destroyed instance compares
    /// == null (Unity-null), so the getter recreates it on the next play session.
    /// </summary>
    internal static class SingletonState
    {
        public static bool ApplicationIsQuitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => ApplicationIsQuitting = false;
    }

    /// <summary>
    /// Generic singleton base class for MonoBehaviours.
    /// Inherit from this to make any class a singleton.
    /// Example: public class MyManager : Singleton<MyManager>
    /// </summary>
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Access singleton instance
        /// </summary>
        public static T Instance
        {
            get
            {
                if (SingletonState.ApplicationIsQuitting)
                {
                    GameLog.Warn($"[Singleton] Instance of {typeof(T)} already destroyed. Returning null.");
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        // Try to find existing instance in scene
                        _instance = FindFirstObjectByType<T>();

                        if (_instance == null)
                        {
                            // Create new GameObject with the component
                            GameObject singletonObject = new GameObject($"{typeof(T).Name} (Singleton)");
                            _instance = singletonObject.AddComponent<T>();
                            
                            GameLog.Info($"[Singleton] Created new instance of {typeof(T)}");
                        }
                    }

                    return _instance;
                }
            }
        }

        /// <summary>
        /// Whether this singleton survives scene loads. Default true: one instance persists for
        /// the whole game (DontDestroyOnLoad). Override to false for a scene-scoped singleton
        /// (e.g. a per-battle clock) so each scene gets its own fresh instance and no persistent
        /// copy lingers for a reloaded scene's placed copy to collide with.
        /// </summary>
        protected virtual bool Persistent => true;

        /// <summary>
        /// Called when the instance is created
        /// </summary>
        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                if (Persistent) DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                GameLog.Warn($"[Singleton] Duplicate instance of {typeof(T)} found. Destroying duplicate.");
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Called when application quits to prevent creating instances during shutdown
        /// </summary>
        protected virtual void OnApplicationQuit()
        {
            SingletonState.ApplicationIsQuitting = true;
        }

        /// <summary>
        /// Called when instance is destroyed
        /// </summary>
        protected virtual void OnDestroy()
        {
            // Clear the reference if WE were the live instance, so a scene-scoped singleton can be
            // re-created cleanly on the next scene. The application-quitting guard is set only by
            // OnApplicationQuit, never here: setting it on every destroy would wrongly disable the
            // Instance getter the moment a non-persistent singleton's scene unloads.
            if (_instance == this)
                _instance = null;
        }
    }
}