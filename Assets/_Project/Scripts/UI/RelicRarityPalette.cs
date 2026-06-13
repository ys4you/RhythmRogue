using UnityEngine;
using RhythmRogue.Data;

namespace RhythmRogue.UI
{
    /// <summary>
    /// Single source of truth for relic rarity colours. Card background, accent (border/ring),
    /// and the icon swatch all derive from a relic's rarity here, so relic visuals stay consistent
    /// everywhere and a relic's colour cannot be set by hand, it follows its rarity.
    ///
    /// Replaces the per-relic <c>cardColor</c> field and the duplicate rarity colour switches that
    /// previously lived in RewardPickScreen, ShopScreen, RelicDetailCard and RelicBar.
    /// </summary>
    public static class RelicRarityPalette
    {
        /// <summary>Dark card body colour for a rarity.</summary>
        public static Color Background(RelicRarity rarity) => rarity switch
        {
            RelicRarity.Common => UIHelpers.BgSurface,
            RelicRarity.Uncommon => UIHelpers.Shadow,
            RelicRarity.Rare => UIHelpers.BgLight,
            _ => UIHelpers.BgSurface
        };

        /// <summary>Bright accent (card border, icon ring, rarity label) for a rarity.</summary>
        public static Color Accent(RelicRarity rarity) => rarity switch
        {
            RelicRarity.Common => UIHelpers.RustOrange,
            RelicRarity.Uncommon => UIHelpers.AmberOrange,
            RelicRarity.Rare => UIHelpers.WarmGold,
            _ => UIHelpers.RustOrange
        };

        /// <summary>
        /// Backdrop tint behind the relic icon. Follows rarity (this is what replaced the old
        /// per-relic cardColor). Uses the bright accent so the tile reads as the rarity colour.
        /// </summary>
        public static Color IconSwatch(RelicRarity rarity) => Accent(rarity);
    }
}
