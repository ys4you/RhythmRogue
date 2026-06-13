using System.Collections.Generic;
using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Data definition for a single relic.
    /// 
    /// Relics are passive modifiers collected during a run that alter
    /// gameplay systems (hit windows, damage, combo, HP, economy).
    /// A relic carries a LIST of effects (see <see cref="Effects"/>); each effect holds its
    /// own typed, named data, so one relic can combine several effects.
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

        [Header("Effects")]
        [Tooltip("What this relic does. A relic can have one or more effects, and each effect " +
                 "carries its own named values. Edit these through the relic inspector's " +
                 "Add Effect menu rather than by hand.")]
        [SerializeReference] public List<RelicEffectDef> Effects = new();

        /// <summary>
        /// Compact one-line badge summarising the relic's effects for cards (e.g. "+5ms",
        /// "+20 max HP"), built by joining each effect's ShortValue. Empty if there are none.
        /// </summary>
        public string ShortEffectSummary
        {
            get
            {
                if (Effects == null || Effects.Count == 0) return "";
                string result = "";
                for (int i = 0; i < Effects.Count; i++)
                {
                    RelicEffectDef e = Effects[i];
                    if (e == null) continue;
                    string s = e.ShortValue;
                    if (string.IsNullOrEmpty(s)) continue;
                    result = result.Length == 0 ? s : result + ", " + s;
                }
                return result;
            }
        }
    }
}
