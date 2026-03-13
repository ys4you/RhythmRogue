using UnityEngine;

namespace RhythmRogue.Util.Pooling
{
    /// <summary>
    /// Optional convenience base class for MonoBehaviours that will be pooled.
    /// Provides virtual OnSpawn/OnDespawn hooks so subclasses only override what they need.
    /// 
    /// Not required — you can implement IPoolable directly on any MonoBehaviour.
    /// This just reduces boilerplate for the common case.
    /// </summary>
    public abstract class PoolableMonoBehaviour : MonoBehaviour, IPoolable
    {
        /// <summary>
        /// Called when retrieved from the pool. Override to reset state.
        /// Base implementation does nothing — GameObject activation is
        /// handled by MonoPool.
        /// </summary>
        public virtual void OnSpawn() { }

        /// <summary>
        /// Called when returned to the pool. Override to clean up.
        /// Base implementation does nothing — GameObject deactivation is
        /// handled by MonoPool.
        /// </summary>
        public virtual void OnDespawn() { }
    }
}
