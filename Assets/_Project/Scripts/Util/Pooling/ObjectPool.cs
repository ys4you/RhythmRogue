using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmRogue.Util.Pooling
{
    /// <summary>
    /// Generic object pool that works with any class type.
    /// 
    /// SOLID breakdown:
    /// - S: Only manages the pool lifecycle (get/release/prewarm/clear).
    /// - O: Extensible via IPoolFactory and IPoolable — no need to modify this class.
    /// - L: Any T that satisfies the constraint works identically.
    /// - I: Consumers see only IObjectPool&lt;T&gt;; factory logic is in IPoolFactory&lt;T&gt;.
    /// - D: Depends on IPoolFactory&lt;T&gt; abstraction, not concrete creation logic.
    /// </summary>
    /// <typeparam name="T">Type of object to pool. Must be a reference type.</typeparam>
    public class ObjectPool<T> : IObjectPool<T> where T : class
    {
        private readonly Stack<T> _inactive;
        private readonly HashSet<T> _active;
        private readonly IPoolFactory<T> _factory;
        private readonly int _maxSize;

        /// <inheritdoc/>
        public int CountInactive => _inactive.Count;

        /// <inheritdoc/>
        public int CountActive => _active.Count;

        /// <inheritdoc/>
        public int CountAll => CountActive + CountInactive;

        /// <summary>
        /// Create a new object pool.
        /// </summary>
        /// <param name="factory">Factory responsible for creating and destroying instances.</param>
        /// <param name="initialCapacity">Initial internal collection capacity (not pre-warmed count).</param>
        /// <param name="maxSize">
        /// Hard cap on total pooled objects. Objects released beyond this limit are destroyed.
        /// Use 0 or negative for unlimited.
        /// </param>
        public ObjectPool(IPoolFactory<T> factory, int initialCapacity = 16, int maxSize = 0)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _maxSize = maxSize;
            _inactive = new Stack<T>(initialCapacity);
            _active = new HashSet<T>(initialCapacity);
        }

        /// <inheritdoc/>
        public T Get()
        {
            T item = _inactive.Count > 0
                ? _inactive.Pop()
                : _factory.Create();

            _active.Add(item);

            if (item is IPoolable poolable)
            {
                poolable.OnSpawn();
            }

            return item;
        }

        /// <inheritdoc/>
        public void Release(T item)
        {
            if (item == null)
            {
                GameLog.Warn("[ObjectPool] Attempted to release a null object.");
                return;
            }

            if (!_active.Remove(item))
            {
                GameLog.Warn("[ObjectPool] Attempted to release an object not owned by this pool.");
                return;
            }

            if (item is IPoolable poolable)
            {
                poolable.OnDespawn();
            }

            // If we're over the max size, destroy instead of returning to pool
            if (_maxSize > 0 && CountAll >= _maxSize)
            {
                _factory.Destroy(item);
                return;
            }

            _inactive.Push(item);
        }

        /// <inheritdoc/>
        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (_maxSize > 0 && CountAll >= _maxSize) break;

                T item = _factory.Create();

                // Immediately run the full spawn/despawn cycle so the object
                // is in a clean "pooled" state
                if (item is IPoolable poolable)
                {
                    poolable.OnSpawn();
                    poolable.OnDespawn();
                }

                _inactive.Push(item);
            }
        }

        /// <inheritdoc/>
        public void Clear()
        {
            foreach (T item in _active)
            {
                if (item is IPoolable poolable)
                {
                    poolable.OnDespawn();
                }

                _factory.Destroy(item);
            }

            foreach (T item in _inactive)
            {
                _factory.Destroy(item);
            }

            _active.Clear();
            _inactive.Clear();
        }
    }
}
