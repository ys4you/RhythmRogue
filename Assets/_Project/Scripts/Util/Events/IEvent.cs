namespace RhythmRogue.Util.Events
{
    /// <summary>
    /// Marker interface for all game events routed through the EventBus.
    /// 
    /// Events are plain structs carrying data — no logic, no dependencies.
    /// Using structs avoids GC allocations on every publish, which matters
    /// for events that fire frequently (combo updates, score changes).
    /// 
    /// Example:
    ///   public struct BattleStartedEvent : IEvent
    ///   {
    ///       public int EnemyId;
    ///       public float Bpm;
    ///   }
    /// </summary>
    public interface IEvent { }
}
