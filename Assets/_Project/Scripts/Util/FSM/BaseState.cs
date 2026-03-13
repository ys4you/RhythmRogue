namespace RhythmRogue.Util.FSM
{
    /// <summary>
    /// Convenience base class for states.
    /// 
    /// Provides virtual no-op implementations of all lifecycle methods
    /// so concrete states only override what they need. Most states
    /// don't use FixedUpdate, many don't need Exit — this saves
    /// boilerplate without sacrificing flexibility.
    /// 
    /// Not required — you can implement IState&lt;TKey&gt; directly
    /// if you prefer or need multiple inheritance.
    /// </summary>
    /// <typeparam name="TKey">State identifier type (typically an enum).</typeparam>
    public abstract class BaseState<TKey> : IState<TKey>
    {
        /// <inheritdoc/>
        public TKey Key { get; }

        /// <summary>
        /// Create a state with the given key.
        /// </summary>
        protected BaseState(TKey key)
        {
            Key = key;
        }

        /// <inheritdoc/>
        public virtual void Enter(TKey previousState) { }

        /// <inheritdoc/>
        public virtual void Update() { }

        /// <inheritdoc/>
        public virtual void FixedUpdate() { }

        /// <inheritdoc/>
        public virtual void Exit(TKey nextState) { }
    }
}
