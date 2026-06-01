using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// A single authored event encounter: a title, flavor text, and a set of choices.
    /// This is the changeable content unit, authored entirely in the Inspector like
    /// RelicData. Add new events by creating more of these assets and putting them in
    /// an EventPool; no code changes needed.
    ///
    /// The dark/occult tone lives here, in the writing: an event is a short atmospheric
    /// beat (a shrine, a wandering figure, a cursed offering) with risk/reward choices
    /// that touch the systems the run already has (currency, HP, relics).
    ///
    /// Create via: Assets > Create > RhythmRogue > Data > Event
    ///
    /// SOLID:
    ///   S - Pure data definition of one event. No selection, application, or UI logic.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEvent", menuName = "RhythmRogue/Data/Event")]
    public class EventData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Event title shown at the top, e.g. 'A Hollow Shrine'.")]
        public string eventTitle = "An Encounter";

        [TextArea(3, 6)]
        [Tooltip("Flavor / setup text describing the scene and the decision facing the player.")]
        public string flavorText = "";

        [Header("Display")]
        [Tooltip("Accent color for the event title. Defaults to the warm gold if left white-ish.")]
        public Color accentColor = Color.white;

        [Header("Choices")]
        [Tooltip("The choices presented to the player. 1-3 works best on screen.")]
        public EventChoice[] choices = new EventChoice[0];
    }
}
