using System.Collections.Generic;
using UnityEngine;
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

        /// <summary>Heal a LIVING enemy. Does nothing once the enemy has died; a modifier that
        /// consumed a death must use <see cref="ReviveEnemy"/> instead.</summary>
        void HealEnemy(int amount);

        /// <summary>Bring the enemy back at the given HP after this modifier returned true from
        /// <see cref="EnemyModifier.OnEnemyWouldDie"/>. Clears the death latch, so the enemy can
        /// take damage and die again. Only call it from inside a consumed death: reviving a living
        /// enemy is a no-op. Always re-check <see cref="EnemyCurrentHP"/> afterwards and let the
        /// death stand if the revive did not take, so a failed revive cannot leave the fight in an
        /// unkillable state.</summary>
        void ReviveEnemy(int hp);

        int PlayerCurrentHP { get; }
        int PlayerMaxHP { get; }

        /// <summary>
        /// Flash a short headline over the fight ("IT REFUSES TO DIE"). Transient and
        /// non-blocking: the song keeps playing and there is nothing to dismiss. Say WHAT happened
        /// in a few words; the battle decides how it is drawn. Empty text is ignored.
        ///
        /// A mechanic the player cannot perceive is indistinguishable from a bug, so a modifier
        /// that changes the fight should announce itself.
        /// </summary>
        void Announce(string text);

        /// <summary>
        /// Play a one-shot sound authored on the modifier asset itself, so a new modifier can
        /// bring its own audio without touching the shared SFX library. Pitch below 1 drops and
        /// lengthens the clip, which is enough to turn a stock blip into a heavy sting. Null clips
        /// are ignored.
        /// </summary>
        void PlaySound(AudioClip clip, float volumeScale = 1f, float pitch = 1f);

        /// <summary>
        /// How many times the fight has escalated. 0 for the opening phase; a modifier raises it
        /// at a dramatic beat via <see cref="AdvancePhase"/>.
        ///
        /// This is the one thing modifiers share, and it is deliberately just a number. A last
        /// stand can raise the phase without knowing what else reacts, and a counter-attack can
        /// wake up at phase 1 without knowing what raised it. Neither has to reference the other.
        /// </summary>
        int Phase { get; }

        /// <summary>Move the fight into its next phase and return the new value.</summary>
        int AdvancePhase();

        /// <summary>
        /// Swap the rest of the player's chart for the denser one the battle prepared at setup
        /// (see <see cref="EnemyModifier.ChartEscalation"/>). Instant: the chart is already built,
        /// and the swap lands past the spawn horizon so notes currently on screen play out first.
        ///
        /// Returns false when there is nothing to swap to (the modifier declared no escalation,
        /// the enemy has no beat map, the fight is on an authored chart, the song is too near its
        /// end, or it already escalated). Escalating is once per fight.
        /// </summary>
        bool EscalateChart();

        /// <summary>Give the enemy a set of notes to auto-play on its highway. The existing guard and
        /// damage wiring already turns an unblocked enemy note into player damage, so this is all a
        /// counter-attack modifier needs. Replaces any notes currently on the enemy highway.</summary>
        void SetEnemyNotes(IReadOnlyList<ModifierNote> notes);
    }
}
