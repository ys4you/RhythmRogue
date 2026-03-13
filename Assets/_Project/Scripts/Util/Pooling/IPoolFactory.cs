namespace RhythmRogue.Util.Pooling
{
    /// <summary>
    /// Factory abstraction for creating and destroying pooled objects.
    /// Separates creation logic from pool management (Single Responsibility).
    /// 
    /// The pool calls Create() when it needs a new object and
    /// Destroy() when clearing. This lets you swap between
    /// prefab instantiation, plain C# constructors, addressables, etc.
    /// without modifying the pool itself (Open/Closed).
    /// </summary>
    /// <typeparam name="T">Type of object this factory produces.</typeparam>
    public interface IPoolFactory<T> where T : class
    {
        /// <summary>
        /// Create a new instance of T.
        /// </summary>
        T Create();

        /// <summary>
        /// Destroy an instance of T permanently.
        /// Called when the pool is cleared or when trimming excess objects.
        /// </summary>
        void Destroy(T item);
    }
}
