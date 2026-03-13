using UnityEngine;

namespace RhythmRogue.Util.Pooling
{
    /// <summary>
    /// MonoBehaviour wrapper that exposes an ObjectPool in the Unity inspector.
    /// Attach this to a GameObject, assign a prefab, and it handles everything.
    /// 
    /// This is a composition wrapper — it delegates to ObjectPool&lt;T&gt; and
    /// PrefabFactory&lt;T&gt; rather than inheriting from either (favoring
    /// composition over inheritance).
    /// 
    /// Usage:
    ///   public class NotePool : MonoPool&lt;NoteView&gt; { }
    /// Then attach NotePool to a GameObject and assign the note prefab in the inspector.
    /// </summary>
    /// <typeparam name="T">MonoBehaviour component type on the prefab.</typeparam>
    public abstract class MonoPool<T> : MonoBehaviour, IObjectPool<T> where T : MonoBehaviour
    {
        [Header("Pool Settings")]
        [Tooltip("Prefab to instantiate. Must have the target component.")]
        [SerializeField] private T _prefab;

        [Tooltip("Number of objects to create on Awake. Avoids runtime allocations.")]
        [SerializeField] private int _prewarmCount = 10;

        [Tooltip("Maximum total objects (0 = unlimited). Excess releases are destroyed.")]
        [SerializeField] private int _maxSize = 0;

        private ObjectPool<T> _pool;

        /// <summary>
        /// The underlying pool. Accessible for advanced usage or testing.
        /// </summary>
        protected IObjectPool<T> Pool => _pool;

        /// <inheritdoc/>
        public int CountInactive => _pool.CountInactive;

        /// <inheritdoc/>
        public int CountActive => _pool.CountActive;

        /// <inheritdoc/>
        public int CountAll => _pool.CountAll;

        protected virtual void Awake()
        {
            Initialize();
        }

        /// <summary>
        /// Initialize the pool. Safe to call multiple times — subsequent calls are no-ops.
        /// Automatically called in Awake, but can be called earlier if needed.
        /// </summary>
        public void Initialize()
        {
            if (_pool != null) return;

            var factory = CreateFactory();
            _pool = new ObjectPool<T>(factory, _prewarmCount, _maxSize);

            if (_prewarmCount > 0)
            {
                _pool.Prewarm(_prewarmCount);
            }
        }

        /// <summary>
        /// Override to provide a custom factory. Default uses PrefabFactory.
        /// This is the extension point if you need Addressables, sub-factories, etc.
        /// </summary>
        protected virtual IPoolFactory<T> CreateFactory()
        {
            return new PrefabFactory<T>(_prefab, transform);
        }

        /// <inheritdoc/>
        public T Get()
        {
            T item = _pool.Get();
            item.gameObject.SetActive(true);
            return item;
        }

        /// <inheritdoc/>
        public void Release(T item)
        {
            item.gameObject.SetActive(false);
            _pool.Release(item);
        }

        /// <inheritdoc/>
        public void Prewarm(int count) => _pool.Prewarm(count);

        /// <inheritdoc/>
        public void Clear() => _pool.Clear();

        protected virtual void OnDestroy()
        {
            _pool?.Clear();
        }
    }
}
