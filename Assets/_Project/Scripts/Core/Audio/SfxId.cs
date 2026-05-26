namespace RhythmRogue.Core.Audio
{
    /// <summary>
    /// Enumerated names for every SFX in the game.
    /// Add new entries here when wiring up new sounds, then assign clips
    /// in the SfxLibrary asset.
    ///
    /// Using an enum (rather than strings) means typos are caught at compile time.
    /// </summary>
    public enum SfxId
    {
        None = 0,

        // UI
        UiHover,
        UiConfirm,
        UiBack,
        UiError,
        UiSelectMajor,    // Reward pick, important menu confirmation

        // Battle
        HitPerfect,
        HitGood,
        HitBad,
        Miss,
        ComboMilestone,
        ComboBreak,
        Heal,
        PlayerHurt,

        // Moments
        Victory,
        Defeat,
        BossIntro,
    }
}
