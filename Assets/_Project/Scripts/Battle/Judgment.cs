namespace RhythmRogue.Battle
{
    /// <summary>
    /// Hit judgment classifications.
    /// 
    /// Ordered by accuracy (best to worst). The numeric values
    /// are intentional — they match the int field in NoteJudgedEvent
    /// from GameEvents.cs for EventBus compatibility:
    ///   0 = Perfect, 1 = Good, 2 = Bad, 3 = Miss
    /// </summary>
    public enum Judgment
    {
        Perfect = 0,
        Good = 1,
        Bad = 2,
        Miss = 3
    }
}
