using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.Core;
using RhythmRogue.Core.Audio;
using RhythmRogue.Data;
using RhythmRogue.Util;
using RhythmRogue.Util.Random;
using RhythmRogue.UI.Navigation;

namespace RhythmRogue.UI
{
    /// <summary>
    /// Event scene controller. Presents a seeded event from the EventPool: title, flavor
    /// text, and a set of choices. Choosing one applies its effects (currency / HP / relic
    /// via EventOutcomeApplier), shows the result, and offers Continue back to the map.
    ///
    /// Flow is choose-once: unlike the shop (browse freely), an event is a single decision.
    /// After a choice resolves, the choice buttons are replaced by the result text and a
    /// single Continue button.
    ///
    /// Reuses the RestScreen presentation pattern (canvas, palette panels, MakeText/Button
    /// helpers, focus + cancel handlers). Stock selection + effect application live in the
    /// data layer (EventPool, EventOutcomeApplier), keeping this class presentation-only.
    ///
    /// SOLID:
    ///   S - Scene presentation + choice flow only. Selection in EventPool, effects in
    ///       EventOutcomeApplier, currency/HP/relics in RunState/PlayerHealth.
    ///   O - New effect kinds flow in through the applier; this screen needs no change.
    ///   D - Depends on RunState / EventPool / RelicPool abstractions assigned in Inspector.
    /// </summary>
    public class EventScreen : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RunState _runState;
        [SerializeField] private EventPool _eventPool;
        [SerializeField] private RelicPool _relicPool; // for relic-granting outcomes

        private Canvas _canvas;
        private RectTransform _canvasRT;
        private Text _titleText, _flavorText, _resultText;
        private readonly List<Button> _choiceButtons = new();
        private Button _continueBtn;
        private UIFocusSetter _focusSetter;
        private UICancelHandler _cancelHandler;

        private EventData _event;
        private ISeededRandom _rng;
        private bool _resolved;

        private void Start()
        {
            if (_runState == null) { GameLog.Error("[EventScreen] No RunState assigned."); return; }

            // Seed from run seed + battle count via the Events domain, so the same run rolls
            // the same event at the same point (GDD determinism).
            _rng = _runState.RunSeed != null
                ? _runState.RunSeed.GetRandom(RandomDomain.Events, _runState.BattlesWon)
                : new SeededRandom(System.Environment.TickCount);

            _event = _eventPool != null ? _eventPool.PickOne(_rng) : null;

            CreateUI();

            _focusSetter = gameObject.AddComponent<UIFocusSetter>();
            _cancelHandler = gameObject.AddComponent<UICancelHandler>();
            // Before a choice is made, cancel does nothing (you must decide). After resolving,
            // cancel leaves like Continue. We point the base action at a guard that respects that.
            _cancelHandler.SetBaseAction(OnCancelPressed);

            if (_choiceButtons.Count > 0)
                _focusSetter.SetDefault(_choiceButtons[0].gameObject);

            // Keep the map's ambient playing through the event. Idempotent.
            MusicManager.Instance.Play(MusicTrack.MapShamanic);
        }

        private void OnCancelPressed()
        {
            // Only allow leaving via cancel once the event has resolved. This prevents
            // escaping a decision without taking any outcome.
            if (_resolved) OnContinue();
        }

        private void OnChoiceSelected(int index)
        {
            if (_resolved || _event == null || index < 0 || index >= _event.choices.Length) return;
            _resolved = true;

            EventChoice choice = _event.choices[index];

            // Apply the effects and get a concrete summary of what changed.
            string summary = EventOutcomeApplier.Apply(choice, _runState, _relicPool, _rng);

            var am = AudioManager.Instance;
            if (am != null) am.PlayIfRegistered(SfxId.UiSelectMajor);

            // Hide the choice buttons; show the result.
            foreach (var b in _choiceButtons) b.gameObject.SetActive(false);

            // Compose the result: authored flavor line first (if any), then the concrete
            // mechanical summary (if any), so the player sees both the story and the numbers.
            string resultBody = choice.ResultText ?? "";
            if (!string.IsNullOrEmpty(summary))
            {
                if (!string.IsNullOrEmpty(resultBody)) resultBody += "\n\n";
                resultBody += summary;
            }
            if (string.IsNullOrEmpty(resultBody)) resultBody = "You move on.";

            _resultText.text = resultBody;
            _resultText.gameObject.SetActive(true);

            ShowContinueButton();

            GameLog.Info($"[EventScreen] Event '{_event.eventTitle}' choice '{choice.Label}' resolved: {summary}");
        }

        private void OnContinue()
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
            var canvasGO = new GameObject("EventCanvas");
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

            // Background
            var bgGO = MakePanel(_canvasRT, "BG", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, UIHelpers.BgDeep);
            bgGO.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            bgGO.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // Subtle central glow, cooler/eerier than the rest screen's campfire: use BgLight purple.
            MakePanel(_canvasRT, "Glow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 1000), new Color(UIHelpers.BgLight.r, UIHelpers.BgLight.g, UIHelpers.BgLight.b, 0.18f));

            if (_event == null)
            {
                // Graceful fallback if the pool is empty/unassigned: a neutral non-event.
                _titleText = MakeText(_canvasRT, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0, -120), new Vector2(1500, 100), 52, TextAnchor.MiddleCenter, UIHelpers.WarmGold);
                _titleText.fontStyle = FontStyle.Bold;
                _titleText.text = "An Empty Road";

                _flavorText = MakeText(_canvasRT, "Flavor", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0, -260), new Vector2(1100, 200), 26, TextAnchor.UpperCenter, UIHelpers.AmberOrange);
                _flavorText.fontStyle = FontStyle.Italic;
                _flavorText.text = "Nothing stirs here. You press onward.";

                ShowContinueButton();
                return;
            }

            // Title
            Color titleColor = IsNearWhite(_event.accentColor) ? UIHelpers.WarmGold : _event.accentColor;
            _titleText = MakeText(_canvasRT, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -110), new Vector2(1500, 100), 52, TextAnchor.MiddleCenter, titleColor);
            _titleText.fontStyle = FontStyle.Bold;
            _titleText.text = _event.eventTitle;

            // Flavor
            _flavorText = MakeText(_canvasRT, "Flavor", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -260), new Vector2(1100, 240), 26, TextAnchor.UpperCenter, UIHelpers.OffWhite);
            _flavorText.text = _event.flavorText;

            // Result text (hidden until a choice resolves), sits where the flavor's lower area is.
            _resultText = MakeText(_canvasRT, "Result", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 40), new Vector2(1100, 240), 28, TextAnchor.MiddleCenter, UIHelpers.WarmGold);
            _resultText.gameObject.SetActive(false);

            CreateChoiceButtons();
        }

        private void CreateChoiceButtons()
        {
            _choiceButtons.Clear();
            int n = _event.choices.Length;
            if (n == 0) { ShowContinueButton(); return; }

            // Stack choices vertically in the lower third of the screen.
            float btnW = 760f, btnH = 80f, gap = 24f;
            float totalH = n * btnH + (n - 1) * gap;
            float startY = -120f + totalH * 0.5f - btnH * 0.5f; // centered around y = -120 from middle

            for (int i = 0; i < n; i++)
            {
                EventChoice choice = _event.choices[i];
                float y = startY - i * (btnH + gap);
                var btn = MakeButton(_canvasRT, $"Choice_{i}", choice.Label, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(btnW, btnH), UIHelpers.RustOrange);
                int idx = i;
                btn.onClick.AddListener(() => OnChoiceSelected(idx));
                UISelectableStyle.Apply(btn);
                _choiceButtons.Add(btn);
            }

            // Vertical navigation through the choices.
            if (_choiceButtons.Count > 1)
                UINavigationHelper.WireVerticalNoWrap(_choiceButtons.ToArray());
        }

        private void ShowContinueButton()
        {
            if (_continueBtn != null) { _continueBtn.gameObject.SetActive(true); return; }

            _continueBtn = MakeButton(_canvasRT, "ContinueBtn", "Continue", new Vector2(0.5f, 0f), new Vector2(0, 70), new Vector2(360, 80), UIHelpers.BgLight);
            _continueBtn.onClick.AddListener(OnContinue);
            UISelectableStyle.Apply(_continueBtn);

            // Hand keyboard focus to Continue if the player is navigating by keyboard.
            if (_focusSetter != null) _focusSetter.FocusOn(_continueBtn.gameObject);
        }

        private static bool IsNearWhite(Color c) => c.r > 0.9f && c.g > 0.9f && c.b > 0.9f;

        // ============================================================
        // Shared helpers (same as RestScreen)
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
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static Button MakeButton(RectTransform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color bgColor)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            obj.GetComponent<Image>().color = bgColor;

            var txtGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtGO.transform.SetParent(obj.transform, false);
            var txtRT = txtGO.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = new Vector2(20, 0); txtRT.offsetMax = new Vector2(-20, 0);
            var t = txtGO.GetComponent<Text>();
            t.font = UIHelpers.GetDefaultFont(28);
            t.fontSize = 28; t.alignment = TextAnchor.MiddleCenter;
            t.color = UIHelpers.OffWhite; t.fontStyle = FontStyle.Bold; t.text = label;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return obj.GetComponent<Button>();
        }
    }
}
