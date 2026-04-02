using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.Core;
using RhythmRogue.Battle;
using RhythmRogue.Util;
using RhythmRogue.UI.Navigation;

namespace RhythmRogue.UI
{
    /// <summary>
    /// Main menu — the player's entry point.
    /// 
    /// Provides:
    ///   - New Run (random seed)
    ///   - Seed Entry (custom seed)
    ///   - Settings (audio offset, volume)
    ///   - Quit
    ///   - Version display
    /// 
    /// Fully keyboard/gamepad navigable:
    ///   - Arrow keys / D-pad navigate vertically between buttons
    ///   - Enter / Space / A confirms
    ///   - Escape / B closes settings panel or triggers quit
    ///   - Settings sliders: Left/Right adjusts, Up/Down navigates between them
    /// 
    /// Code-generated UI, sized for 384×216.
    /// </summary>
    public class MainMenuScreen : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("References")]
        [SerializeField] private RunState _runState;

        [Header("Version")]
        [SerializeField] private string _versionText = "Prototype v0.1";

        // =================================================================
        // UI REFS
        // =================================================================

        private Canvas _canvas;
        private RectTransform _canvasRT;
        private InputField _seedInput;
        private GameObject _settingsPanel;
        private Slider _offsetSlider;
        private Text _offsetValue;
        private Slider _masterVolSlider;
        private Slider _musicVolSlider;
        private Slider _sfxVolSlider;

        // Button references for navigation wiring
        private Button _newRunBtn;
        private Button _seedGoBtn;
        private Button _settingsBtn;
        private Button _quitBtn;
        private Button _settingsCloseBtn;

        // Navigation components
        private UIFocusSetter _focusSetter;
        private UICancelHandler _cancelHandler;

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Start()
        {
            CreateUI();
            SetupNavigation();
        }

        // =================================================================
        // NAVIGATION SETUP
        // =================================================================

        private void SetupNavigation()
        {
            // Wire vertical chain: New Run → Seed Go → Settings → Quit
            UINavigationHelper.WireVerticalNoWrap(_newRunBtn, _seedGoBtn, _settingsBtn, _quitBtn);

            // Seed input field: Down goes to Settings (skip Go button since it's beside the field)
            UINavigationHelper.Wire(_seedInput,
                up: _newRunBtn,
                down: _settingsBtn,
                right: _seedGoBtn);

            // Seed Go button: connect to neighbors
            UINavigationHelper.AddLink(_seedGoBtn, left: _seedInput);

            // Seed input: when done editing, move focus to Go button
            _seedInput.onEndEdit.AddListener(_ =>
            {
                if (_seedInput.isFocused) return;
                _focusSetter.FocusOn(_seedGoBtn.gameObject);
            });

            // Apply visual focus styles to all interactive elements
            UISelectableStyle.Apply(_newRunBtn);
            UISelectableStyle.Apply(_seedGoBtn);
            UISelectableStyle.Apply(_settingsBtn);
            UISelectableStyle.Apply(_quitBtn);
            UISelectableStyle.Apply(_seedInput);

            // Focus setter — auto-selects New Run on enable
            _focusSetter = gameObject.AddComponent<UIFocusSetter>();
            _focusSetter.SetDefault(_newRunBtn.gameObject);

            // Cancel handler — Escape triggers quit (or closes settings if open)
            _cancelHandler = gameObject.AddComponent<UICancelHandler>();
            _cancelHandler.SetBaseAction(OnQuit);
        }

        // =================================================================
        // ACTIONS
        // =================================================================

        private void OnNewRun()
        {
            StartRun(null);
        }

        private void OnSeededRun()
        {
            string seed = _seedInput != null ? _seedInput.text.Trim() : "";
            StartRun(string.IsNullOrEmpty(seed) ? null : seed);
        }

        private void StartRun(string seed)
        {
            if (_runState == null)
            {
                GameLog.Error("[MainMenu] No RunState assigned!");
                return;
            }

            if (SceneTransitionManager.Instance != null &&
                SceneTransitionManager.Instance.IsTransitioning)
                return;

            _runState.StartNewRun(seed);

            var ph = PlayerHealth.Instance;
            if (ph != null)
                ph.ResetForNewRun();

            GameLog.Info($"[MainMenu] Starting run. Seed: {_runState.Seed}");

            var tm = SceneTransitionManager.Instance;
            if (tm != null)
                tm.GoToMap();
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    SceneTransitionManager.MAP_SCENE);
        }

        private void OnSettings()
        {
            _settingsPanel.SetActive(true);

            // Push cancel handler so Escape closes settings first
            _cancelHandler.Push(OnSettingsClose);

            // Wire settings sliders vertically
            UINavigationHelper.WireVerticalNoWrap(
                _offsetSlider, _masterVolSlider, _musicVolSlider, _sfxVolSlider);

            // Close button below last slider
            UINavigationHelper.AddLink(_sfxVolSlider, down: _settingsCloseBtn);
            UINavigationHelper.Wire(_settingsCloseBtn, up: _sfxVolSlider);

            // Apply slider styles
            UISelectableStyle.ApplySlider(_offsetSlider);
            UISelectableStyle.ApplySlider(_masterVolSlider);
            UISelectableStyle.ApplySlider(_musicVolSlider);
            UISelectableStyle.ApplySlider(_sfxVolSlider);
            UISelectableStyle.Apply(_settingsCloseBtn);

            // Focus the first slider
            _focusSetter.FocusOn(_offsetSlider.gameObject);
        }

        private void OnSettingsClose()
        {
            _settingsPanel.SetActive(false);

            // Save prefs
            PlayerPrefs.SetFloat("audioOffset", _offsetSlider.value);
            PlayerPrefs.SetFloat("masterVolume", _masterVolSlider.value);
            PlayerPrefs.SetFloat("musicVolume", _musicVolSlider.value);
            PlayerPrefs.SetFloat("sfxVolume", _sfxVolSlider.value);
            PlayerPrefs.Save();

            // Restore focus to the Settings button
            _focusSetter.FocusOn(_settingsBtn.gameObject);
        }

        private void OnQuit()
        {
            GameLog.Info("[MainMenu] Quit");
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        // =================================================================
        // UI CREATION
        // =================================================================

        private void CreateUI()
        {
            // Canvas
            GameObject canvasGO = new GameObject("MenuCanvas");
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

            // Background
            GameObject bgGO = MakePanel(_canvasRT, "BG",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                new Color(0.06f, 0.06f, 0.1f));
            bgGO.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            bgGO.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // Decorative accent line
            MakePanel(_canvasRT, "AccentLine",
                new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(200, 1),
                new Color(0.4f, 0.3f, 0.6f, 0.4f));

            // --- TITLE ---
            Text title = MakeText(_canvasRT, "Title",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -30), new Vector2(300, 24),
                14, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0f));
            title.fontStyle = FontStyle.Bold;
            title.text = "RHYTHM ROGUE";

            Text subtitle = MakeText(_canvasRT, "Subtitle",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -46), new Vector2(200, 10),
                5, TextAnchor.MiddleCenter, new Color(0.6f, 0.5f, 0.7f));
            subtitle.text = "a rhythm roguelike";

            // --- BUTTONS ---
            float btnW = 80f;
            float btnH = 16f;
            float startY = -68f;
            float gap = 20f;

            _newRunBtn = MakeMenuButton("New Run", startY, btnW, btnH, new Color(0.2f, 0.45f, 0.2f), OnNewRun);
            CreateSeedEntry(startY - gap, btnW, btnH);
            _settingsBtn = MakeMenuButton("Settings", startY - gap * 2, btnW, btnH, new Color(0.3f, 0.3f, 0.45f), OnSettings);
            _quitBtn = MakeMenuButton("Quit", startY - gap * 3, btnW, btnH, new Color(0.4f, 0.2f, 0.2f), OnQuit);

            // --- VERSION ---
            Text version = MakeText(_canvasRT, "Version",
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-4, 4), new Vector2(80, 8),
                4, TextAnchor.MiddleRight, new Color(0.35f, 0.35f, 0.35f));
            version.text = _versionText;

            // --- SETTINGS PANEL (hidden) ---
            CreateSettingsPanel();
        }

        // =================================================================
        // SEED ENTRY
        // =================================================================

        private void CreateSeedEntry(float y, float btnW, float btnH)
        {
            // Container
            GameObject container = new GameObject("SeedEntry", typeof(RectTransform));
            container.transform.SetParent(_canvasRT, false);

            RectTransform crt = container.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 1f);
            crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(0, y);
            crt.sizeDelta = new Vector2(btnW + 40, btnH);

            // Input field background
            GameObject inputGO = new GameObject("SeedInput", typeof(RectTransform), typeof(Image), typeof(InputField));
            inputGO.transform.SetParent(crt, false);

            RectTransform irt = inputGO.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0, 0.5f);
            irt.anchorMax = new Vector2(0, 0.5f);
            irt.pivot = new Vector2(0, 0.5f);
            irt.anchoredPosition = new Vector2(-btnW * 0.5f - 20, 0);
            irt.sizeDelta = new Vector2(btnW - 10, btnH);

            Image inputBG = inputGO.GetComponent<Image>();
            inputBG.color = new Color(0.15f, 0.15f, 0.2f);

            // Input text child
            GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(inputGO.transform, false);

            RectTransform trt = textGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(3, 0);
            trt.offsetMax = new Vector2(-3, 0);

            Text inputText = textGO.GetComponent<Text>();
            inputText.font = Font.CreateDynamicFontFromOSFont("Arial", 6);
            inputText.fontSize = 6;
            inputText.alignment = TextAnchor.MiddleLeft;
            inputText.color = Color.white;
            inputText.supportRichText = false;

            // Placeholder
            GameObject placeholderGO = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            placeholderGO.transform.SetParent(inputGO.transform, false);

            RectTransform prt = placeholderGO.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(3, 0);
            prt.offsetMax = new Vector2(-3, 0);

            Text placeholder = placeholderGO.GetComponent<Text>();
            placeholder.font = Font.CreateDynamicFontFromOSFont("Arial", 6);
            placeholder.fontSize = 6;
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.color = new Color(0.4f, 0.4f, 0.4f);
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.text = "Enter seed...";

            // Wire InputField
            _seedInput = inputGO.GetComponent<InputField>();
            _seedInput.textComponent = inputText;
            _seedInput.placeholder = placeholder;
            _seedInput.characterLimit = 20;

            // Go button
            GameObject goBtnGO = MakePanel(crt, "GoBtn",
                new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                new Vector2(btnW * 0.5f + 20, 0), new Vector2(28, btnH),
                new Color(0.2f, 0.45f, 0.2f));
            _seedGoBtn = goBtnGO.AddComponent<Button>();
            _seedGoBtn.onClick.AddListener(OnSeededRun);

            MakeText(goBtnGO.GetComponent<RectTransform>(), "GoText",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(28, btnH),
                5, TextAnchor.MiddleCenter, Color.white).fontStyle = FontStyle.Bold;
            goBtnGO.transform.GetChild(0).GetComponent<Text>().text = "Go";
        }

        // =================================================================
        // SETTINGS PANEL
        // =================================================================

        private void CreateSettingsPanel()
        {
            // Dim overlay
            _settingsPanel = MakePanel(_canvasRT, "SettingsPanel",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                new Color(0, 0, 0, 0.7f));
            _settingsPanel.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            _settingsPanel.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            RectTransform panelRT = _settingsPanel.GetComponent<RectTransform>();

            // Panel card
            GameObject card = MakePanel(panelRT, "Card",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(200, 140),
                new Color(0.1f, 0.1f, 0.15f, 0.95f));
            RectTransform cardRT = card.GetComponent<RectTransform>();

            // Title
            Text settingsTitle = MakeText(cardRT, "SettingsTitle",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -8), new Vector2(180, 14),
                8, TextAnchor.MiddleCenter, Color.white);
            settingsTitle.fontStyle = FontStyle.Bold;
            settingsTitle.text = "Settings";

            // Audio offset
            float sliderY = -28f;
            float sliderGap = 24f;

            float savedOffset = PlayerPrefs.GetFloat("audioOffset", 0f);
            _offsetSlider = CreateSliderRow(cardRT, "Audio Offset", sliderY,
                -100f, 100f, savedOffset, out _offsetValue);
            _offsetSlider.wholeNumbers = true;
            _offsetSlider.onValueChanged.AddListener(v =>
                _offsetValue.text = $"{v:+0;-0;0} ms");
            _offsetValue.text = $"{savedOffset:+0;-0;0} ms";

            // Master volume
            float savedMaster = PlayerPrefs.GetFloat("masterVolume", 1f);
            _masterVolSlider = CreateSliderRow(cardRT, "Master Vol", sliderY - sliderGap,
                0f, 1f, savedMaster, out _);

            // Music volume
            float savedMusic = PlayerPrefs.GetFloat("musicVolume", 1f);
            _musicVolSlider = CreateSliderRow(cardRT, "Music Vol", sliderY - sliderGap * 2,
                0f, 1f, savedMusic, out _);

            // SFX volume
            float savedSFX = PlayerPrefs.GetFloat("sfxVolume", 1f);
            _sfxVolSlider = CreateSliderRow(cardRT, "SFX Vol", sliderY - sliderGap * 3,
                0f, 1f, savedSFX, out _);

            // Close button
            GameObject closeBtnGO = MakePanel(cardRT, "CloseBtn",
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 8), new Vector2(50, 14),
                new Color(0.35f, 0.2f, 0.2f));
            _settingsCloseBtn = closeBtnGO.AddComponent<Button>();
            _settingsCloseBtn.onClick.AddListener(OnSettingsClose);

            MakeText(closeBtnGO.GetComponent<RectTransform>(), "CloseText",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(50, 14),
                6, TextAnchor.MiddleCenter, Color.white).text = "Back";

            _settingsPanel.SetActive(false);
        }

        private Slider CreateSliderRow(RectTransform parent, string label, float y,
            float min, float max, float value, out Text valueText)
        {
            // Label
            MakeText(parent, $"{label}_Label",
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(10, y), new Vector2(60, 10),
                5, TextAnchor.MiddleLeft, new Color(0.7f, 0.7f, 0.7f)).text = label;

            // Slider
            GameObject sliderGO = CreateSliderGO(parent, $"{label}_Slider",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(15, y - 10), new Vector2(120, 8));

            Slider slider = sliderGO.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;

            // Value text
            valueText = MakeText(parent, $"{label}_Value",
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-10, y), new Vector2(40, 10),
                5, TextAnchor.MiddleRight, Color.white);

            // Default display for volume sliders
            if (max <= 1f)
            {
                Text volText = valueText;
                volText.text = $"{Mathf.RoundToInt(value * 100)}%";
                slider.onValueChanged.AddListener(v =>
                    volText.text = $"{Mathf.RoundToInt(v * 100)}%");
            }

            return slider;
        }

        // =================================================================
        // MENU BUTTON HELPER — returns Button for navigation wiring
        // =================================================================

        private Button MakeMenuButton(string label, float y, float w, float h,
            Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnGO = MakePanel(_canvasRT, $"Btn_{label}",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0, y), new Vector2(w, h), bgColor);
            Button btn = btnGO.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            Text txt = MakeText(btnGO.GetComponent<RectTransform>(), "Text",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(w, h),
                7, TextAnchor.MiddleCenter, Color.white);
            txt.fontStyle = FontStyle.Bold;
            txt.text = label;

            return btn;
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

        private static GameObject CreateSliderGO(RectTransform parent, string name,
            Vector2 ancMin, Vector2 ancMax, Vector2 pivot,
            Vector2 pos, Vector2 size)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);

            RectTransform rootRT = root.GetComponent<RectTransform>();
            rootRT.anchorMin = ancMin;
            rootRT.anchorMax = ancMax;
            rootRT.pivot = pivot;
            rootRT.anchoredPosition = pos;
            rootRT.sizeDelta = size;

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(root.transform, false);
            RectTransform bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            RectTransform fillAreaRT = fillArea.GetComponent<RectTransform>();
            fillAreaRT.anchorMin = Vector2.zero;
            fillAreaRT.anchorMax = Vector2.one;
            fillAreaRT.offsetMin = Vector2.zero;
            fillAreaRT.offsetMax = Vector2.zero;

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = new Color(0.4f, 0.5f, 0.7f);

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(root.transform, false);
            RectTransform handleAreaRT = handleArea.GetComponent<RectTransform>();
            handleAreaRT.anchorMin = Vector2.zero;
            handleAreaRT.anchorMax = Vector2.one;
            handleAreaRT.offsetMin = Vector2.zero;
            handleAreaRT.offsetMax = Vector2.zero;

            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            RectTransform handleRT = handle.GetComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(6, size.y + 2);
            handle.GetComponent<Image>().color = Color.white;

            Slider slider = root.GetComponent<Slider>();
            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.targetGraphic = handle.GetComponent<Image>();

            return root;
        }
    }
}