namespace RhythmRogue.Util.Pooling
{
    /// <summary>
    /// Abstraction for an object pool.
    /// Consumers depend on this interface, allowing different pool
    /// implementations (MonoBehaviour pools, pure C# pools, etc.)
    /// to be swapped without changing calling code.
    /// </summary>
    /// <typeparam name="T">Type of object managed by this pool.</typeparam>
    public interface IObjectPool<T> where T : class
    {
        /// <summary>
        /// Current number of inactive objects available in the pool.
        /// </summary>
        int CountInactive { get; }

        /// <summary>
        /// Current number of active objects spawned from this pool.
        /// </summary>
        int CountActive { get; }

        /// <summary>
        /// Total objects owned by this pool (active + inactive).
        /// </summary>
        int CountAll { get; }

        /// <summary>
        /// Retrieve an object from the pool. Creates a new one if empty.
        /// </summary>
        T Get();

        /// <summary>
        /// Return an object to the pool.
        /// </summary>
        void Release(T item);

        /// <summary>
        /// Pre-warm the pool by creating objects up front.
        /// Call during loading screens or scene init to avoid runtime allocations.
        /// </summary>
        void Prewarm(int count);

        /// <summary>
        /// Destroy all objects and reset the pool.
        /// </summary>
        void Clear();
    }
}
