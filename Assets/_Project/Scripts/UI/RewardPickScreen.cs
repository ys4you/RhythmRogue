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
    /// <summary>
    /// Post-battle reward pick screen.
    /// 
    /// Shows 2-3 relic options. Player picks one, it gets added
    /// to RunState.ActiveRelics, then transitions back to the map.
    /// 
    /// Relic options are generated deterministically from the run
    /// seed + encounter index so the same seed always offers the
    /// same choices at the same point in a run.
    /// 
    /// Fully keyboard/gamepad navigable.
    /// Code-generated UI, sized for 384×216.
    /// 
    /// Scene: RewardScene (create a new scene with a GameObject
    /// holding this component, with RunState + RelicPool assigned).
    /// </summary>
    public class RewardPickScreen : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("References")]
        [SerializeField] private RunState _runState;
        [SerializeField] private RelicPool _relicPool;

        [Header("Options")]
        [Tooltip("Number of relic options to offer.")]
        [SerializeField] private int _optionCount = 3;

        // =================================================================
        // STATE
        // =================================================================

        private Canvas _canvas;
        private RectTransform _canvasRT;
        private List<RelicData> _options;
        private readonly List<Button> _optionButtons = new();
        private Text _titleText;
        private bool _picked;

        // Navigation
        private UIFocusSetter _focusSetter;

        // Colors
        private static readonly Color CommonColor = new(0.25f, 0.3f, 0.25f);
        private static readonly Color UncommonColor = new(0.2f, 0.25f, 0.4f);
        private static readonly Color RareColor = new(0.4f, 0.25f, 0.15f);

        private static readonly Color CommonBorder = new(0.5f, 0.7f, 0.5f);
        private static readonly Color UncommonBorder = new(0.4f, 0.5f, 0.9f);
        private static readonly Color RareBorder = new(0.9f, 0.6f, 0.2f);

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Start()
        {
            GenerateOptions();
            CreateUI();
        }

        // =================================================================
        // OPTION GENERATION
        // =================================================================

        private void GenerateOptions()
        {
            if (_relicPool == null || _runState == null)
            {
                GameLog.Error("[RewardPick] Missing RunState or RelicPool!");
                _options = new List<RelicData>();
                return;
            }

            // Fork RNG from Rewards domain + battle count for determinism
            ISeededRandom rng;

            if (_runState.RunSeed != null)
            {
                rng = _runState.RunSeed.GetRandom(RandomDomain.Rewards, _runState.BattlesWon);
            }
            else
            {
                rng = new SeededRandom(System.Environment.TickCount);
            }

            _options = _relicPool.PickOptions(rng, _optionCount, _runState.ActiveRelics);
        }

        // =================================================================
        // RELIC PICKUP
        // =================================================================

        private void OnRelicPicked(int index)
        {
            if (_picked) return;
            if (index < 0 || index >= _options.Count) return;

            _picked = true;

            RelicData chosen = _options[index];
            _runState.ActiveRelics.Add(chosen);

            // Apply immediate effects (like MaxHP boost)
            if (chosen.effect == RelicEffect.MaxHPBoost)
            {
                var ph = PlayerHealth.Instance;
                if (ph != null)
                    ph.IncreaseMaxHP(chosen.intValue);
            }

            GameLog.Info($"[RewardPick] Picked: {chosen.relicName} ({chosen.effect})");

            // Disable all buttons
            foreach (var btn in _optionButtons)
                btn.interactable = false;

            // Highlight chosen card
            _optionButtons[index].GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.3f);

            // Brief delay then go to map
            StartCoroutine(TransitionAfterDelay(0.5f));
        }

        private System.Collections.IEnumerator TransitionAfterDelay(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);

            var tm = SceneTransitionManager.Instance;
            if (tm != null)
                tm.GoToMap();
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    SceneTransitionManager.MAP_SCENE);
        }

        // =================================================================
        // UI CREATION
        // =================================================================

        private void CreateUI()
        {
            // Canvas
            GameObject canvasGO = new GameObject("RewardCanvas");
            canvasGO.transform.SetParent(transform);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(384, 216);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();
            _canvasRT = canvasGO.GetComponent<RectTransform>();

            UIEventSystemProvider.EnsureEventSystem();

            // Background
            GameObject bgGO = MakePanel(_canvasRT, "BG",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                new Color(0.06f, 0.06f, 0.1f));
            bgGO.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            bgGO.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // Title
            _titleText = MakeText(_canvasRT, "Title",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -16), new Vector2(300, 16),
                9, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0f));
            _titleText.fontStyle = FontStyle.Bold;
            _titleText.text = "CHOOSE A RELIC";

            // Subtitle
            Text sub = MakeText(_canvasRT, "Subtitle",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -28), new Vector2(300, 10),
                5, TextAnchor.MiddleCenter, new Color(0.6f, 0.6f, 0.6f));
            sub.text = $"Relics: {_runState.ActiveRelics.Count}";

            // Relic cards
            if (_options.Count == 0)
            {
                Text noRelics = MakeText(_canvasRT, "NoRelics",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(200, 14),
                    7, TextAnchor.MiddleCenter, new Color(0.5f, 0.5f, 0.5f));
                noRelics.text = "No relics available";

                // Auto-return to map after delay
                StartCoroutine(TransitionAfterDelay(1.5f));
                return;
            }

            CreateRelicCards();
        }

        private void CreateRelicCards()
        {
            float cardW = 90f;
            float cardH = 120f;
            float gap = 10f;
            float totalW = _options.Count * cardW + (_options.Count - 1) * gap;
            float startX = -totalW * 0.5f + cardW * 0.5f;

            for (int i = 0; i < _options.Count; i++)
            {
                float x = startX + i * (cardW + gap);
                CreateRelicCard(_options[i], i, x, cardW, cardH);
            }

            // Navigation
            _focusSetter = gameObject.AddComponent<UIFocusSetter>();

            if (_optionButtons.Count > 0)
            {
                UINavigationHelper.WireHorizontal(_optionButtons.ToArray());

                foreach (var btn in _optionButtons)
                    UISelectableStyle.Apply(btn);

                // Focus middle card (or first if only 2)
                int focusIdx = _optionButtons.Count > 2 ? 1 : 0;
                _focusSetter.SetDefault(_optionButtons[focusIdx].gameObject);
            }
        }

        private void CreateRelicCard(RelicData relic, int index, float x,
            float cardW, float cardH)
        {
            Color bgColor = relic.rarity switch
            {
                RelicRarity.Common => CommonColor,
                RelicRarity.Uncommon => UncommonColor,
                RelicRarity.Rare => RareColor,
                _ => CommonColor
            };

            Color borderColor = relic.rarity switch
            {
                RelicRarity.Common => CommonBorder,
                RelicRarity.Uncommon => UncommonBorder,
                RelicRarity.Rare => RareBorder,
                _ => CommonBorder
            };

            // Card root (button)
            GameObject cardGO = MakePanel(_canvasRT, $"Card_{index}",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, -8), new Vector2(cardW, cardH),
                bgColor);

            Button btn = cardGO.AddComponent<Button>();
            int idx = index;
            btn.onClick.AddListener(() => OnRelicPicked(idx));
            _optionButtons.Add(btn);

            RectTransform cardRT = cardGO.GetComponent<RectTransform>();

            // Border glow
            GameObject borderGO = MakePanel(cardRT, "Border",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                new Color(borderColor.r, borderColor.g, borderColor.b, 0.3f));
            RectTransform brt = borderGO.GetComponent<RectTransform>();
            brt.offsetMin = new Vector2(-2, -2);
            brt.offsetMax = new Vector2(2, 2);
            brt.SetAsFirstSibling();

            // Rarity label
            string rarityStr = relic.rarity.ToString().ToUpper();
            Color rarityColor = borderColor;

            Text rarityText = MakeText(cardRT, "Rarity",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -6), new Vector2(cardW - 8, 8),
                4, TextAnchor.MiddleCenter, rarityColor);
            rarityText.text = rarityStr;

            // Icon area (placeholder colored square)
            Color iconColor = relic.cardColor;
            MakePanel(cardRT, "IconBG",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -28), new Vector2(30, 30),
                iconColor);

            // Relic name
            Text nameText = MakeText(cardRT, "Name",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -50), new Vector2(cardW - 8, 12),
                6, TextAnchor.MiddleCenter, Color.white);
            nameText.fontStyle = FontStyle.Bold;
            nameText.text = relic.relicName;

            // Description
            Text descText = MakeText(cardRT, "Desc",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -72), new Vector2(cardW - 12, 40),
                4, TextAnchor.UpperCenter, new Color(0.75f, 0.75f, 0.75f));
            descText.text = relic.description;

            // Effect value highlight
            string valueStr = FormatEffectValue(relic);
            if (!string.IsNullOrEmpty(valueStr))
            {
                Text valueText = MakeText(cardRT, "Value",
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0, 8), new Vector2(cardW - 8, 10),
                    5, TextAnchor.MiddleCenter, new Color(0.5f, 1f, 0.5f));
                valueText.fontStyle = FontStyle.Bold;
                valueText.text = valueStr;
            }
        }

        // =================================================================
        // HELPERS
        // =================================================================

        private static string FormatEffectValue(RelicData relic)
        {
            return relic.effect switch
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
        }

        // =================================================================
        // UI PRIMITIVES
        // =================================================================

        private static GameObject MakePanel(RectTransform parent, string name,
            Vector2 ancMin, Vector2 ancMax, Vector2 pivot,
            Vector2 pos, Vector2 size, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = ancMin;
            rt.anchorMax = ancMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            obj.GetComponent<Image>().color = color;
            return obj;
        }

        private static Text MakeText(RectTransform parent, string name,
            Vector2 ancMin, Vector2 ancMax, Vector2 pivot,
            Vector2 pos, Vector2 size,
            int fontSize, TextAnchor align, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = ancMin;
            rt.anchorMax = ancMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Text t = obj.GetComponent<Text>();
            t.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            return t;
        }
    }
}
