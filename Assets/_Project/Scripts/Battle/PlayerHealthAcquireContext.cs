using RhythmRogue.Data;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Battle-layer implementation of <see cref="IRelicAcquireContext"/>. Lets relic effects
    /// apply one-time, on-pickup changes (currently Max HP) without the Data layer referencing
    /// battle systems, which would create a dependency cycle.
    ///
    /// Stateless, so a single shared instance is reused (see <see cref="Default"/>).
    /// </summary>
    public sealed class PlayerHealthAcquireContext : IRelicAcquireContext
    {
        /// <summary>Shared reusable instance; the context holds no state.</summary>
        public static readonly PlayerHealthAcquireContext Default = new();

        public void AddMaxHP(int amount)
        {
            var ph = PlayerHealth.Instance;
            if (ph != null) ph.IncreaseMaxHP(amount);
        }
    }
}
