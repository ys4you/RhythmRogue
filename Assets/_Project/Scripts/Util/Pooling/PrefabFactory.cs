using UnityEngine;

namespace RhythmRogue.Util.Pooling
{
    /// <summary>
    /// Factory that creates pooled objects by instantiating a Unity prefab.
    /// Handles parenting under a container transform to keep the hierarchy tidy.
    /// 
    /// This is the standard factory for any MonoBehaviour-based pool
    /// (notes, hit effects, particles, UI popups, etc.)
    /// </summary>
    /// <typeparam name="T">
    /// MonoBehaviour component type. The prefab must have this component attached.
    /// </typeparam>
    public class PrefabFactory<T> : IPoolFactory<T> where T : MonoBehaviour
    {
        private readonly T _prefab;
        private readonly Transform _parent;

        /// <summary>
        /// Create a prefab factory.
        /// </summary>
        /// <param name="prefab">Prefab to instantiate. Must have component T.</param>
        /// <param name="parent">
        /// Optional parent transform for instantiated objects.
        /// Keeps the hierarchy clean (e.g. a "NotePool" container).
        /// </param>
        public PrefabFactory(T prefab, Transform parent = null)
        {
            _prefab = prefab;
            _parent = parent;
        }

        /// <inheritdoc/>
        public T Create()
        {
            T instance = Object.Instantiate(_prefab, _parent);
            instance.gameObject.SetActive(false);
            return instance;
        }

        /// <inheritdoc/>
        public void Destroy(T item)
        {
            if (item != null && item.gameObject != null)
            {
                Object.Destroy(item.gameObject);
            }
        }
    }
}
