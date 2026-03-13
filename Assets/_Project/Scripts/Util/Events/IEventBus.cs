using System;

namespace RhythmRogue.Util.Events
{
    /// <summary>
    /// Abstraction for a typed publish-subscribe event bus.
    /// 
    /// Publishers call Publish&lt;T&gt;(event) without knowing who listens.
    /// Subscribers call Subscribe&lt;T&gt;(handler) without knowing who publishes.
    /// This fully decouples game systems from each other.
    /// 
    /// Note: This bus is for game-wide events (battle flow, UI updates,
    /// progression). High-frequency per-beat events should use C# events
    /// directly on the Conductor for performance.
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Subscribe a handler to receive events of type T.
        /// </summary>
        /// <typeparam name="T">Event type to listen for.</typeparam>
        /// <param name="handler">Callback invoked when the event is published.</param>
        void Subscribe<T>(Action<T> handler) where T : struct, IEvent;

        /// <summary>
        /// Remove a previously registered handler.
        /// Always unsubscribe when the listener is destroyed or disabled
        /// to prevent stale references and memory leaks.
        /// </summary>
        void Unsubscribe<T>(Action<T> handler) where T : struct, IEvent;

        /// <summary>
        /// Publish an event to all registered handlers of type T.
        /// Handlers are invoked synchronously in registration order.
        /// </summary>
        /// <typeparam name="T">Event type.</typeparam>
        /// <param name="evt">Event data.</param>
        void Publish<T>(T evt) where T : struct, IEvent;

        /// <summary>
        /// Check if any handlers are registered for event type T.
        /// Useful for conditional logic (e.g. skip building event data
        /// if nobody is listening).
        /// </summary>
        bool HasSubscribers<T>() where T : struct, IEvent;

        /// <summary>
        /// Remove all subscribers for all event types.
        /// Call during scene transitions or cleanup.
        /// </summary>
        void Clear();
    }
}
