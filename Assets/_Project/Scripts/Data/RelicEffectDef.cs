using System;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Base type for a single relic effect.
    ///
    /// A <see cref="RelicData"/> holds a LIST of these, serialized polymorphically via
    /// [SerializeReference]. That means one relic can carry several effects, and each effect
    /// carries its own typed, named data, instead of the old shared (and ambiguous) intValue /
    /// floatValue pair where you had to know which field a given effect read.
    ///
    /// To add a new effect: create a new [Serializable] subclass (see RelicEffects.cs), give it
    /// its own fields, and implement the members below. It then appears automatically in the
    /// relic inspector's "Add Effect" menu and is picked up by the aggregator. No switch
    /// statement to edit anywhere.
    ///
    /// Effects act at one of two moments:
    ///   - Battle start: stat effects override <see cref="Contribute"/> to add to RelicModifiers.
    ///   - On pickup: one-time effects (e.g. Max HP) override <see cref="OnAcquired"/>.
    ///
    /// SOLID:
    ///   S — Each effect is one self-contained unit of behaviour + its data.
    ///   O — New behaviour is a new subclass; existing effects and the aggregator never change.
    ///   L — Every subclass is usable anywhere a RelicEffectDef is expected (default no-op hooks).
    /// </summary>
    [Serializable]
    public abstract class RelicEffectDef
    {
        /// <summary>Designer-facing name shown in the inspector's Add Effect menu and effect header.</summary>
        public abstract string DisplayName { get; }

        /// <summary>Compact value badge for relic cards, e.g. "+5ms", "+20 max HP". Empty when zero/none.</summary>
        public abstract string ShortValue { get; }

        /// <summary>Plain-language sentence describing the effect, shown live in the inspector.</summary>
        public abstract string Describe();

        /// <summary>
        /// Battle-start stat contribution: a stat effect adds its numbers to the builder.
        /// Default is a no-op, for effects that act only on pickup.
        /// </summary>
        public virtual void Contribute(RelicModifiersBuilder builder) { }

        /// <summary>
        /// One-time effect applied the moment the relic is acquired (reward pick or shop buy),
        /// e.g. raising max HP. Default is a no-op.
        /// </summary>
        public virtual void OnAcquired(IRelicAcquireContext context) { }
    }

    /// <summary>
    /// Abstraction the relic-acquire flow passes to <see cref="RelicEffectDef.OnAcquired"/> so an
    /// effect can cause a one-time, on-pickup change without the Data layer depending on battle
    /// systems (which would be a dependency cycle). Implemented in the Battle layer
    /// (PlayerHealthAcquireContext).
    /// </summary>
    public interface IRelicAcquireContext
    {
        /// <summary>Permanently increase the player's max HP by the given amount.</summary>
        void AddMaxHP(int amount);
    }
}
