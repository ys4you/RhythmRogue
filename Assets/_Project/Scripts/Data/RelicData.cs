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

        [Tooltip("Relic icon sprite. If left empty, a shared placeholder is used (the map's " +
                 "event '?' icon) so the relic still shows something until real art exists.")]
        public Sprite icon;

        /// <summary>
        /// The icon to display for this relic. Returns the assigned <see cref="icon"/> if set,
        /// otherwise a shared placeholder loaded from Resources (the same '?' sprite the map
        /// uses for Event nodes). Cached statically so the placeholder is loaded only once.
        /// </summary>
        public Sprite ResolvedIcon => icon != null ? icon : PlaceholderIcon;

        private static Sprite _placeholderIcon;
        private static bool _placeholderLoaded;

        /// <summary>
        /// Shared placeholder icon: the map's event node sprite (Resources/MapIcons/node_event).
        /// Loaded lazily and cached. May be null if that sprite doesn't exist, callers must
        /// handle a null icon (e.g. fall back to a coloured swatch).
        /// </summary>
        public static Sprite PlaceholderIcon
        {
            get
            {
                if (!_placeholderLoaded)
                {
                    _placeholderIcon = Resources.Load<Sprite>("MapIcons/node_event");
                    _placeholderLoaded = true;
                }
                return _placeholderIcon;
            }
        }

        [Header("Effect")]
        public RelicEffect effect;

        [Tooltip("Integer value for effects that use whole numbers (HP, damage reduction).")]
        public int intValue;

        [Tooltip("Float value for effects that use decimals (timing ms, multiplier rates).")]
        public float floatValue;
    }
}
