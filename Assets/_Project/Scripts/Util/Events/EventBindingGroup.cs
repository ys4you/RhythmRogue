using System;
using System.Collections.Generic;

namespace RhythmRogue.Util.Events
{
    /// <summary>
    /// Manages a collection of event bindings for bulk disposal.
    /// 
    /// Systems that subscribe to multiple events create a group in Awake,
    /// add bindings to it, and call Dispose() once in OnDestroy.
    /// No risk of forgetting to unsubscribe individual handlers.
    /// 
    /// Usage:
    ///   private EventBindingGroup _bindings;
    /// 
    ///   void OnEnable()
    ///   {
    ///       var bus = EventBusProvider.Instance.Bus;
    ///       _bindings = new EventBindingGroup();
    ///       _bindings.Add&lt;BattleStartedEvent&gt;(bus, OnBattleStarted);
    ///       _bindings.Add&lt;BattleEndedEvent&gt;(bus, OnBattleEnded);
    ///       _bindings.Add&lt;ComboChangedEvent&gt;(bus, OnComboChanged);
    ///   }
    /// 
    ///   void OnDisable()
    ///   {
    ///       _bindings?.Dispose();
    ///   }
    /// </summary>
    public class EventBindingGroup : IDisposable
    {
        private readonly List<IDisposable> _bindings = new();
        private bool _disposed;

        /// <summary>
        /// Subscribe to an event and track the binding for bulk disposal.
        /// </summary>
        /// <typeparam name="T">Event type.</typeparam>
        /// <param name="bus">Event bus to subscribe to.</param>
        /// <param name="handler">Handler to invoke on event.</param>
        /// <returns>This group, for fluent chaining.</returns>
        public EventBindingGroup Add<T>(IEventBus bus, Action<T> handler)
            where T : struct, IEvent
        {
            if (_disposed) throw new ObjectDisposedException(nameof(EventBindingGroup));

            _bindings.Add(new EventBinding<T>(bus, handler));
            return this;
        }

        /// <summary>
        /// Dispose all tracked bindings, unsubscribing every handler.
        /// Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            for (int i = _bindings.Count - 1; i >= 0; i--)
            {
                _bindings[i].Dispose();
            }

            _bindings.Clear();
        }
    }
}
