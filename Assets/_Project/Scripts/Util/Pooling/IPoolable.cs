namespace RhythmRogue.Util.Pooling
{
    /// <summary>
    /// Contract for objects managed by an object pool.
    /// Implement this on any component that needs setup/teardown
    /// when retrieved from or returned to a pool.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// Called when the object is retrieved from the pool.
        /// Use for initialization, enabling visuals, resetting state, etc.
        /// </summary>
        void OnSpawn();

        /// <summary>
        /// Called when the object is returned to the pool.
        /// Use for cleanup, disabling visuals, cancelling coroutines, etc.
        /// </summary>
        void OnDespawn();
    }
}
