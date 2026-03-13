using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmRogue.Util.Pooling
{
    /// <summary>
    /// Central registry that maps types to their object pools.
    /// 
    /// Systems that need to spawn pooled objects (chart generator, hit effects,
    /// combo popups) can request a pool by type without holding direct references
    /// to specific pool instances. This keeps spawning logic decoupled from
    /// pool configuration.
    /// 
    /// Register pools during scene init or from MonoPool.Awake.
    /// </summary>
    public class PoolRegistry : Singleton<PoolRegistry>
    {
        private readonly Dictionary<Type, object> _pools = new();

        /// <summary>
        /// Register a pool for a given type.
        /// </summary>
        /// <typeparam name="T">The pooled object type.</typeparam>
        /// <param name="pool">The pool instance to register.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if a pool for this type is already registered.
        /// </exception>
        public void Register<T>(IObjectPool<T> pool) where T : class
        {
            var type = typeof(T);

            if (_pools.ContainsKey(type))
            {
                Debug.LogWarning($"[PoolRegistry] Pool for {type.Name} is already registered. Overwriting.");
            }

            _pools[type] = pool;
        }

        /// <summary>
        /// Unregister a pool for a given type.
        /// Call this when a pool is destroyed (e.g. scene unload).
        /// </summary>
        public void Unregister<T>() where T : class
        {
            _pools.Remove(typeof(T));
        }

        /// <summary>
        /// Try to retrieve a registered pool for the given type.
        /// </summary>
        /// <returns>True if a pool was found.</returns>
        public bool TryGet<T>(out IObjectPool<T> pool) where T : class
        {
            if (_pools.TryGetValue(typeof(T), out object raw))
            {
                pool = raw as IObjectPool<T>;
                return pool != null;
            }

            pool = null;
            return false;
        }

        /// <summary>
        /// Retrieve a registered pool. Throws if not found.
        /// Use TryGet if the pool might not exist.
        /// </summary>
        public IObjectPool<T> Get<T>() where T : class
        {
            if (TryGet<T>(out var pool))
            {
                return pool;
            }

            throw new InvalidOperationException(
                $"[PoolRegistry] No pool registered for type {typeof(T).Name}. " +
                "Did you forget to register it or is the pool scene not loaded?");
        }

        /// <summary>
        /// Clear all registered pools and the registry itself.
        /// </summary>
        public void ClearAll()
        {
            _pools.Clear();
        }
    }
}
