using UnityEngine;

namespace RhythmRogue.Util.Events
{
    /// <summary>
    /// Singleton MonoBehaviour that provides the global IEventBus instance.
    /// 
    /// Lives across scenes via DontDestroyOnLoad (inherited from Singleton).
    /// Access via EventBusProvider.Instance.Bus from any system.
    /// 
    /// For dependency injection setups, you can bypass this and inject
    /// IEventBus directly — the EventBus class has no Unity dependency.
    /// </summary>
    public class EventBusProvider : Singleton<EventBusProvider>
    {
        private EventBus _bus;

        /// <summary>
        /// The global event bus. All game-wide events route through this.
        /// </summary>
        public IEventBus Bus => _bus;

        protected override void Awake()
        {
            base.Awake();
            _bus = new EventBus();
        }

        protected override void OnDestroy()
        {
            _bus?.Clear();
            base.OnDestroy();
        }
    }
}
