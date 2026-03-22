using UnityEngine;

namespace RhythmRogue.Util
{
    /// <summary>
    /// Generic singleton base class for MonoBehaviours.
    /// Inherit from this to make any class a singleton.
    /// Example: public class MyManager : Singleton<MyManager>
    /// </summary>
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _applicationIsQuitting = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _applicationIsQuitting = false;
        }

        /// <summary>
        /// Access singleton instance
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_applicationIsQuitting)
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
        /// Called when the instance is created
        /// </summary>
        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                DontDestroyOnLoad(gameObject);
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
            _applicationIsQuitting = true;
        }

        /// <summary>
        /// Called when instance is destroyed
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                _applicationIsQuitting = true;
            }
        }
    }
}