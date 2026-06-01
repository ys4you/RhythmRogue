using System;
using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// One concrete effect an event choice applies: change currency, change HP, or grant
    /// a relic. Kept as plain data so events are authored entirely in the Inspector, with
    /// no per-event code. EventOutcomeApplier turns this data into actual state changes.
    ///
    /// A choice can carry several of these (e.g. "lose 10 HP AND gain 50 currency"), so the
    /// type is small and composable rather than one big enum of every combination.
    ///
    /// SOLID:
    ///   S - Describes a single effect. No application logic, no UI.
    ///   O - New effect kinds are added to EventEffectKind + a case in the applier,
    ///       without touching existing authored events.
    /// </summary>
    [Serializable]
    public class EventEffect
    {
        public enum EffectKind
        {
            /// <summary>Add currency (Amount > 0) or remove it (Amount &lt; 0, clamped at 0).</summary>
            Currency,
            /// <summary>Heal (Amount > 0) or damage (Amount &lt; 0) the player by a flat amount.</summary>
            HealthFlat,
            /// <summary>Heal or damage by a percent of max HP. Amount is a percent, e.g. 25 = 25%.</summary>
            HealthPercent,
            /// <summary>Grant a random relic from the pool. Amount = how many (usually 1).</summary>
            GrantRandomRelic,
            /// <summary>Grant a specific relic (SpecificRelic field). Amount ignored.</summary>
            GrantSpecificRelic
        }

        [Tooltip("What this effect does.")]
        public EffectKind Kind = EffectKind.Currency;

        [Tooltip("Magnitude. Meaning depends on Kind: currency amount, flat HP, HP percent (25 = 25%), or relic count. Negatives allowed for currency/health.")]
        public int Amount = 0;

        [Tooltip("Only used when Kind = GrantSpecificRelic.")]
        public RelicData SpecificRelic;
    }

    /// <summary>
    /// One selectable choice in an event: the button label, the effects it applies, and a
    /// short result line shown after choosing. Outcomes can be guaranteed or, for risk
    /// events, this is one branch the screen picks between by chance (see EventChoice.IsRandom).
    /// </summary>
    [Serializable]
    public class EventChoice
    {
        [Tooltip("Button label, e.g. 'Take the offering' or 'Walk away'.")]
        public string Label = "Choose";

        [Tooltip("Effects applied when this choice is taken (all of them, in order).")]
        public EventEffect[] Effects = Array.Empty<EventEffect>();

        [TextArea(2, 3)]
        [Tooltip("Result text shown after the choice resolves, e.g. 'The shadows recede, and you feel stronger.'")]
        public string ResultText = "";
    }
}
