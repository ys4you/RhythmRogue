using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Data definition for a single relic.
    /// 
    /// Relics are passive modifiers collected during a run that alter
    /// gameplay systems (hit windows, damage, combo, HP, economy).
    /// Each relic has exactly one effect with a numeric value.
    /// 
    /// Create via: Assets → Create → RhythmRogue → Data → Relic
    /// </summary>
    [CreateAssetMenu(fileName = "NewRelic", menuName = "RhythmRogue/Data/Relic")]
    public class RelicData : ScriptableObject
    {
        [Header("Identity")]
        public string relicName = "New Relic";

        [TextArea(2, 4)]
        [Tooltip("Mechanical description of what the relic does.")]
        public string description = "";

        [TextArea(2, 3)]
        [Tooltip("Optional atmospheric flavor line shown in italics on the detail card. Purely cosmetic.")]
        public string flavorText = "";

        public RelicRarity rarity = RelicRarity.Common;

        [Header("Display")]
        [Tooltip("Accent color shown on the relic card icon area.")]
        public Color cardColor = Color.white;

        [Header("Effect")]
        public RelicEffect effect;

        [Tooltip("Integer value for effects that use whole numbers (HP, damage reduction).")]
        public int intValue;

        [Tooltip("Float value for effects that use decimals (timing ms, multiplier rates).")]
        public float floatValue;
    }
}
