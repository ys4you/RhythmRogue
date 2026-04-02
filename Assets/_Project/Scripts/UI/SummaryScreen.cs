using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.Core;
using RhythmRogue.Battle;
using RhythmRogue.UI.Navigation;

namespace RhythmRogue.UI
{
    /// <summary>
    /// Run summary screen shown after victory or defeat.
    /// 
    /// Reads final stats from RunState. Displays:
    ///   - Victory/Defeated header with animated entrance
    ///   - Run stats with number roll animation
    ///   - Seed for sharing
    ///   - Action buttons: New Run, Retry (same seed), Main Menu
    /// 
    /// Fully keyboard/gamepad navigable:
    ///   - New Run auto-focused after animation
    ///   - Left/Right navigates between New Run ↔ Retry Seed ↔ Menu
    ///   - Enter/Space confirms
    ///   - Escape goes to Main Menu
    /// 
    /// Fully code-generated UI, sized for 384×216.
    /// </summary>
    public class SummaryScreen : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("References")]
        [SerializeField] private RunState _runState;

        [Header("Timing")]
        [SerializeField] private float _headerAnimDuration = 0.5f;
        [SerializeField] private float _statRollDuration = 1.2f;
        [SerializeField] private float _statStaggerDelay = 0.15f;

        // =================================================================
        // UI REFERENCES
        // =================================================================

        private Canvas _canvas;
        private RectTransform _canvasRT;

        private Text _headerText;
        private Text _subHeaderText;
        private Text[] _statLabels;
        private Text[] _statValues;
        private Text _seedText;
        private Button _newRunBtn;
        private Button _retryBtn;
        private Button _menuBtn;
        private Image _bgOverlay;

        // Navigation
        private UIFocusSetter _focusSetter;
        private UICancelHandler _cancelHandler;

        // Stats to display
        private int _battlesWon;
        private int _totalScore;
        private int _maxCombo;
        private float _bestAccuracy;
        private int _nodesCompleted;
        private int _totalNodes;
        private int _finalHP;
        private int _maxHP;
        private bool _isVictory;
        private string _seed;

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Start()
        {
            GatherStats();
            CreateUI();
            SetupNavigation();
            StartCoroutine(AnimateEntrance());
        }

        // =================================================================
        // NAVIGATION SETUP
        // =================================================================

        private void SetupNavigation()
        {
            // Focus setter — will be activated after animation reveals buttons
            _focusSetter = gameObject.AddComponent<UIFocusSetter>();

            // Cancel handler — Escape goes to main menu
            _cancelHandler = gameObject.AddComponent<UICancelHandler>();
            _cancelHandler.SetBaseAction(OnMenu);
        }

        /// <summary>
        /// Wire navigation after buttons are revealed by the animation.
        /// Called at the end of AnimateEntrance.
        /// </summary>
        private void ActivateButtonNavigation()
        {
            // Wire horizontal navigation: New Run ↔ Retry Seed ↔ Menu
            UINavigationHelper.WireHorizontal(_newRunBtn, _retryBtn, _menuBtn);

            // Apply visual focus styles
            UISelectableStyle.Apply(_newRunBtn);
            UISelectableStyle.Apply(_retryBtn);
            UISelectableStyle.Apply(_menuBtn);

            // Set default focus to New Run
            _focusSetter.SetDefault(_newRunBtn.gameObject);
            _focusSetter.ApplyFocus();
        }

        // =================================================================
        // STATS GATHERING
        // =================================================================

        private void GatherStats()
        {
            if (_runState != null)
            {
                _isVictory = _runState.WasVictory;
                _battlesWon = _runState.BattlesWon;
                _totalScore = _runState.TotalScore;
                _maxCombo = _runState.MaxCombo;
                _bestAccuracy = _runState.BestAccuracy;
                _seed = _runState.Seed ?? "???";

                if (_runState.MapData != null)
                {
                    _totalNodes = _runState.MapData.AllNodes.Count;
                    _nodesCompleted = 0;
                    foreach (var n in _runState.MapData.AllNodes)
                    {
                        if (n.IsCompleted) _nodesCompleted++;
                    }
                }
            }

            var ph = PlayerHealth.Instance;
            if (ph != null)
            {
                _finalHP = ph.CurrentHP;
                _maxHP = ph.MaxHP;
            }
        }

        // =================================================================
        // ENTRANCE ANIMATION
        // =================================================================

        private IEnumerator AnimateEntrance()
        {
            // Fade BG overlay
            float t = 0f;
            Color bgTarget = _isVictory
                ? new Color(0.05f, 0.08f, 0.02f, 0.9f)
                : new Color(0.1f, 0.02f, 0.02f, 0.9f);

            while (t < _headerAnimDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / _headerAnimDuration);

                float scale = Mathf.Lerp(2f, 1f, Mathf.SmoothStep(0, 1, p));
                _headerText.rectTransform.localScale = Vector3.one * scale;

                Color hc = _headerText.color;
                hc.a = p;
                _headerText.color = hc;

                Color bgc = _bgOverlay.color;
                bgc.a = Mathf.Lerp(0, bgTarget.a, p);
                _bgOverlay.color = bgc;

                yield return null;
            }

            yield return FadeText(_subHeaderText, 0.3f);

            for (int i = 0; i < _statLabels.Length; i++)
            {
                StartCoroutine(FadeText(_statLabels[i], 0.2f));
                StartCoroutine(RollStat(i));
                yield return new WaitForSecondsRealtime(_statStaggerDelay);
            }

            yield return new WaitForSecondsRealtime(0.2f);
            yield return FadeText(_seedText, 0.3f);

            // Show buttons and activate navigation
            yield return new WaitForSecondsRealtime(0.3f);
            _newRunBtn.gameObject.SetActive(true);
            _retryBtn.gameObject.SetActive(true);
            _menuBtn.gameObject.SetActive(true);

            ActivateButtonNavigation();
        }

        private IEnumerator FadeText(Text text, float duration)
        {
            float t = 0f;
            Color c = text.color;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                c.a = Mathf.Clamp01(t / duration);
                text.color = c;
                yield return null;
            }

            c.a = 1f;
            text.color = c;
        }

        private IEnumerator RollStat(int index)
        {
            int target = GetStatTarget(index);
            float elapsed = 0f;
            Text val = _statValues[index];
            Color c = val.color;

            while (elapsed < _statRollDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _statRollDuration);
                t = Mathf.SmoothStep(0, 1, t);

                int display = Mathf.RoundToInt(Mathf.Lerp(0, target, t));
                val.text = FormatStatValue(index, display);

                c.a = Mathf.Clamp01(elapsed / 0.2f);
                val.color = c;

                yield return null;
            }

            val.text = FormatStatValue(index, target);
            c.a = 1f;
            val.color = c;
        }

        private int GetStatTarget(int index)
        {
            return index switch
            {
                0 => _battlesWon,
                1 => _nodesCompleted,
                2 => _totalScore,
                3 => _maxCombo,
                4 => Mathf.RoundToInt(_bestAccuracy * 100f),
                5 => _finalHP,
                _ => 0
            };
        }

        private string FormatStatValue(int index, int value)
        {
            return index switch
            {
                1 => $"{value} / {_totalNodes}",
                4 => $"{value}%",
                5 => $"{value} / {_maxHP}",
                _ => value.ToString()
            };
        }

        // =================================================================
        // BUTTON HANDLERS
        // =================================================================

        private void OnNewRun()
        {
            if (_runState != null)
                _runState.StartNewRun();

            ResetPlayerAndTransition(SceneTransitionManager.MAP_SCENE);
        }

        private void OnRetry()
        {
            string seed = _seed;

            if (_runState != null)
                _runState.StartNewRun(seed);

            ResetPlayerAndTransition(SceneTransitionManager.MAP_SCENE);
        }

        private void OnMenu()
        {
            if (_runState != null)
                _runState.StartNewRun();

            ResetPlayerAndTransition(SceneTransitionManager.MAIN_MENU_SCENE);
        }

        private void ResetPlayerAndTransition(string scene)
        {
            var ph = PlayerHealth.Instance;
            if (ph != null)
                ph.ResetForNewRun();

            // Disable buttons to prevent double press
            _newRunBtn.interactable = false;
            _retryBtn.interactable = false;
            _menuBtn.interactable = false;

            var tm = SceneTransitionManager.Instance;
            if (tm != null)
            {
                tm.GoTo(scene);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
            }
        }

        // =================================================================
        // UI CREATION
        // =================================================================

        private void CreateUI()
        {
            // Canvas
            GameObject canvasGO = new GameObject("SummaryCanvas");
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

            // EventSystem — uses InputSystemUIInputModule
            UIEventSystemProvider.EnsureEventSystem();

            // BG overlay
            GameObject bgGO = MakePanel(_canvasRT, "BG",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                _isVictory
                    ? new Color(0.05f, 0.08f, 0.02f, 0f)
                    : new Color(0.1f, 0.02f, 0.02f, 0f));
            bgGO.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            bgGO.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            _bgOverlay = bgGO.GetComponent<Image>();

            // Header
            _headerText = MakeText(_canvasRT, "Header",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -18), new Vector2(300, 20),
                12, TextAnchor.MiddleCenter,
                _isVictory ? new Color(1f, 0.85f, 0f, 0f) : new Color(1f, 0.25f, 0.25f, 0f));
            _headerText.fontStyle = FontStyle.Bold;
            _headerText.text = _isVictory ? "VICTORY" : "DEFEATED";

            // Subheader
            string subText = _isVictory
                ? "You conquered the dungeon!"
                : $"You were defeated. Made it through {_nodesCompleted} nodes.";
            _subHeaderText = MakeText(_canvasRT, "SubHeader",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -32), new Vector2(300, 12),
                6, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.7f, 0f));
            _subHeaderText.text = subText;

            // Stats
            string[] labels = { "Battles Won", "Nodes Cleared", "Score", "Max Combo", "Best Accuracy", "Final HP" };
            _statLabels = new Text[labels.Length];
            _statValues = new Text[labels.Length];

            float startY = -48f;
            float rowH = 13f;

            for (int i = 0; i < labels.Length; i++)
            {
                float y = startY - i * rowH;

                _statLabels[i] = MakeText(_canvasRT, $"StatLabel_{i}",
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(-40, y), new Vector2(120, 11),
                    6, TextAnchor.MiddleRight, new Color(0.7f, 0.7f, 0.7f, 0f));
                _statLabels[i].text = labels[i];

                _statValues[i] = MakeText(_canvasRT, $"StatValue_{i}",
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(40, y), new Vector2(80, 11),
                    6, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0f));
                _statValues[i].fontStyle = FontStyle.Bold;
                _statValues[i].text = "0";
            }

            // Seed
            _seedText = MakeText(_canvasRT, "Seed",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, startY - labels.Length * rowH - 8),
                new Vector2(200, 10),
                5, TextAnchor.MiddleCenter, new Color(0.5f, 0.5f, 0.5f, 0f));
            _seedText.text = $"Seed: {_seed}";

            // Buttons (hidden until animation completes)
            float btnY = 18f;
            float btnW = 60f;
            float btnH = 14f;
            float btnGap = 8f;

            _newRunBtn = MakeButton(_canvasRT, "NewRunBtn", "New Run",
                new Vector2(0.5f, 0), new Vector2(-(btnW + btnGap), btnY),
                new Vector2(btnW, btnH), new Color(0.2f, 0.5f, 0.2f));
            _newRunBtn.onClick.AddListener(OnNewRun);
            _newRunBtn.gameObject.SetActive(false);

            _retryBtn = MakeButton(_canvasRT, "RetryBtn", "Retry Seed",
                new Vector2(0.5f, 0), new Vector2(0, btnY),
                new Vector2(btnW, btnH), new Color(0.3f, 0.3f, 0.6f));
            _retryBtn.onClick.AddListener(OnRetry);
            _retryBtn.gameObject.SetActive(false);

            _menuBtn = MakeButton(_canvasRT, "MenuBtn", "Menu",
                new Vector2(0.5f, 0), new Vector2(btnW + btnGap, btnY),
                new Vector2(btnW, btnH), new Color(0.4f, 0.2f, 0.2f));
            _menuBtn.onClick.AddListener(OnMenu);
            _menuBtn.gameObject.SetActive(false);
        }

        // =================================================================
        // UI HELPERS
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

        private static Button MakeButton(RectTransform parent, string name, string label,
            Vector2 anchor, Vector2 pos, Vector2 size, Color bgColor)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            obj.GetComponent<Image>().color = bgColor;

            // Button label
            GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtGO.transform.SetParent(obj.transform, false);

            RectTransform txtRT = txtGO.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero;
            txtRT.offsetMax = Vector2.zero;

            Text t = txtGO.GetComponent<Text>();
            t.font = Font.CreateDynamicFontFromOSFont("Arial", 6);
            t.fontSize = 6;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.fontStyle = FontStyle.Bold;
            t.text = label;

            return obj.GetComponent<Button>();
        }
    }
}