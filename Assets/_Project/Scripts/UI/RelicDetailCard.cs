using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.Data;
using RhythmRogue.UI.Navigation;

namespace RhythmRogue.UI
{
    /// <summary>
    /// A modal detail card for a single relic, shown when the player clicks a relic icon in
    /// the RelicBar. Displays everything the player needs: name, rarity (colour-coded to the
    /// warm palette), the mechanical effect value, the description, and the optional flavor
    /// line. Dismissed by clicking the backdrop, the Close button, or Escape.
    ///
    /// Built on its own high-sorting canvas so it overlays any scene's HUD. One instance is
    /// created lazily by RelicBar and reused (Show/Hide) rather than rebuilt per click.
    ///
    /// SOLID:
    ///   S - Presents one relic's details. No knowledge of where relics come from or how
    ///       the bar is laid out.
    ///   D - Takes a RelicData and renders it; depends on the data abstraction only.
    /// </summary>
    public class RelicDetailCard : MonoBehaviour
    {
        private Canvas _canvas;
        private RectTransform _canvasRT;
        private GameObject _root;          // dim backdrop (toggled)
        private Image _iconSwatch;
        private Image _iconSprite;   // sprite drawn on top of the swatch (real art or placeholder)
        private Text _nameText, _rarityText, _effectText, _descText, _flavorText;
        private UICancelHandler _cancelHandler;
        private bool _built;
        private bool _cancelPushed;

        /// <summary>Create (once) and return a reusable detail card.</summary>
        public static RelicDetailCard Create()
        {
            var go = new GameObject("RelicDetailCard");
            var card = go.AddComponent<RelicDetailCard>();
            card.Build();
            return card;
        }

        private void Build()
        {
            if (_built) return;

            var canvasGO = new GameObject("RelicDetailCanvas");
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the relic bar (700) and the pause menu (500); this is a focused modal.
            _canvas.sortingOrder = 800;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            _canvasRT = canvasGO.GetComponent<RectTransform>();
            canvasGO.transform.SetParent(transform, false);
            UIEventSystemProvider.EnsureEventSystem();

            // Dim backdrop. Clicking it closes the card.
            _root = MakePanel(_canvasRT, "Backdrop", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
                new Color(UIHelpers.BgDeep.r, UIHelpers.BgDeep.g, UIHelpers.BgDeep.b, 0.8f));
            var rootRT = _root.GetComponent<RectTransform>();
            rootRT.offsetMin = Vector2.zero; rootRT.offsetMax = Vector2.zero;
            var backdropBtn = _root.AddComponent<Button>();
            backdropBtn.transition = Selectable.Transition.None;
            backdropBtn.onClick.AddListener(Hide);

            // Card
            var card = MakePanel(rootRT, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 620), UIHelpers.BgSurface);
            var cardRT = card.GetComponent<RectTransform>();
            // Eat clicks on the card so they don't fall through to the backdrop's close.
            var cardBlocker = card.AddComponent<Button>();
            cardBlocker.transition = Selectable.Transition.None;

            // Rarity border behind the card.
            var border = MakePanel(cardRT, "Border", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, UIHelpers.WarmGold);
            var borderRT = border.GetComponent<RectTransform>();
            borderRT.offsetMin = new Vector2(-8, -8); borderRT.offsetMax = new Vector2(8, 8);
            borderRT.SetAsFirstSibling();
            _rarityBorder = border.GetComponent<Image>();

            // Icon swatch (the relic's accent colour) with the icon sprite layered on top.
            // The sprite is the relic's own art if assigned, else the shared event-node
            // placeholder, so there's always something recognisable here.
            var swatchGO = MakePanel(cardRT, "IconSwatch", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(120, 120), UIHelpers.RustOrange);
            _iconSwatch = swatchGO.GetComponent<Image>();

            var iconSpriteGO = new GameObject("IconSprite", typeof(RectTransform), typeof(Image));
            iconSpriteGO.transform.SetParent(swatchGO.GetComponent<RectTransform>(), false);
            var isRT = iconSpriteGO.GetComponent<RectTransform>();
            isRT.anchorMin = Vector2.zero; isRT.anchorMax = Vector2.one;
            isRT.offsetMin = new Vector2(12, 12); isRT.offsetMax = new Vector2(-12, -12);
            _iconSprite = iconSpriteGO.GetComponent<Image>();
            _iconSprite.color = UIHelpers.OffWhite;
            _iconSprite.preserveAspect = true;
            _iconSprite.raycastTarget = false;

            _rarityText = MakeText(cardRT, "Rarity", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -170), new Vector2(580, 36), 18, TextAnchor.MiddleCenter, UIHelpers.WarmGold);
            _rarityText.fontStyle = FontStyle.Bold;

            _nameText = MakeText(cardRT, "Name", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -215), new Vector2(580, 50), 32, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
            _nameText.fontStyle = FontStyle.Bold;

            _effectText = MakeText(cardRT, "Effect", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -285), new Vector2(580, 40), 24, TextAnchor.MiddleCenter, UIHelpers.WarmGold);
            _effectText.fontStyle = FontStyle.Bold;

            _descText = MakeText(cardRT, "Desc", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -340), new Vector2(540, 120), 22, TextAnchor.UpperCenter, UIHelpers.AmberOrange);

            _flavorText = MakeText(cardRT, "Flavor", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 110), new Vector2(540, 120), 20, TextAnchor.LowerCenter, UIHelpers.Shadow);
            _flavorText.fontStyle = FontStyle.Italic;

            var closeGO = MakePanel(cardRT, "CloseBtn", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 35), new Vector2(220, 56), UIHelpers.RustOrange);
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.onClick.AddListener(Hide);
            UISelectableStyle.Apply(closeBtn);
            MakeText(closeGO.GetComponent<RectTransform>(), "CloseText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, 56), 24, TextAnchor.MiddleCenter, UIHelpers.OffWhite).text = "Close";
            _closeButton = closeBtn;

            _cancelHandler = gameObject.AddComponent<UICancelHandler>();

            _root.SetActive(false);
            _built = true;
        }

        private Image _rarityBorder;
        private Button _closeButton;

        public void Show(RelicData relic)
        {
            if (!_built || relic == null) return;

            Color rarityColor = RarityColor(relic.rarity);
            _rarityBorder.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.5f);
            _iconSwatch.color = relic.cardColor;

            // Icon sprite: relic's own art, else the shared placeholder. Hidden if neither
            // exists (the coloured swatch alone still represents the relic).
            Sprite resolved = relic.ResolvedIcon;
            if (resolved != null)
            {
                _iconSprite.sprite = resolved;
                _iconSprite.gameObject.SetActive(true);
            }
            else _iconSprite.gameObject.SetActive(false);

            _rarityText.text = relic.rarity.ToString().ToUpper();
            _rarityText.color = rarityColor;
            _nameText.text = relic.relicName;
            _effectText.text = FormatEffectValue(relic);
            _descText.text = relic.description;

            if (!string.IsNullOrWhiteSpace(relic.flavorText))
            {
                _flavorText.text = relic.flavorText;
                _flavorText.gameObject.SetActive(true);
            }
            else _flavorText.gameObject.SetActive(false);

            _root.SetActive(true);

            if (!_cancelPushed) { _cancelHandler.Push(Hide); _cancelPushed = true; }
        }

        public void Hide()
        {
            if (!_built) return;
            if (_cancelPushed && _cancelHandler != null) { _cancelHandler.Pop(); _cancelPushed = false; }
            _root.SetActive(false);
        }

        public bool IsOpen => _built && _root != null && _root.activeSelf;

        private static Color RarityColor(RelicRarity rarity) => rarity switch
        {
            RelicRarity.Common => UIHelpers.RustOrange,
            RelicRarity.Uncommon => UIHelpers.AmberOrange,
            RelicRarity.Rare => UIHelpers.WarmGold,
            _ => UIHelpers.RustOrange
        };

        // Mirrors the reward/shop value badge so the effect reads consistently everywhere.
        private static string FormatEffectValue(RelicData relic)
        {
            switch (relic.effect)
            {
                case RelicEffect.WiderPerfectWindow: return relic.floatValue != 0f ? $"+{relic.floatValue:0.##} ms Perfect window" : "";
                case RelicEffect.BonusPerfectDamage: return relic.floatValue != 0f ? $"+{relic.floatValue:0.##} Perfect damage" : "";
                case RelicEffect.ComboRateBoost: return relic.floatValue != 0f ? $"+{relic.floatValue:0.##} combo/hit" : "";
                case RelicEffect.ComboCapBoost: return relic.floatValue != 0f ? $"+{relic.floatValue:0.##}x combo cap" : "";
                case RelicEffect.HealOnComboMilestone: return relic.intValue != 0 ? $"Heal +{relic.intValue} HP at combo milestones" : "";
                case RelicEffect.ReduceMissDamage: return relic.intValue != 0 ? $"-{relic.intValue} Miss damage" : "";
                case RelicEffect.MaxHPBoost: return relic.intValue != 0 ? $"+{relic.intValue} max HP" : "";
                case RelicEffect.CurrencyMultiplier: return relic.floatValue != 0f ? $"+{relic.floatValue * 100:0}% currency" : "";
                default: return "";
            }
        }

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
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
