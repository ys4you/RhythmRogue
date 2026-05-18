using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.Core;
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

        private void Start() { GenerateOptions(); CreateUI(); }

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
                foreach (var btn in _optionButtons) UISelectableStyle.Apply(btn);
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

            var borderGO = MakePanel(cardRT, "Border", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(borderColor.r, borderColor.g, borderColor.b, 0.4f));
            var brt = borderGO.GetComponent<RectTransform>(); brt.offsetMin = new Vector2(-10, -10); brt.offsetMax = new Vector2(10, 10); brt.SetAsFirstSibling();

            MakeText(cardRT, "Rarity", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(cardW - 40, 40), 18, TextAnchor.MiddleCenter, borderColor).text = relic.rarity.ToString().ToUpper();

            MakePanel(cardRT, "IconBG", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(150, 150), relic.cardColor);

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

        private static string FormatEffectValue(RelicData relic) => relic.effect switch
        {
            RelicEffect.WiderPerfectWindow => $"+{relic.floatValue}ms",
            RelicEffect.BonusPerfectDamage => $"+{relic.floatValue} dmg",
            RelicEffect.ComboRateBoost => $"+{relic.floatValue}/hit",
            RelicEffect.ComboCapBoost => $"+{relic.floatValue}x cap",
            RelicEffect.HealOnComboMilestone => $"+{relic.intValue} HP",
            RelicEffect.ReduceMissDamage => $"-{relic.intValue} dmg",
            RelicEffect.MaxHPBoost => $"+{relic.intValue} max HP",
            RelicEffect.CurrencyMultiplier => $"+{relic.floatValue * 100:0}%",
            _ => ""
        };

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