namespace RhythmRogue.Battle
{
    /// <summary>
    /// Fired by DamagePipeline after applying damage.
    /// UI subscribes to show floating damage numbers, HP bar flashes, etc.
    /// </summary>
    public readonly struct DamageResult
    {
        /// <summary>Final damage amount after multiplier.</summary>
        public readonly int Amount;

        /// <summary>The judgment that caused this damage.</summary>
        public readonly Judgment Judgment;

        /// <summary>True if this damaged the player (Miss), false if enemy.</summary>
        public readonly bool IsPlayerDamage;

        /// <summary>Combo multiplier at time of damage (1.0 for misses).</summary>
        public readonly float Multiplier;

        /// <summary>Lane index (0-3).</summary>
        public readonly int Lane;

        public DamageResult(int amount, Judgment judgment, bool isPlayerDamage, float multiplier, int lane)
        {
            Amount = amount;
            Judgment = judgment;
            IsPlayerDamage = isPlayerDamage;
            Multiplier = multiplier;
            Lane = lane;
        }

        public override string ToString()
        {
            string target = IsPlayerDamage ? "Player" : "Enemy";
            return $"[{Amount} dmg → {target} ({Judgment}, {Multiplier:F1}x)]";
        }
    }
}
