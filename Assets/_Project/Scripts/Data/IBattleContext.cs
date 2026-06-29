using System.Collections.Generic;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Data
{
    /// <summary>
    /// One enemy-side note an <see cref="EnemyModifier"/> wants the enemy to play, in the same
    /// beat-grid terms as the rest of the chart. Lane is 0-3, Beat is the song beat it lands on,
    /// and HoldBeats > 0 makes it a hold. Kept to primitives on purpose so the modifier surface
    /// stays free of battle-runtime types.
    /// </summary>
    public readonly struct ModifierNote
    {
        public readonly int Lane;
        public readonly float Beat;
        public readonly float HoldBeats;

        public ModifierNote(int lane, float beat, float holdBeats = 0f)
        {
            Lane = lane;
            Beat = beat;
            HoldBeats = holdBeats;
        }
    }

    /// <summary>
    /// The slice of a battle an <see cref="EnemyModifier"/> is allowed to touch. The battle supplies
    /// the concrete implementation; modifiers depend only on this interface, so a modifier can never
    /// reach into BattleManager and the dependency points one way (the battle knows about modifiers,
    /// modifiers know only this surface). That also keeps EnemyData free of battle-runtime types.
    ///
    /// It exposes timing, the fight's facts, a deterministic RNG, and the few actions modifiers
    /// actually need. Grow it deliberately as new modifiers need more, rather than exposing systems
    /// wholesale.
    /// </summary>
    public interface IBattleContext
    {
        /// <summary>Current song position in beats. Frozen while paused, so beat-keyed work pauses too.</summary>
        float SongBeat { get; }

        /// <summary>True while the song is actively playing (not paused, not pre-song).</summary>
        bool IsSongPlaying { get; }

        /// <summary>True if this fight is a boss, so a modifier can behave differently on bosses.</summary>
        bool IsBoss { get; }

        /// <summary>This fight's difficulty slot (area, depth, tier). Use it to scale a modifier.</summary>
        DifficultyContext Difficulty { get; }

        /// <summary>Deterministic random stream for modifiers. The same seed and run reproduce the
        /// same behaviour, so modifiers stay as replayable as the rest of the game.</summary>
        ISeededRandom Rng { get; }

        int EnemyCurrentHP { get; }
        int EnemyMaxHP { get; }

        /// <summary>Heal the enemy. Used by a last stand to revive it the first time it would die.</summary>
        void HealEnemy(int amount);

        int PlayerCurrentHP { get; }
        int PlayerMaxHP { get; }

        /// <summary>Give the enemy a set of notes to auto-play on its highway. The existing guard and
        /// damage wiring already turns an unblocked enemy note into player damage, so this is all a
        /// counter-attack modifier needs. Replaces any notes currently on the enemy highway.</summary>
        void SetEnemyNotes(IReadOnlyList<ModifierNote> notes);
    }
}
