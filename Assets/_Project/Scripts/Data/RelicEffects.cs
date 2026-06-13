using System;
using UnityEngine;

namespace RhythmRogue.Data
{
    // The concrete relic effects. Each is a small, self-describing unit: its fields ARE its
    // parameters (named for what they do), and it knows how to contribute to battle modifiers
    // and/or act on pickup, plus how to describe itself to a designer. Adding one here makes it
    // appear in the relic inspector's Add Effect menu and in the aggregator automatically.
    //
    // Stat effects (most) override Contribute(). Pickup effects (Max HP) override OnAcquired().

    [Serializable]
    public sealed class WiderPerfectWindowEffect : RelicEffectDef
    {
        [Tooltip("Milliseconds added to the Perfect timing window.")]
        public float bonusMilliseconds = 5f;

        public override string DisplayName => "Wider Perfect Window";
        public override string ShortValue => bonusMilliseconds != 0f ? $"+{bonusMilliseconds:0.##}ms" : "";
        public override string Describe() => $"Widens the Perfect timing window by {bonusMilliseconds:0.##} ms.";
        public override void Contribute(RelicModifiersBuilder b) => b.BonusPerfectWindowMs += bonusMilliseconds;
    }

    [Serializable]
    public sealed class BonusPerfectDamageEffect : RelicEffectDef
    {
        [Tooltip("Extra damage added to each Perfect hit.")]
        public float bonusDamage = 2f;

        public override string DisplayName => "Bonus Perfect Damage";
        public override string ShortValue => bonusDamage != 0f ? $"+{bonusDamage:0.##} dmg" : "";
        public override string Describe() => $"Perfect hits deal {bonusDamage:0.##} extra damage.";
        public override void Contribute(RelicModifiersBuilder b) => b.BonusPerfectDamage += bonusDamage;
    }

    [Serializable]
    public sealed class ComboRateBoostEffect : RelicEffectDef
    {
        [Tooltip("Added to the combo multiplier gained per hit.")]
        public float ratePerHit = 0.05f;

        public override string DisplayName => "Combo Rate Boost";
        public override string ShortValue => ratePerHit != 0f ? $"+{ratePerHit:0.##}/hit" : "";
        public override string Describe() => $"The combo multiplier rises {ratePerHit:0.##} faster per hit.";
        public override void Contribute(RelicModifiersBuilder b) => b.ComboRateBoost += ratePerHit;
    }

    [Serializable]
    public sealed class ComboCapBoostEffect : RelicEffectDef
    {
        [Tooltip("Added to the maximum combo multiplier.")]
        public float capIncrease = 1f;

        public override string DisplayName => "Combo Cap Boost";
        public override string ShortValue => capIncrease != 0f ? $"+{capIncrease:0.##}x cap" : "";
        public override string Describe() => $"Raises the combo multiplier cap by {capIncrease:0.##}x.";
        public override void Contribute(RelicModifiersBuilder b) => b.ComboCapBoost += capIncrease;
    }

    [Serializable]
    public sealed class HealOnComboMilestoneEffect : RelicEffectDef
    {
        [Tooltip("HP healed each time a combo milestone is reached.")]
        [Min(0)] public int healAmount = 3;

        public override string DisplayName => "Heal On Combo Milestone";
        public override string ShortValue => healAmount != 0 ? $"+{healAmount} HP" : "";
        public override string Describe() => $"Heals {healAmount} HP at each combo milestone.";
        public override void Contribute(RelicModifiersBuilder b) => b.HealOnMilestoneHP += healAmount;
    }

    [Serializable]
    public sealed class ReduceMissDamageEffect : RelicEffectDef
    {
        [Tooltip("Flat damage removed from each Miss.")]
        [Min(0)] public int reduction = 2;

        public override string DisplayName => "Reduce Miss Damage";
        public override string ShortValue => reduction != 0 ? $"-{reduction} dmg" : "";
        public override string Describe() => $"Misses deal {reduction} less damage.";
        public override void Contribute(RelicModifiersBuilder b) => b.MissDamageReduction += reduction;
    }

    [Serializable]
    public sealed class CurrencyMultiplierEffect : RelicEffectDef
    {
        [Tooltip("Fractional currency bonus, e.g. 0.25 = +25%. Stacks multiplicatively.")]
        public float bonusFraction = 0.25f;

        public override string DisplayName => "Currency Multiplier";
        public override string ShortValue => bonusFraction != 0f ? $"+{bonusFraction * 100f:0}%" : "";
        public override string Describe() => $"Earn {bonusFraction * 100f:0}% more currency.";
        public override void Contribute(RelicModifiersBuilder b) => b.CurrencyMultiplier *= (1f + bonusFraction);
    }

    [Serializable]
    public sealed class MissShieldEffect : RelicEffectDef
    {
        [Tooltip("Shield charges granted at the start of each battle. Non-final charges halve a Miss; the final charge fully blocks it.")]
        [Min(0)] public int charges = 2;

        public override string DisplayName => "Miss Shield";
        public override string ShortValue => charges != 0 ? $"{charges} shield" : "";
        public override string Describe() => $"Start each battle with {charges} shield charge(s) that absorb Misses (then refresh on a clean hit).";
        public override void Contribute(RelicModifiersBuilder b) => b.MissShieldCharges += charges;
    }

    [Serializable]
    public sealed class MaxHPBoostEffect : RelicEffectDef
    {
        [Tooltip("Permanent max HP added when the relic is picked up.")]
        [Min(0)] public int bonusHP = 20;

        public override string DisplayName => "Max HP Boost";
        public override string ShortValue => bonusHP != 0 ? $"+{bonusHP} max HP" : "";
        public override string Describe() => $"Permanently raises max HP by {bonusHP} when picked up.";
        public override void OnAcquired(IRelicAcquireContext ctx) => ctx.AddMaxHP(bonusHP);
    }
}
