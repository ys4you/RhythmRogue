using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Data asset for a single relic.
    /// 
    /// Relics are temporary run upgrades acquired from post-battle
    /// reward picks. Each relic has one effect that modifies a
    /// gameplay config value. Multiple relics of the same effect
    /// type stack additively.
    /// 
    /// Create via: Assets → Create → RhythmRogue → RelicData
    /// 
    /// SOLID: Pure data container (S). New relics are new assets,
    /// not code changes (O). RelicApplier reads these without
    /// knowing how many exist (D).
    /// </summary>
    [CreateAssetMenu(fileName = "NewRelic", menuName = "RhythmRogue/RelicData")]
    public class RelicData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier. Used for save/unlock tracking.")]
        public string relicId;

        [Tooltip("Display name shown in reward pick UI.")]
        public string relicName;

        [Tooltip("Short description of what this relic does.")]
        [TextArea(2, 4)]
        public string description;

        [Header("Rarity")]
        [Tooltip("Affects weighted selection in reward pools.")]
        public RelicRarity rarity = RelicRarity.Common;

        [Header("Effect")]
        [Tooltip("Which gameplay system this relic modifies.")]
        public RelicEffect effect;

        [Tooltip("Numeric modifier. Interpretation depends on effect type.")]
        public float floatValue;

        [Tooltip("Integer modifier for effects that need whole numbers (HP, damage).")]
        public int intValue;

        [Header("Visuals")]
        [Tooltip("Icon shown in reward pick and relic inventory. Optional for prototype.")]
        public Sprite icon;

        [Tooltip("Color tint for the relic card in the reward UI.")]
        public Color cardColor = new Color(0.3f, 0.3f, 0.4f);
    }

    /// <summary>
    /// Relic rarity tier. Affects reward pool weighting.
    /// </summary>
    public enum RelicRarity
    {
        Common,
        Uncommon,
        Rare
    }
}
