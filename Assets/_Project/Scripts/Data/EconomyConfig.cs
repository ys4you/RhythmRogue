using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// All tunable numbers and naming for the run currency live here, in one place,
    /// so the economy can be balanced and even renamed without touching code.
    ///
    /// Design intent (model C from the economy research):
    ///   Each battle pays a flat BASE amount (so even a struggling player can still
    ///   afford to shop and isn't pushed into a death spiral), plus a PERFORMANCE
    ///   BONUS scaled by accuracy (so playing well feels rewarded). The bonus is
    ///   capped, so skill helps but never dwarfs the guaranteed floor.
    ///
    ///   award = round( (base + accuracy01 * maxPerformanceBonus) * currencyMultiplier )
    ///
    /// where base depends on the encounter type (normal / elite / boss), accuracy01
    /// is 0..1 from the AccuracyTracker, and currencyMultiplier comes from relics
    /// (CurrencyMultiplier effect, e.g. Coin Magnet).
    ///
    /// CHANGEABLE BY DESIGN:
    ///   - Name: CurrencyName is a plain string. Rename "Beats" to anything in the
    ///     Inspector; every UI element reads this field, nothing hardcodes the word.
    ///   - Processing: PayoutModel switches between Flat / PerformanceScaled /
    ///     BasePlusBonus without code edits. Default is BasePlusBonus (model C).
    ///   - Numbers: every base, bonus, and shop price is a serialized field.
    ///
    /// SOLID:
    ///   S - Owns economy configuration only. No earning logic, no UI, no storage.
    ///   O - New payout models are added to the enum + a case in EconomyService,
    ///       not by editing existing fields.
    ///   D - Consumed via an abstraction (loaded from Resources or assigned in
    ///       Inspector); systems depend on this data object, not on hardcoded values.
    ///
    /// Loading: systems call EconomyConfig.LoadDefault() which returns the Inspector
    /// asset if assigned, otherwise auto-loads "Configs/DefaultEconomy" from Resources,
    /// matching the fallback convention used by JudgmentConfig / DamageConfig / ComboConfig.
    /// </summary>
    [CreateAssetMenu(fileName = "DefaultEconomy", menuName = "RhythmRogue/EconomyConfig")]
    public class EconomyConfig : ScriptableObject
    {
        /// <summary>How a battle's currency award is computed.</summary>
        public enum PayoutModel
        {
            /// <summary>Flat base amount only. Performance ignored. (Research model A.)</summary>
            Flat,
            /// <summary>Base amount scaled entirely by accuracy. High risk for weak players. (Research model B.)</summary>
            PerformanceScaled,
            /// <summary>Flat base + accuracy-scaled bonus on top, bonus capped. (Research model C, recommended.)</summary>
            BasePlusBonus
        }

        [Header("Identity (rename freely - UI reads these)")]
        [Tooltip("Display name of the currency. Default 'Beats'. Changing this here changes it everywhere in the UI.")]
        public string CurrencyName = "Beats";

        [Tooltip("Short symbol/abbreviation shown next to amounts, e.g. a coin glyph or 'B'. Leave blank to show the number alone.")]
        public string CurrencySymbol = "";

        [Header("Payout Model")]
        [Tooltip("How battle awards are computed. BasePlusBonus = flat floor + capped accuracy bonus (recommended).")]
        public PayoutModel Model = PayoutModel.BasePlusBonus;

        [Header("Base Awards (flat floor per encounter)")]
        [Tooltip("Currency granted for winning a standard enemy battle, before performance bonus and relic multipliers.")]
        public int NormalBattleBase = 20;

        [Tooltip("Currency granted for winning an elite battle. Elites are the highest per-node income.")]
        public int EliteBattleBase = 40;

        [Tooltip("Currency granted for defeating a boss (on top of the guaranteed rare reward pick).")]
        public int BossBattleBase = 100;

        [Header("Performance Bonus (model C / B)")]
        [Tooltip("Max bonus added at 100% accuracy for a NORMAL battle. Scales linearly with accuracy from 0 to this.")]
        public int NormalPerformanceBonus = 10;

        [Tooltip("Max bonus added at 100% accuracy for an ELITE battle.")]
        public int ElitePerformanceBonus = 20;

        [Tooltip("Max bonus added at 100% accuracy for a BOSS battle.")]
        public int BossPerformanceBonus = 50;

        [Header("Starting Balance")]
        [Tooltip("Currency the player begins each run with. Slay the Spire starts ~99; we start lower since runs are shorter.")]
        public int StartingCurrency = 0;

        [Header("Shop Prices (used later when the shop exists)")]
        [Tooltip("Reference price for a common relic in the shop. Defined now so balance lives in one asset.")]
        public int CommonRelicPrice = 50;
        [Tooltip("Reference price for an uncommon relic in the shop.")]
        public int UncommonRelicPrice = 90;
        [Tooltip("Reference price for a rare relic in the shop.")]
        public int RareRelicPrice = 150;
        [Tooltip("Reference price for a single-use consumable in the shop.")]
        public int ConsumablePrice = 30;
        [Tooltip("Reference price for HP restoration at the shop.")]
        public int HealPrice = 40;

        // -----------------------------------------------------------------
        // Loading (mirrors JudgmentConfig / DamageConfig / ComboConfig pattern)
        // -----------------------------------------------------------------

        private const string ResourcePath = "Configs/DefaultEconomy";
        private static EconomyConfig _cached;

        /// <summary>
        /// Returns a usable EconomyConfig. Prefers the supplied Inspector reference;
        /// if null, loads "Configs/DefaultEconomy" from Resources; if that is also
        /// missing, creates a transient default in memory so the game never hard-fails
        /// on a missing asset (with a warning so it gets noticed).
        /// </summary>
        public static EconomyConfig Resolve(EconomyConfig inspectorAssigned)
        {
            if (inspectorAssigned != null) return inspectorAssigned;
            return LoadDefault();
        }

        /// <summary>Load the default economy config from Resources, caching the result.</summary>
        public static EconomyConfig LoadDefault()
        {
            if (_cached != null) return _cached;

            _cached = Resources.Load<EconomyConfig>(ResourcePath);
            if (_cached == null)
            {
                // Transient fallback so a missing asset doesn't break the run. The
                // values match the serialized defaults above.
                _cached = CreateInstance<EconomyConfig>();
                Util.GameLog.Warn($"[EconomyConfig] No asset at Resources/{ResourcePath}. Using in-memory defaults. " +
                                  "Create one via Assets > Create > RhythmRogue > EconomyConfig and place it there to tune the economy.");
            }
            return _cached;
        }
    }
}
