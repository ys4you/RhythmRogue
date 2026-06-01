using System;
using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.UI;
using RhythmRogue.UI.Navigation;
using RhythmRogue.Core.Audio;
using RhythmRogue.Data;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Shared pause/menu overlay used by both the battle scene and the map scene.
    /// Offers Resume, Settings, and Quit to Menu. The owning scene decides what
    /// "resume" and "quit" mean by subscribing to the events (battle resumes the
    /// conductor; the map just closes the overlay).
    ///
    /// The Settings button opens an in-place sub-panel with the run-relevant sliders
    /// (scroll speed, audio offset, master/music/SFX volume) bound to the same
    /// AudioSettings / ScrollSpeedSetting the main menu uses, so changes are consistent
    /// and persist, without leaving the current scene or losing run state.
    ///
    /// SOLID:
    ///   S - Presents the pause overlay + its settings sub-panel. It does not pause the
    ///       conductor or change scenes itself; it raises events the scene handles.
    ///   D - Reads/writes settings through the AudioSettings / ScrollSpeedSetting
    ///       abstractions rather than touching AudioManager or PlayerPrefs directly.
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
        private GameObject _settingsGroup; // sliders + Back
        private Button _resumeBtn, _settingsBtn, _quitBtn, _settingsBackBtn;
        private Text _titleLabel;
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

            // The key press that opened this menu (Escape, handled by GlobalPauseManager)
            // is read by this same input system. Without suppression, our own UICancelHandler
            // would see that very same Escape this frame and immediately fire resume, closing
            // the menu the instant it opens. Suppress cancel for one frame to avoid that.
            if (_cancelHandler != null) _cancelHandler.SuppressForOneFrame();
        }

        public void Hide() { if (_panel != null) _panel.SetActive(false); }

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
            _settingsGroup.SetActive(false);
            // Base cancel = resume while on the main group.
            _cancelHandler.SetBaseAction(OnResume);
            _focusSetter.FocusOn(_resumeBtn.gameObject);
        }

        private void ShowSettingsGroup()
        {
            _mainGroup.SetActive(false);
            _settingsGroup.SetActive(true);
            // While in settings, cancel goes back to the main group rather than resuming.
            _cancelHandler.SetBaseAction(CloseSettings);
            _focusSetter.FocusOn(_settingsBackBtn.gameObject);
        }

        private void OpenSettings() => ShowSettingsGroup();
        private void CloseSettings() => ShowMainGroup();

        // ============================================================
        // UI
        // ============================================================

        private void CreateUI()
        {
            // IMPORTANT: do NOT parent the canvas under this component's transform.
            // This PauseMenu lives under a DontDestroyOnLoad object (GlobalPauseManager),
            // and nesting a ScreenSpaceOverlay canvas under a non-canvas parent that carries
            // a non-identity/zero scale makes the canvas render at the wrong size (often
            // invisible). Creating the canvas as a ROOT object guarantees Unity drives it to
            // full screen space. We keep it alive across scene loads with its own
            // DontDestroyOnLoad so it travels with the persistent manager logically.
            var canvasGO = new GameObject("PauseCanvas");
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // High sorting order so the pause overlay draws above all in-scene UI
            // (map canvas is 50, battle UI similar). Stays below the SceneTransitionManager
            // fade overlay (9999) so scene-change fades still cover it.
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

            // Card behind everything
            MakePanel(panelRT, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 720), UIHelpers.BgSurface);

            _titleLabel = MakeText(panelRT, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(600, 100), 60, TextAnchor.MiddleCenter, UIHelpers.WarmGold);
            _titleLabel.fontStyle = FontStyle.Bold;
            _titleLabel.text = _titleText;

            BuildMainGroup(panelRT);
            BuildSettingsGroup(panelRT);

            _focusSetter = gameObject.AddComponent<UIFocusSetter>();
            _cancelHandler = gameObject.AddComponent<UICancelHandler>();
            _cancelHandler.SetBaseAction(OnResume);
        }

        private void BuildMainGroup(RectTransform parent)
        {
            _mainGroup = MakeGroup(parent, "MainGroup");
            var rt = _mainGroup.GetComponent<RectTransform>();

            float btnW = 420f, btnH = 80f, gap = 24f;
            float y = 120f;

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

        private void BuildSettingsGroup(RectTransform parent)
        {
            _settingsGroup = MakeGroup(parent, "SettingsGroup");
            var rt = _settingsGroup.GetComponent<RectTransform>();

            // Slider rows, top to bottom. Each binds to the shared settings singletons so
            // values are consistent with the main menu and persist via PlayerPrefs.
            float sliderY = 150f, sliderGap = 78f;

            // Scroll Speed
            var scrollSlider = MakeSliderRow(rt, "Scroll Speed", sliderY, 0.5f, 6.0f, ScrollSpeedSetting.Multiplier, out var scrollVal);
            scrollVal.text = ScrollSpeedSetting.DisplayString;
            scrollSlider.onValueChanged.AddListener(v =>
            {
                float r = Mathf.Round(v * 10f) / 10f;
                ScrollSpeedSetting.Multiplier = r;
                scrollVal.text = ScrollSpeedSetting.DisplayString;
            });

            // Audio Offset (ms)
            float savedOffset = PlayerPrefs.GetFloat("audioOffset", 0f);
            var offsetSlider = MakeSliderRow(rt, "Audio Offset", sliderY - sliderGap, -100f, 100f, savedOffset, out var offsetVal);
            offsetSlider.wholeNumbers = true;
            offsetVal.text = $"{savedOffset:+0;-0;0} ms";
            offsetSlider.onValueChanged.AddListener(v =>
            {
                PlayerPrefs.SetFloat("audioOffset", v);
                PlayerPrefs.Save();
                offsetVal.text = $"{v:+0;-0;0} ms";
            });

            // Master / Music / SFX volume
            var masterSlider = MakeSliderRow(rt, "Master Vol", sliderY - sliderGap * 2, 0f, 1f, RhythmRogue.Core.Audio.AudioSettings.MasterVolume, out _);
            masterSlider.onValueChanged.AddListener(v => RhythmRogue.Core.Audio.AudioSettings.MasterVolume = v);

            var musicSlider = MakeSliderRow(rt, "Music Vol", sliderY - sliderGap * 3, 0f, 1f, RhythmRogue.Core.Audio.AudioSettings.MusicVolume, out _);
            musicSlider.onValueChanged.AddListener(v => RhythmRogue.Core.Audio.AudioSettings.MusicVolume = v);

            var sfxSlider = MakeSliderRow(rt, "SFX Vol", sliderY - sliderGap * 4, 0f, 1f, RhythmRogue.Core.Audio.AudioSettings.SfxVolume, out _);
            sfxSlider.onValueChanged.AddListener(v => RhythmRogue.Core.Audio.AudioSettings.SfxVolume = v);

            // Back button returns to the main group.
            _settingsBackBtn = MakeButton(rt, "SettingsBackBtn", "Back", new Vector2(0.5f, 0f), new Vector2(0, 50), new Vector2(320, 70), UIHelpers.RustOrange);
            _settingsBackBtn.onClick.AddListener(CloseSettings);
            UISelectableStyle.Apply(_settingsBackBtn);

            // Vertical navigation: sliders then Back.
            UINavigationHelper.WireVerticalNoWrap(scrollSlider, offsetSlider, masterSlider, musicSlider, sfxSlider);
            UINavigationHelper.AddLink(sfxSlider, down: _settingsBackBtn);
            UINavigationHelper.AddLink(_settingsBackBtn, up: sfxSlider);
            UISelectableStyle.ApplySlider(scrollSlider); UISelectableStyle.ApplySlider(offsetSlider);
            UISelectableStyle.ApplySlider(masterSlider); UISelectableStyle.ApplySlider(musicSlider);
            UISelectableStyle.ApplySlider(sfxSlider);
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

        private Slider MakeSliderRow(RectTransform parent, string label, float y, float min, float max, float value, out Text valueText)
        {
            // Label on the left, slider in the middle, value on the right.
            MakeText(parent, label + "_Label", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-260, y), new Vector2(220, 50), 22, TextAnchor.MiddleLeft, UIHelpers.OffWhite).text = label;

            var sliderGO = new GameObject(label + "_Slider", typeof(RectTransform), typeof(Slider));
            sliderGO.transform.SetParent(parent, false);
            var sRT = sliderGO.GetComponent<RectTransform>();
            sRT.anchorMin = sRT.anchorMax = new Vector2(0.5f, 0.5f);
            sRT.pivot = new Vector2(0.5f, 0.5f);
            sRT.anchoredPosition = new Vector2(60, y);
            sRT.sizeDelta = new Vector2(360, 30);

            // Background
            var bg = MakePanel(sRT, "Background", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 12), UIHelpers.BgDeep);
            var bgRT = bg.GetComponent<RectTransform>(); bgRT.anchorMin = new Vector2(0, 0.5f); bgRT.anchorMax = new Vector2(1, 0.5f); bgRT.offsetMin = new Vector2(0, -6); bgRT.offsetMax = new Vector2(0, 6);

            // Fill
            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(sRT, false);
            var faRT = fillArea.GetComponent<RectTransform>();
            faRT.anchorMin = new Vector2(0, 0.5f); faRT.anchorMax = new Vector2(1, 0.5f);
            faRT.offsetMin = new Vector2(0, -6); faRT.offsetMax = new Vector2(0, 6);
            var fill = MakePanel(faRT.GetComponent<RectTransform>(), "Fill", new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, UIHelpers.AmberOrange);
            var fillRT = fill.GetComponent<RectTransform>(); fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one; fillRT.offsetMin = Vector2.zero; fillRT.offsetMax = Vector2.zero;

            // Handle
            var handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(sRT, false);
            var haRT = handleArea.GetComponent<RectTransform>();
            haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one; haRT.offsetMin = Vector2.zero; haRT.offsetMax = Vector2.zero;
            var handle = MakePanel(haRT.GetComponent<RectTransform>(), "Handle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24, 36), UIHelpers.WarmGold);

            var slider = sliderGO.GetComponent<Slider>();
            slider.fillRect = fillRT;
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min; slider.maxValue = max; slider.value = value;

            valueText = MakeText(parent, label + "_Value", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(290, y), new Vector2(120, 50), 22, TextAnchor.MiddleRight, UIHelpers.WarmGold);
            valueText.text = value.ToString("0.##");

            return slider;
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
