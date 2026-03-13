using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmRogue.Util.Events
{
    /// <summary>
    /// Typed event bus implementation.
    /// 
    /// Uses a dictionary keyed by event Type, with each entry holding
    /// a strongly-typed handler list. The generic methods ensure compile-time
    /// type safety — you can't accidentally subscribe a BattleEndedEvent
    /// handler to a ComboChangedEvent channel.
    /// 
    /// Performance notes:
    /// - Publish iterates a List&lt;Action&lt;T&gt;&gt; with no boxing or allocation.
    /// - Events are structs passed by value — no GC pressure on publish.
    /// - Handler lookup is a single Dictionary.TryGetValue per publish.
    /// - Safe against subscribe/unsubscribe during iteration via snapshot copy.
    /// 
    /// SOLID breakdown:
    /// - S: Only routes events from publishers to subscribers.
    /// - O: New event types require zero changes to this class.
    /// - L: Substitutable anywhere IEventBus is expected.
    /// - I: Single focused interface, no forced dependencies.
    /// - D: Publishers and subscribers depend on IEventBus, not this class.
    /// </summary>
    public class EventBus : IEventBus
    {
        /// <summary>
        /// Type-erased wrapper around a List&lt;Action&lt;T&gt;&gt;.
        /// Allows storing all handler lists in a single Dictionary&lt;Type, object&gt;.
        /// </summary>
        private class HandlerList<T> where T : struct, IEvent
        {
            private readonly List<Action<T>> _handlers = new();
            private bool _isPublishing;

            // Deferred modifications to avoid mutating during iteration
            private readonly List<Action<T>> _pendingAdds = new();
            private readonly List<Action<T>> _pendingRemoves = new();

            public int Count => _handlers.Count;

            public void Add(Action<T> handler)
            {
                if (_isPublishing)
                {
                    _pendingAdds.Add(handler);
                }
                else
                {
                    _handlers.Add(handler);
                }
            }

            public void Remove(Action<T> handler)
            {
                if (_isPublishing)
                {
                    _pendingRemoves.Add(handler);
                }
                else
                {
                    _handlers.Remove(handler);
                }
            }

            public void Publish(T evt)
            {
                _isPublishing = true;

                for (int i = 0; i < _handlers.Count; i++)
                {
                    try
                    {
                        _handlers[i].Invoke(evt);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }

                _isPublishing = false;
                ApplyPendingChanges();
            }

            public void Clear()
            {
                _handlers.Clear();
                _pendingAdds.Clear();
                _pendingRemoves.Clear();
            }

            private void ApplyPendingChanges()
            {
                foreach (var handler in _pendingRemoves)
                {
                    _handlers.Remove(handler);
                }
                _pendingRemoves.Clear();

                foreach (var handler in _pendingAdds)
                {
                    _handlers.Add(handler);
                }
                _pendingAdds.Clear();
            }
        }

        private readonly Dictionary<Type, object> _handlerMap = new();

        /// <inheritdoc/>
        public void Subscribe<T>(Action<T> handler) where T : struct, IEvent
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            GetOrCreateList<T>().Add(handler);
        }

        /// <inheritdoc/>
        public void Unsubscribe<T>(Action<T> handler) where T : struct, IEvent
        {
            if (handler == null) return;

            if (_handlerMap.TryGetValue(typeof(T), out object raw))
            {
                ((HandlerList<T>)raw).Remove(handler);
            }
        }

        /// <inheritdoc/>
        public void Publish<T>(T evt) where T : struct, IEvent
        {
            if (_handlerMap.TryGetValue(typeof(T), out object raw))
            {
                ((HandlerList<T>)raw).Publish(evt);
            }
        }

        /// <inheritdoc/>
        public bool HasSubscribers<T>() where T : struct, IEvent
        {
            if (_handlerMap.TryGetValue(typeof(T), out object raw))
            {
                return ((HandlerList<T>)raw).Count > 0;
            }

            return false;
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _handlerMap.Clear();
        }

        private HandlerList<T> GetOrCreateList<T>() where T : struct, IEvent
        {
            var type = typeof(T);

            if (!_handlerMap.TryGetValue(type, out object raw))
            {
                raw = new HandlerList<T>();
                _handlerMap[type] = raw;
            }

            return (HandlerList<T>)raw;
        }
    }
}
