using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.Core;
using RhythmRogue.Core.Audio;
using RhythmRogue.Data;
using RhythmRogue.Battle;
using RhythmRogue.Util;
using RhythmRogue.Util.Random;
using RhythmRogue.UI.Navigation;

namespace RhythmRogue.UI
{
    /// <summary>
    /// Shop scene controller. The primary currency sink: the player spends run currency
    /// (earned in battle) on relics. First version sells relics only; browse freely and
    /// leave via a Leave button when done.
    ///
    /// Reuses established patterns:
    ///   - Stock is generated from the shared RelicPool, seeded deterministically from the
    ///     run seed + battle count, so the same seed yields the same shop (GDD requirement).
    ///   - Cards mirror the reward screen layout (rarity color, name, description, value
    ///     badge) plus a price tag and a buy button. Focus shown via UIFocusFrame, not
    ///     ColorTint, so card backgrounds keep their true rarity color.
    ///   - Prices come from EconomyConfig by rarity, so balancing lives in one asset.
    ///   - Spending goes through RunState.TrySpendCurrency, which refuses if unaffordable.
    ///
    /// SOLID:
    ///   S - Scene presentation + buy flow only. Pricing lives in EconomyConfig, stock in
    ///       RelicPool, spending in RunState. This class wires them to UI.
    ///   O - New stock kinds (consumables, heals) added as new card builders + sources,
    ///       not by rewriting the relic path.
    ///   D - Depends on RunState / RelicPool / EconomyConfig abstractions, not concretions.
    /// </summary>
    public class ShopScreen : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RunState _runState;
        [SerializeField] private RelicPool _relicPool;

        [Header("Stock")]
        [Tooltip("How many relics the shop offers.")]
        [SerializeField] private int _stockCount = 3;

        private Canvas _canvas;
        private RectTransform _canvasRT;
        private Button _leaveButton;
        private UIFocusSetter _focusSetter;
        private UICancelHandler _cancelHandler;

        // Per-card runtime state. One entry per relic on sale.
        private class ShopItem
        {
            public RelicData Relic;
            public int Price;
            public bool Purchased;
            public Button BuyButton;
            public Text PriceText;
            public GameObject SoldOverlay;
        }
        private readonly List<ShopItem> _items = new();

        private void Start()
        {
            if (_runState == null) { GameLog.Error("[ShopScreen] No RunState assigned."); return; }

            GenerateStock();
            CreateUI();

            // Shops sit on the map, so keep the map's shamanic ambient playing. Idempotent.
            MusicManager.Instance.Play(MusicTrack.MapShamanic);
        }

        /// <summary>
        /// Build the shop's relic stock. Seeded from the run seed + battle count via the
        /// Shop domain, so the same run always stocks the same shop. Prices each relic by
        /// rarity from EconomyConfig.
        /// </summary>
        private void GenerateStock()
        {
            _items.Clear();
            if (_relicPool == null) { GameLog.Warn("[ShopScreen] No RelicPool assigned; shop will be empty."); return; }

            ISeededRandom rng = _runState.RunSeed != null
                ? _runState.RunSeed.GetRandom(RandomDomain.Shop, _runState.BattlesWon)
                : new SeededRandom(System.Environment.TickCount);

            List<RelicData> picks = _relicPool.PickOptions(rng, _stockCount, _runState.ActiveRelics);
            EconomyConfig econ = _runState.Economy;

            foreach (var relic in picks)
            {
                _items.Add(new ShopItem
                {
                    Relic = relic,
                    Price = PriceFor(relic, econ),
                    Purchased = false
                });
            }
        }

        private static int PriceFor(RelicData relic, EconomyConfig econ)
        {
            if (econ == null) return 50;
            return relic.rarity switch
            {
                RelicRarity.Common => econ.CommonRelicPrice,
                RelicRarity.Uncommon => econ.UncommonRelicPrice,
                RelicRarity.Rare => econ.RareRelicPrice,
                _ => econ.CommonRelicPrice
            };
        }

        private void OnBuy(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            ShopItem item = _items[index];
            if (item.Purchased) return;

            // TrySpendCurrency refuses and changes nothing if the player can't afford it.
            if (!_runState.TrySpendCurrency(item.Price))
            {
                // Not enough currency: play the error cue and leave everything as-is.
                var am = AudioManager.Instance;
                if (am != null) am.PlayIfRegistered(SfxId.UiError);
                return;
            }

            // Purchase succeeds: grant the relic through the shared acquire path (adds it and
            // applies any one-time on-pickup effects, e.g. Max HP).
            item.Purchased = true;
            _runState.AcquireRelic(item.Relic, PlayerHealthAcquireContext.Default);

            var mgr = AudioManager.Instance;
            if (mgr != null) mgr.PlayIfRegistered(SfxId.UiConfirm);

            GameLog.Info($"[ShopScreen] Bought {item.Relic.relicName} for {item.Price}. Remaining: {_runState.Currency}.");

            MarkSold(item);
            RefreshAffordability();
        }

        /// <summary>Grey out a purchased card and show its SOLD overlay.</summary>
        private void MarkSold(ShopItem item)
        {
            if (item.BuyButton != null)
            {
                item.BuyButton.interactable = false;
                var img = item.BuyButton.GetComponent<Image>();
                if (img != null) img.color = UIHelpers.Shadow;
            }
            if (item.SoldOverlay != null) item.SoldOverlay.SetActive(true);
        }

        /// <summary>
        /// Update each unpurchased buy button so unaffordable items read as disabled.
        /// Keeps the buttons present (so the player sees what they could save toward) but
        /// non-interactable and dimmed when they can't afford them.
        /// </summary>
        private void RefreshAffordability()
        {
            foreach (var item in _items)
            {
                if (item.Purchased || item.BuyButton == null) continue;
                bool canAfford = _runState.Currency >= item.Price;
                item.BuyButton.interactable = canAfford;
                var img = item.BuyButton.GetComponent<Image>();
                if (img != null) img.color = canAfford ? UIHelpers.RustOrange : UIHelpers.BgSurface;
                if (item.PriceText != null)
                    item.PriceText.color = canAfford ? UIHelpers.WarmGold : UIHelpers.Shadow;
            }
        }

        private void OnLeave()
        {
            if (_runState != null) _runState.CompleteSelectedNode();
            var tm = SceneTransitionManager.Instance;
            if (tm != null) tm.GoToMap();
            else UnityEngine.SceneManagement.SceneManager.LoadScene(SceneTransitionManager.MAP_SCENE);
        }

        // ============================================================
        // UI construction
        // ============================================================

        private void CreateUI()
        {
            var canvasGO = new GameObject("ShopCanvas");
            canvasGO.transform.SetParent(transform);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            _canvasRT = canvasGO.GetComponent<RectTransform>();
            UIEventSystemProvider.EnsureEventSystem();

            var bgGO = MakePanel(_canvasRT, "BG", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, UIHelpers.BgDeep);
            bgGO.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            bgGO.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // Title
            var title = MakeText(_canvasRT, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -80), new Vector2(1500, 80), 42, TextAnchor.MiddleCenter, UIHelpers.WarmGold);
            title.fontStyle = FontStyle.Bold;
            title.text = "MERCHANT";

            // Currency: shared top-right readout (coin + amount), consistent with the map.
            // It subscribes to RunState.OnCurrencyChanged, so buying updates it automatically.
            CurrencyReadout.Create(_canvasRT, _runState);

            if (_items.Count == 0)
            {
                MakeText(_canvasRT, "Empty", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(1000, 70), 30, TextAnchor.MiddleCenter, UIHelpers.Shadow).text = "The merchant has nothing left to sell.";
            }
            else
            {
                CreateStockCards();
            }

            CreateLeaveButton();
            SetupNavigation();
            RefreshAffordability();
        }

        private void CreateStockCards()
        {
            float cardW = 420f, cardH = 560f, gap = 50f;
            float totalW = _items.Count * cardW + (_items.Count - 1) * gap;
            float startX = -totalW * 0.5f + cardW * 0.5f;

            for (int i = 0; i < _items.Count; i++)
                CreateShopCard(_items[i], i, startX + i * (cardW + gap), cardW, cardH);
        }

        private void CreateShopCard(ShopItem item, int index, float x, float cardW, float cardH)
        {
            RelicData relic = item.Relic;
            Color bgColor = RelicRarityPalette.Background(relic.rarity);
            Color borderColor = RelicRarityPalette.Accent(relic.rarity);

            // Card body sits a little above center to leave room for the buy button below it.
            var cardGO = MakePanel(_canvasRT, $"ShopCard_{index}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 20), new Vector2(cardW, cardH), bgColor);
            RectTransform cardRT = cardGO.GetComponent<RectTransform>();

            // Always-on rarity border behind the card (same as reward screen).
            var borderGO = MakePanel(cardRT, "Border", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(borderColor.r, borderColor.g, borderColor.b, 0.4f));
            var brt = borderGO.GetComponent<RectTransform>(); brt.offsetMin = new Vector2(-10, -10); brt.offsetMax = new Vector2(10, 10); brt.SetAsFirstSibling();

            MakeText(cardRT, "Rarity", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -25), new Vector2(cardW - 40, 36), 18, TextAnchor.MiddleCenter, borderColor).text = relic.rarity.ToString().ToUpper();

            var iconBG = MakePanel(cardRT, "IconBG", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(140, 140), RelicRarityPalette.IconSwatch(relic.rarity));
            // Icon sprite over the colour swatch: relic art if assigned, else shared placeholder.
            Sprite resolvedIcon = relic.ResolvedIcon;
            if (resolvedIcon != null)
            {
                var iconSpriteGO = new GameObject("IconSprite", typeof(RectTransform), typeof(Image));
                iconSpriteGO.transform.SetParent(iconBG.GetComponent<RectTransform>(), false);
                var isRT = iconSpriteGO.GetComponent<RectTransform>();
                isRT.anchorMin = Vector2.zero; isRT.anchorMax = Vector2.one;
                isRT.offsetMin = new Vector2(14, 14); isRT.offsetMax = new Vector2(-14, -14);
                var isImg = iconSpriteGO.GetComponent<Image>();
                isImg.sprite = resolvedIcon;
                isImg.color = UIHelpers.OffWhite;
                isImg.preserveAspect = true;
                isImg.raycastTarget = false;
            }

            var nameText = MakeText(cardRT, "Name", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -220), new Vector2(cardW - 40, 50), 26, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
            nameText.fontStyle = FontStyle.Bold; nameText.text = relic.relicName;

            MakeText(cardRT, "Desc", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -310), new Vector2(cardW - 60, 150), 19, TextAnchor.UpperCenter, UIHelpers.AmberOrange).text = relic.description;

            string valueStr = relic.ShortEffectSummary;
            if (!string.IsNullOrEmpty(valueStr))
            {
                var vt = MakeText(cardRT, "Value", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 90), new Vector2(cardW - 40, 40), 22, TextAnchor.MiddleCenter, UIHelpers.WarmGold);
                vt.fontStyle = FontStyle.Bold; vt.text = valueStr;
            }

            // Price tag near the bottom of the card.
            string currencyName = _runState.Economy != null ? _runState.Economy.CurrencyName : "Beats";
            item.PriceText = MakeText(cardRT, "Price", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(cardW - 40, 40), 24, TextAnchor.MiddleCenter, UIHelpers.WarmGold);
            item.PriceText.fontStyle = FontStyle.Bold;
            item.PriceText.text = $"{item.Price} {currencyName}";

            // SOLD overlay, hidden until purchased.
            item.SoldOverlay = MakePanel(cardRT, "SoldOverlay", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(UIHelpers.BgDeep.r, UIHelpers.BgDeep.g, UIHelpers.BgDeep.b, 0.7f));
            var soldRT = item.SoldOverlay.GetComponent<RectTransform>(); soldRT.offsetMin = Vector2.zero; soldRT.offsetMax = Vector2.zero;
            MakeText(item.SoldOverlay.GetComponent<RectTransform>(), "SoldText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(cardW, 80), 40, TextAnchor.MiddleCenter, UIHelpers.WarmGold).text = "SOLD";
            item.SoldOverlay.SetActive(false);

            // Buy button below the card.
            var buyGO = new GameObject($"Buy_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
            buyGO.transform.SetParent(_canvasRT, false);
            var buyRT = buyGO.GetComponent<RectTransform>();
            buyRT.anchorMin = buyRT.anchorMax = new Vector2(0.5f, 0.5f);
            buyRT.pivot = new Vector2(0.5f, 0.5f);
            buyRT.anchoredPosition = new Vector2(x, 20 - cardH * 0.5f - 50);
            buyRT.sizeDelta = new Vector2(cardW, 70);
            buyGO.GetComponent<Image>().color = UIHelpers.RustOrange;
            var buyText = MakeText(buyRT, "BuyText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(cardW, 70), 26, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
            buyText.fontStyle = FontStyle.Bold; buyText.text = "BUY";
            item.BuyButton = buyGO.GetComponent<Button>();
            int idx = index; item.BuyButton.onClick.AddListener(() => OnBuy(idx));
            buyGO.AddComponent<UISelectableSounds>();
        }

        private void CreateLeaveButton()
        {
            var leaveGO = new GameObject("LeaveBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            leaveGO.transform.SetParent(_canvasRT, false);
            var rt = leaveGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0, 60);
            rt.sizeDelta = new Vector2(320, 70);
            leaveGO.GetComponent<Image>().color = UIHelpers.BgLight;
            var t = MakeText(rt, "Text", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320, 70), 28, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
            t.fontStyle = FontStyle.Bold; t.text = "Leave";
            _leaveButton = leaveGO.GetComponent<Button>();
            _leaveButton.onClick.AddListener(OnLeave);
            UISelectableStyle.Apply(_leaveButton);
        }

        private void SetupNavigation()
        {
            _focusSetter = gameObject.AddComponent<UIFocusSetter>();
            _cancelHandler = gameObject.AddComponent<UICancelHandler>();
            // Escape / cancel leaves the shop, matching the Leave button.
            _cancelHandler.SetBaseAction(OnLeave);

            // Wire horizontal navigation across buy buttons, then down to Leave.
            var buyButtons = new List<Button>();
            foreach (var item in _items) if (item.BuyButton != null) buyButtons.Add(item.BuyButton);

            if (buyButtons.Count > 0)
            {
                UINavigationHelper.WireHorizontal(buyButtons.ToArray());
                foreach (var b in buyButtons) UINavigationHelper.AddLink(b, down: _leaveButton);
                UINavigationHelper.AddLink(_leaveButton, up: buyButtons[0]);
                _focusSetter.SetDefault(buyButtons[0].gameObject);
            }
            else
            {
                _focusSetter.SetDefault(_leaveButton.gameObject);
            }
        }

        // ============================================================
        // Shared helpers (same style as RewardPickScreen / RestScreen)
        // ============================================================

        private static GameObject MakePanel(RectTransform parent, string name, Vector2 ancMin, Vector2 ancMax, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = ancMin; rt.anchorMax = ancMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            obj.GetComponent<Image>().color = color;
            return obj;
        }

        private static Text MakeText(RectTransform parent, string name, Vector2 ancMin, Vector2 ancMax, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = ancMin; rt.anchorMax = ancMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = obj.GetComponent<Text>();
            t.font = UIHelpers.GetDefaultFont(fontSize);
            t.fontSize = fontSize; t.alignment = align; t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
