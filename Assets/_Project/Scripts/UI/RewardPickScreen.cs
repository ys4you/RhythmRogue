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
    public class RewardPickScreen : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RunState _runState;
        [SerializeField] private RelicPool _relicPool;
        [Header("Options")]
        [SerializeField] private int _optionCount = 3;
        [SerializeField] private int _eliteOptionCount = 3;

        private Canvas _canvas;
        private RectTransform _canvasRT;
        private List<RelicData> _options;
        private readonly List<Button> _optionButtons = new();
        private Text _titleText;
        private bool _picked;
        private UIFocusSetter _focusSetter;

        // Rarity colors from warm palette.
        // Common = neutral (BgSurface bg, RustOrange border)
        // Uncommon = warmer (Shadow bg, AmberOrange border)
        // Rare = special (BgLight bg, WarmGold border)
        private static Color CommonBG => UIHelpers.BgSurface;
        private static Color UncommonBG => UIHelpers.Shadow;
        private static Color RareBG => UIHelpers.BgLight;
        private static Color CommonBorder => UIHelpers.RustOrange;
        private static Color UncommonBorder => UIHelpers.AmberOrange;
        private static Color RareBorder => UIHelpers.WarmGold;

        private void Start()
        {
            GenerateOptions();
            CreateUI();
            // Continue the map ambient through the reward screen. The battle's stop call
            // left the music silent; this fades it back in so the moment of picking a relic
            // shares the same atmosphere as the map itself.
            MusicManager.Instance.Play(MusicTrack.MapShamanic);
        }

        private void GenerateOptions()
        {
            if (_relicPool == null || _runState == null) { _options = new List<RelicData>(); return; }
            ISeededRandom rng = _runState.RunSeed != null
                ? _runState.RunSeed.GetRandom(RandomDomain.Rewards, _runState.BattlesWon)
                : new SeededRandom(System.Environment.TickCount);
            int count = _runState.LastBattleWasElite ? Mathf.Max(_optionCount, _eliteOptionCount) : _optionCount;
            _options = _relicPool.PickOptions(rng, count, _runState.ActiveRelics);
        }

        private void OnRelicPicked(int index)
        {
            if (_picked || index < 0 || index >= _options.Count) return;
            _picked = true;
            RelicData chosen = _options[index];
            _runState.ActiveRelics.Add(chosen);
            if (chosen.effect == RelicEffect.MaxHPBoost) { var ph = PlayerHealth.Instance; if (ph != null) ph.IncreaseMaxHP(chosen.intValue); }
            GameLog.Info($"[RewardPick] Picked: {chosen.relicName} ({chosen.effect})");
            foreach (var btn in _optionButtons) btn.interactable = false;
            _optionButtons[index].GetComponent<Image>().color = UIHelpers.WarmGold;
            StartCoroutine(TransitionAfterDelay(0.5f));
        }

        private System.Collections.IEnumerator TransitionAfterDelay(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            var tm = SceneTransitionManager.Instance;
            if (tm != null) tm.GoToMap();
            else UnityEngine.SceneManagement.SceneManager.LoadScene(SceneTransitionManager.MAP_SCENE);
        }

        private void CreateUI()
        {
            GameObject canvasGO = new GameObject("RewardCanvas");
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

            string titlePrefix = _runState.LastBattleWasElite ? "ELITE " : "";
            _titleText = MakeText(_canvasRT, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -80), new Vector2(1500, 80), 42, TextAnchor.MiddleCenter, UIHelpers.WarmGold);
            _titleText.fontStyle = FontStyle.Bold;
            _titleText.text = $"{titlePrefix}CHOOSE A RELIC";

            var sub = MakeText(_canvasRT, "Subtitle", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -140), new Vector2(1500, 50), 22, TextAnchor.MiddleCenter, UIHelpers.AmberOrange);
            sub.text = $"Relics: {_runState.ActiveRelics.Count}";

            // Currency shown by the shared top-right readout (coin + amount), matching the map and shop.
            CurrencyReadout.Create(_canvasRT, _runState);

            if (_options.Count == 0)
            {
                MakeText(_canvasRT, "NoRelics", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(1000, 70), 32, TextAnchor.MiddleCenter, UIHelpers.Shadow).text = "No relics available";
                StartCoroutine(TransitionAfterDelay(1.5f));
                return;
            }
            CreateRelicCards();
        }

        private void CreateRelicCards()
        {
            float cardW = 450f, cardH = 600f, gap = 50f;
            float totalW = _options.Count * cardW + (_options.Count - 1) * gap;
            float startX = -totalW * 0.5f + cardW * 0.5f;

            for (int i = 0; i < _options.Count; i++)
                CreateRelicCard(_options[i], i, startX + i * (cardW + gap), cardW, cardH);

            _focusSetter = gameObject.AddComponent<UIFocusSetter>();
            if (_optionButtons.Count > 0)
            {
                UINavigationHelper.WireHorizontal(_optionButtons.ToArray());
                // NOTE: deliberately NOT calling UISelectableStyle.Apply here. That sets a
                // ColorTint transition whose selected-state color multiplies the card
                // background, making the focused card a different color than its siblings.
                // Focus is instead shown by UIFocusFrame (a toggled gold ring) set up in
                // CreateRelicCard, with the button transition forced to None. We still want
                // the hover/confirm SFX that UISelectableStyle would have attached, so we
                // attach the sound component directly.
                foreach (var btn in _optionButtons)
                {
                    if (btn.GetComponent<UISelectableSounds>() == null)
                        btn.gameObject.AddComponent<UISelectableSounds>();
                }
                _focusSetter.SetDefault(_optionButtons[_optionButtons.Count > 2 ? 1 : 0].gameObject);
            }
        }

        private void CreateRelicCard(RelicData relic, int index, float x, float cardW, float cardH)
        {
            Color bgColor = relic.rarity switch { RelicRarity.Common => CommonBG, RelicRarity.Uncommon => UncommonBG, RelicRarity.Rare => RareBG, _ => CommonBG };
            Color borderColor = relic.rarity switch { RelicRarity.Common => CommonBorder, RelicRarity.Uncommon => UncommonBorder, RelicRarity.Rare => RareBorder, _ => CommonBorder };

            var cardGO = MakePanel(_canvasRT, $"Card_{index}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -40), new Vector2(cardW, cardH), bgColor);
            Button btn = cardGO.AddComponent<Button>();
            int idx = index; btn.onClick.AddListener(() => OnRelicPicked(idx));
            _optionButtons.Add(btn);
            RectTransform cardRT = cardGO.GetComponent<RectTransform>();

            // Always-on rarity border: a thin static halo behind the card, tinted by rarity.
            // Identical for cards of the same rarity, never recolored by focus state.
            var borderGO = MakePanel(cardRT, "Border", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(borderColor.r, borderColor.g, borderColor.b, 0.4f));
            var brt = borderGO.GetComponent<RectTransform>(); brt.offsetMin = new Vector2(-10, -10); brt.offsetMax = new Vector2(10, 10); brt.SetAsFirstSibling();

            // Focus frame: a bright gold ring shown ONLY when this card has keyboard/mouse
            // focus. Hidden by default, toggled by UIFocusFrame via select/hover events.
            //
            // Ordering matters. Both this frame and the rarity border sit BEHIND the card
            // body (SetAsFirstSibling makes the most-recently-set one the back-most child).
            // We want, from back to front: focus frame (biggest) -> rarity border -> card body.
            // So the focus frame must be set to first-sibling AFTER the border, and must be
            // LARGER than the border (16px vs 10px outset) so a gold edge peeks out beyond
            // the rarity halo when focused. If it were smaller or set before the border, the
            // border would completely cover it and the focus highlight would be invisible
            // (which was the original bug).
            //
            // Replaces Unity's ColorTint focus transition, which multiplied the selected-state
            // color against the card background and made the focused card a different color
            // than its siblings. With ColorTint off + this frame, card backgrounds stay true.
            var focusFrameGO = MakePanel(cardRT, "FocusFrame", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, UIHelpers.WarmGold);
            var ffrt = focusFrameGO.GetComponent<RectTransform>(); ffrt.offsetMin = new Vector2(-16, -16); ffrt.offsetMax = new Vector2(16, 16); ffrt.SetAsFirstSibling();

            // Disable Unity's built-in tint transition and drive focus via UIFocusFrame.
            btn.transition = Selectable.Transition.None;
            var focus = cardGO.AddComponent<UIFocusFrame>();
            focus.SetFrame(focusFrameGO);

            MakeText(cardRT, "Rarity", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(cardW - 40, 40), 18, TextAnchor.MiddleCenter, borderColor).text = relic.rarity.ToString().ToUpper();

            var iconBG = MakePanel(cardRT, "IconBG", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(150, 150), relic.cardColor);
            // Icon sprite on top of the colour swatch: the relic's own art if assigned, else
            // the shared placeholder (the map's event '?' sprite). Always shows something.
            Sprite resolvedIcon = relic.ResolvedIcon;
            if (resolvedIcon != null)
            {
                var iconSpriteGO = new GameObject("IconSprite", typeof(RectTransform), typeof(Image));
                iconSpriteGO.transform.SetParent(iconBG.GetComponent<RectTransform>(), false);
                var isRT = iconSpriteGO.GetComponent<RectTransform>();
                isRT.anchorMin = Vector2.zero; isRT.anchorMax = Vector2.one;
                isRT.offsetMin = new Vector2(15, 15); isRT.offsetMax = new Vector2(-15, -15);
                var isImg = iconSpriteGO.GetComponent<Image>();
                isImg.sprite = resolvedIcon;
                isImg.color = UIHelpers.OffWhite;
                isImg.preserveAspect = true;
                isImg.raycastTarget = false;
            }

            var nameText = MakeText(cardRT, "Name", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -250), new Vector2(cardW - 40, 60), 28, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
            nameText.fontStyle = FontStyle.Bold; nameText.text = relic.relicName;

            MakeText(cardRT, "Desc", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -360), new Vector2(cardW - 60, 200), 20, TextAnchor.UpperCenter, UIHelpers.AmberOrange).text = relic.description;

            string valueStr = FormatEffectValue(relic);
            if (!string.IsNullOrEmpty(valueStr))
            {
                var vt = MakeText(cardRT, "Value", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(cardW - 40, 50), 24, TextAnchor.MiddleCenter, UIHelpers.WarmGold);
                vt.fontStyle = FontStyle.Bold; vt.text = valueStr;
            }
        }

        // Builds the value badge string (e.g. "+5ms", "-2 dmg"). Returns empty when the
        // relevant value is zero so a misconfigured relic shows no badge rather than a
        // meaningless "+0". Each effect reads exactly one value field, matching the
        // canonical mapping in RelicEffectAggregator: float-backed effects read floatValue,
        // int-backed effects read intValue.
        private static string FormatEffectValue(RelicData relic)
        {
            switch (relic.effect)
            {
                case RelicEffect.WiderPerfectWindow:
                    return relic.floatValue != 0f ? $"+{relic.floatValue:0.##}ms" : "";
                case RelicEffect.BonusPerfectDamage:
                    return relic.floatValue != 0f ? $"+{relic.floatValue:0.##} dmg" : "";
                case RelicEffect.ComboRateBoost:
                    return relic.floatValue != 0f ? $"+{relic.floatValue:0.##}/hit" : "";
                case RelicEffect.ComboCapBoost:
                    return relic.floatValue != 0f ? $"+{relic.floatValue:0.##}x cap" : "";
                case RelicEffect.HealOnComboMilestone:
                    return relic.intValue != 0 ? $"+{relic.intValue} HP" : "";
                case RelicEffect.ReduceMissDamage:
                    return relic.intValue != 0 ? $"-{relic.intValue} dmg" : "";
                case RelicEffect.MaxHPBoost:
                    return relic.intValue != 0 ? $"+{relic.intValue} max HP" : "";
                case RelicEffect.CurrencyMultiplier:
                    return relic.floatValue != 0f ? $"+{relic.floatValue * 100:0}%" : "";
                default:
                    return "";
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
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}