using System;
using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.UI;
using RhythmRogue.UI.Navigation;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Shared pause/menu overlay used by both the battle scene and the map scene.
    /// Offers Resume, Settings, and Quit to Menu. The owning scene decides what
    /// "resume" and "quit" mean by subscribing to the events (battle resumes the
    /// conductor; the map just closes the overlay).
    ///
    /// The Settings button opens the shared SettingsPanel component, the SAME full
    /// Audio / Controls / Display settings the main menu uses. No settings UI is built
    /// here; it's delegated, so there's one settings implementation across the game.
    ///
    /// SOLID:
    ///   S - Presents the pause overlay (Resume/Settings/Quit) and hosts the shared
    ///       settings panel. It does not build settings UI or pause the conductor itself.
    ///   D - Delegates all settings to SettingsPanel; raises Resume/Quit events the
    ///       owning scene handles.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public event Action OnResumeRequested;
        public event Action OnQuitRequested;

        [Tooltip("Title shown at the top of the pause overlay. 'PAUSED' for battle; the map sets this to 'MENU'.")]
        [SerializeField] private string _titleText = "PAUSED";

        private Canvas _canvas;
        private RectTransform _canvasRT;
        private GameObject _panel;
        private GameObject _mainGroup;     // Resume / Settings / Quit
        private Button _resumeBtn, _settingsBtn, _quitBtn;
        private Text _titleLabel;
        private SettingsPanel _settings;
        private UIFocusSetter _focusSetter;
        private UICancelHandler _cancelHandler;

        private void Awake()
        {
            CreateUI();
            Hide();
        }

        /// <summary>Set the overlay title (e.g. "PAUSED" in battle, "MENU" on the map). Call before Show.</summary>
        public void SetTitle(string title)
        {
            _titleText = title;
            if (_titleLabel != null) _titleLabel.text = title;
        }

        public void Show()
        {
            if (_canvas != null) _canvas.gameObject.SetActive(true);
            if (_panel != null) _panel.SetActive(true);
            ShowMainGroup();
            if (_focusSetter != null && _resumeBtn != null) _focusSetter.FocusOn(_resumeBtn.gameObject);

            // The key press that opened this menu (Escape, handled by GlobalPauseManager or
            // BattleManager) is read by this same input system. Without suppression our own
            // UICancelHandler would see that same Escape this frame and immediately resume,
            // closing the menu the instant it opens. Suppress cancel for one frame.
            if (_cancelHandler != null) _cancelHandler.SuppressForOneFrame();
        }

        public void Hide()
        {
            if (_settings != null && _settings.IsOpen) _settings.Close();
            if (_panel != null) _panel.SetActive(false);
        }

        // When this component is destroyed (e.g. a duplicate manager is cleaned up), also
        // destroy the root canvas we spawned, so it doesn't leak as an orphaned overlay.
        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        private void OnResume() => OnResumeRequested?.Invoke();
        private void OnQuit() => OnQuitRequested?.Invoke();

        // ---- group switching ----

        private void ShowMainGroup()
        {
            _mainGroup.SetActive(true);
            // Base cancel = resume while on the main group.
            if (_cancelHandler != null) _cancelHandler.SetBaseAction(OnResume);
            if (_focusSetter != null) _focusSetter.FocusOn(_resumeBtn.gameObject);
        }

        private void OpenSettings()
        {
            // Hand off to the shared settings panel. It pushes its own cancel handler so
            // Escape inside settings backs out to here rather than resuming.
            _mainGroup.SetActive(false);
            _settings.Open();
        }

        private void OnSettingsClosed()
        {
            // Settings panel closed itself; return to the main pause group.
            ShowMainGroup();
        }

        // ============================================================
        // UI
        // ============================================================

        private void CreateUI()
        {
            // Create the canvas as a ROOT object (not parented under this component). This
            // PauseMenu can live under a DontDestroyOnLoad object (GlobalPauseManager);
            // nesting a ScreenSpaceOverlay canvas under a non-canvas parent could make it
            // render at the wrong size. A root canvas is reliably driven to full screen.
            var canvasGO = new GameObject("PauseCanvas");
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above all in-scene UI (map canvas 50), below the fade overlay (9999).
            _canvas.sortingOrder = 500;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            _canvasRT = canvasGO.GetComponent<RectTransform>();
            DontDestroyOnLoad(canvasGO);
            UIEventSystemProvider.EnsureEventSystem();

            // Dim overlay
            _panel = MakePanel(_canvasRT, "PausePanel", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(UIHelpers.BgDeep.r, UIHelpers.BgDeep.g, UIHelpers.BgDeep.b, 0.85f));
            _panel.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            _panel.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            var panelRT = _panel.GetComponent<RectTransform>();

            // Card behind the main group
            MakePanel(panelRT, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 560), UIHelpers.BgSurface);

            _titleLabel = MakeText(panelRT, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(600, 100), 60, TextAnchor.MiddleCenter, UIHelpers.WarmGold);
            _titleLabel.fontStyle = FontStyle.Bold;
            _titleLabel.text = _titleText;

            // Focus + cancel handlers, created before the groups so wiring can use them.
            _focusSetter = gameObject.AddComponent<UIFocusSetter>();
            _cancelHandler = gameObject.AddComponent<UICancelHandler>();
            _cancelHandler.SetBaseAction(OnResume);

            BuildMainGroup(panelRT);

            // Shared settings panel lives on the same canvas, hidden until opened.
            _settings = gameObject.AddComponent<SettingsPanel>();
            _settings.Build(_canvasRT, _focusSetter, _cancelHandler);
            _settings.OnCloseRequested += OnSettingsClosed;
        }

        private void BuildMainGroup(RectTransform parent)
        {
            _mainGroup = MakeGroup(parent, "MainGroup");
            var rt = _mainGroup.GetComponent<RectTransform>();

            float btnW = 420f, btnH = 80f, gap = 24f;
            float y = 60f;

            _resumeBtn = MakeButton(rt, "ResumeBtn", "Resume", new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(btnW, btnH), UIHelpers.RustOrange);
            _resumeBtn.onClick.AddListener(OnResume);

            _settingsBtn = MakeButton(rt, "SettingsBtn", "Settings", new Vector2(0.5f, 0.5f), new Vector2(0, y - (btnH + gap)), new Vector2(btnW, btnH), UIHelpers.BgLight);
            _settingsBtn.onClick.AddListener(OpenSettings);

            _quitBtn = MakeButton(rt, "QuitBtn", "Quit to Menu", new Vector2(0.5f, 0.5f), new Vector2(0, y - 2 * (btnH + gap)), new Vector2(btnW, btnH), UIHelpers.Shadow);
            _quitBtn.onClick.AddListener(OnQuit);

            var tip = MakeText(rt, "Tip", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 50), new Vector2(800, 50), 20, TextAnchor.MiddleCenter, UIHelpers.AmberOrange);
            tip.text = "Press Escape to resume";

            UINavigationHelper.WireVerticalNoWrap(_resumeBtn, _settingsBtn, _quitBtn);
            UISelectableStyle.Apply(_resumeBtn); UISelectableStyle.Apply(_settingsBtn); UISelectableStyle.Apply(_quitBtn);
        }

        // ============================================================
        // Builders
        // ============================================================

        private static GameObject MakeGroup(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return go;
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
            txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;
            var t = txtGO.GetComponent<Text>();
            t.font = UIHelpers.GetDefaultFont(28);
            t.fontSize = 28; t.alignment = TextAnchor.MiddleCenter;
            t.color = UIHelpers.OffWhite; t.fontStyle = FontStyle.Bold; t.text = label;
            return obj.GetComponent<Button>();
        }
    }
}
