using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Holds all authored events and picks one for an Event node, seeded so the same run
    /// always rolls the same event at the same node (GDD determinism requirement).
    ///
    /// Mirrors RelicPool's shape. No weighting for now (events are equally likely); a
    /// rarity/weight field can be added later without changing callers.
    ///
    /// Create via: Assets > Create > RhythmRogue > Data > Event Pool
    ///
    /// SOLID:
    ///   S - Owns the event collection + selection only.
    ///   D - Returns EventData abstractions; the screen depends on this, not on concrete events.
    /// </summary>
    [CreateAssetMenu(fileName = "EventPool", menuName = "RhythmRogue/Data/Event Pool")]
    public class EventPool : ScriptableObject
    {
        [SerializeField] private List<EventData> _allEvents = new();

        public IReadOnlyList<EventData> AllEvents => _allEvents;

        /// <summary>
        /// Pick one event at random (seeded). Returns null if the pool is empty so the
        /// caller can fall back gracefully.
        /// </summary>
        public EventData PickOne(ISeededRandom rng)
        {
            if (_allEvents == null || _allEvents.Count == 0) return null;

            // Filter nulls so a missing slot in the Inspector doesn't crash selection.
            var valid = new List<EventData>();
            foreach (var e in _allEvents) if (e != null) valid.Add(e);
            if (valid.Count == 0) return null;

            int index = rng.Range(0, valid.Count);
            return valid[index];
        }
    }
}
