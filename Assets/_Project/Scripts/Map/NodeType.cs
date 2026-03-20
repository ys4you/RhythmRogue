namespace RhythmRogue.Map
{
    /// <summary>
    /// Types of nodes on the run map.
    /// 
    /// Prototype uses: Enemy, Rest, Boss.
    /// Post-prototype adds: Elite, Event, Shop (GDD §5).
    /// </summary>
    public enum NodeType
    {
        Enemy,
        Rest,
        Boss,

        // Post-prototype stubs
        Elite,
        Event,
        Shop
    }
}
