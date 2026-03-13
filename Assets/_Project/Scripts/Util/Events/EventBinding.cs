using System;

namespace RhythmRogue.Util.Events
{
    /// <summary>
    /// Disposable binding that automatically unsubscribes an event handler
    /// when disposed. Prevents the most common event bus bug: forgetting
    /// to unsubscribe in OnDestroy and leaving stale references.
    /// 
    /// Usage:
    ///   _binding = new EventBinding&lt;BattleEndedEvent&gt;(bus, OnBattleEnded);
    ///   // ... later, in OnDestroy:
    ///   _binding.Dispose();
    /// 
    /// Or with EventBindingGroup for bulk cleanup.
    /// </summary>
    /// <typeparam name="T">Event type this binding manages.</typeparam>
    public class EventBinding<T> : IDisposable where T : struct, IEvent
    {
        private IEventBus _bus;
        private Action<T> _handler;
        private bool _disposed;

        /// <summary>
        /// Create a binding that subscribes immediately.
        /// </summary>
        /// <param name="bus">Event bus to subscribe to.</param>
        /// <param name="handler">Handler to invoke on event.</param>
        public EventBinding(IEventBus bus, Action<T> handler)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));

            _bus.Subscribe(handler);
        }

        /// <summary>
        /// Unsubscribe the handler. Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _bus?.Unsubscribe(_handler);
            _bus = null;
            _handler = null;
        }
    }
}
