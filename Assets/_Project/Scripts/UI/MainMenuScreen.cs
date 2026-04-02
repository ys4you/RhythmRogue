using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
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
    ///   - Settings (audio + controls with rebinding)
    ///   - Quit
    ///   - Version display
    /// 
    /// Settings panel has two sub-panels:
    ///   - Audio: offset, master/music/sfx volume
    ///   - Controls: 4 lane rebind buttons, reset to defaults
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
        [SerializeField] private InputActionAsset _rhythmActions;

        [Header("Version")]
        [SerializeField] private string _versionText = "Prototype v0.1";

        // =================================================================
        // UI REFS
        // =================================================================

        private Canvas _canvas;
        private RectTransform _canvasRT;
        private InputField _seedInput;

        // Settings
        private GameObject _settingsPanel;
        private GameObject _audioPanel;
        private GameObject _controlsPanel;
        private Slider _offsetSlider;
        private Text _offsetValue;
        private Slider _masterVolSlider;
        private Slider _musicVolSlider;
        private Slider _sfxVolSlider;

        // Controls panel
        private Text[] _bindingLabels;       // "Left Arrow" display per lane
        private Button[] _rebindButtons;     // Click to rebind per lane
        private Text[] _rebindButtonTexts;   // Text on the rebind buttons
        private Text[] _secondaryLabels;     // Secondary binding display
        private Button[] _secondaryButtons;  // Secondary rebind buttons
        private Text[] _secondaryTexts;      // Text on secondary buttons
        private Button _resetDefaultsBtn;
        private Text _conflictWarning;

        // Tab buttons
        private Button _audioTabBtn;
        private Button _controlsTabBtn;

        // Main menu buttons
        private Button _newRunBtn;
        private Button _seedGoBtn;
        private Button _settingsBtn;
        private Button _quitBtn;
        private Button _settingsCloseBtn;

        // Navigation
        private UIFocusSetter _focusSetter;
        private UICancelHandler _cancelHandler;

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Start()
        {
            // Initialize keybind manager
            if (_rhythmActions != null)
                KeybindManager.Initialize(_rhythmActions);

            CreateUI();
            SetupNavigation();
        }

        // =================================================================
        // NAVIGATION SETUP
        // =================================================================

        private void SetupNavigation()
        {
            UINavigationHelper.WireVerticalNoWrap(_newRunBtn, _seedGoBtn, _settingsBtn, _quitBtn);

            UINavigationHelper.Wire(_seedInput,
                up: _newRunBtn,
                down: _settingsBtn,
                right: _seedGoBtn);

            UINavigationHelper.AddLink(_seedGoBtn, left: _seedInput);

            _seedInput.onEndEdit.AddListener(_ =>
            {
                if (_seedInput.isFocused) return;
                _focusSetter.FocusOn(_seedGoBtn.gameObject);
            });

            UISelectableStyle.Apply(_newRunBtn);
            UISelectableStyle.Apply(_seedGoBtn);
            UISelectableStyle.Apply(_settingsBtn);
            UISelectableStyle.Apply(_quitBtn);
            UISelectableStyle.Apply(_seedInput);

            _focusSetter = gameObject.AddComponent<UIFocusSetter>();
            _focusSetter.SetDefault(_newRunBtn.gameObject);

            _cancelHandler = gameObject.AddComponent<UICancelHandler>();
            _cancelHandler.SetBaseAction(OnQuit);
        }

        // =================================================================
        // ACTIONS
        // =================================================================

        private void OnNewRun() => StartRun(null);

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
            ShowAudioTab();

            _cancelHandler.Push(OnSettingsClose);
            _focusSetter.FocusOn(_audioTabBtn.gameObject);
        }

        private void OnSettingsClose()
        {
            // Cancel any active rebind
            if (KeybindManager.IsRebinding)
                KeybindManager.CancelRebind();

            _settingsPanel.SetActive(false);

            PlayerPrefs.SetFloat("audioOffset", _offsetSlider.value);
            PlayerPrefs.SetFloat("masterVolume", _masterVolSlider.value);
            PlayerPrefs.SetFloat("musicVolume", _musicVolSlider.value);
            PlayerPrefs.SetFloat("sfxVolume", _sfxVolSlider.value);
            PlayerPrefs.Save();

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
        // TAB SWITCHING
        // =================================================================

        private void ShowAudioTab()
        {
            _audioPanel.SetActive(true);
            _controlsPanel.SetActive(false);

            // Highlight active tab
            _audioTabBtn.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.5f);
            _controlsTabBtn.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f);

            // Wire audio navigation
            UINavigationHelper.WireHorizontal(_audioTabBtn, _controlsTabBtn);
            UINavigationHelper.AddLink(_audioTabBtn, down: _offsetSlider);
            UINavigationHelper.AddLink(_controlsTabBtn, down: _offsetSlider);

            UINavigationHelper.WireVerticalNoWrap(
                _offsetSlider, _masterVolSlider, _musicVolSlider, _sfxVolSlider);
            UINavigationHelper.AddLink(_offsetSlider, up: _audioTabBtn);
            UINavigationHelper.AddLink(_sfxVolSlider, down: _settingsCloseBtn);
            UINavigationHelper.Wire(_settingsCloseBtn, up: _sfxVolSlider);

            UISelectableStyle.Apply(_audioTabBtn);
            UISelectableStyle.Apply(_controlsTabBtn);
            UISelectableStyle.ApplySlider(_offsetSlider);
            UISelectableStyle.ApplySlider(_masterVolSlider);
            UISelectableStyle.ApplySlider(_musicVolSlider);
            UISelectableStyle.ApplySlider(_sfxVolSlider);
            UISelectableStyle.Apply(_settingsCloseBtn);

            _focusSetter.FocusOn(_audioTabBtn.gameObject);
        }

        private void ShowControlsTab()
        {
            _audioPanel.SetActive(false);
            _controlsPanel.SetActive(true);

            _audioTabBtn.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f);
            _controlsTabBtn.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.5f);

            // Refresh binding display
            RefreshBindingDisplay();

            // Wire controls navigation
            UINavigationHelper.WireHorizontal(_audioTabBtn, _controlsTabBtn);
            UINavigationHelper.AddLink(_audioTabBtn, down: _rebindButtons[0]);
            UINavigationHelper.AddLink(_controlsTabBtn, down: _rebindButtons[0]);

            // Wire rebind buttons vertically (primary column)
            UINavigationHelper.WireVerticalNoWrap(_rebindButtons);
            UINavigationHelper.AddLink(_rebindButtons[0], up: _controlsTabBtn);
            UINavigationHelper.AddLink(_rebindButtons[3], down: _resetDefaultsBtn);

            // Wire primary ↔ secondary horizontally per row
            for (int i = 0; i < 4; i++)
            {
                UINavigationHelper.AddLink(_rebindButtons[i], right: _secondaryButtons[i]);
                UINavigationHelper.AddLink(_secondaryButtons[i], left: _rebindButtons[i]);

                if (i > 0)
                    UINavigationHelper.AddLink(_secondaryButtons[i], up: _secondaryButtons[i - 1]);
                if (i < 3)
                    UINavigationHelper.AddLink(_secondaryButtons[i], down: _secondaryButtons[i + 1]);
            }
            UINavigationHelper.AddLink(_secondaryButtons[0], up: _controlsTabBtn);
            UINavigationHelper.AddLink(_secondaryButtons[3], down: _resetDefaultsBtn);

            UINavigationHelper.Wire(_resetDefaultsBtn, up: _rebindButtons[3], down: _settingsCloseBtn);
            UINavigationHelper.Wire(_settingsCloseBtn, up: _resetDefaultsBtn);

            UISelectableStyle.Apply(_audioTabBtn);
            UISelectableStyle.Apply(_controlsTabBtn);
            for (int i = 0; i < 4; i++)
            {
                UISelectableStyle.Apply(_rebindButtons[i]);
                UISelectableStyle.Apply(_secondaryButtons[i]);
            }
            UISelectableStyle.Apply(_resetDefaultsBtn);
            UISelectableStyle.Apply(_settingsCloseBtn);

            _focusSetter.FocusOn(_controlsTabBtn.gameObject);
        }

        // =================================================================
        // REBINDING
        // =================================================================

        private void StartRebind(int lane, int bindingIndex, Text displayText)
        {
            if (KeybindManager.IsRebinding) return;

            string originalText = displayText.text;
            displayText.text = "Press a key...";
            displayText.color = new Color(1f, 0.85f, 0f);

            HideConflictWarning();

            KeybindManager.StartRebind(lane, bindingIndex,
                onComplete: newDisplay =>
                {
                    displayText.text = newDisplay;
                    displayText.color = Color.white;
                    RefreshBindingDisplay();
                },
                onCancel: () =>
                {
                    displayText.text = originalText;
                    displayText.color = Color.white;
                    ShowConflictWarning("Key already in use!");
                });
        }

        private void OnResetDefaults()
        {
            KeybindManager.ResetToDefaults();
            RefreshBindingDisplay();
            HideConflictWarning();
        }

        private void RefreshBindingDisplay()
        {
            for (int i = 0; i < 4; i++)
            {
                var indices = KeybindManager.GetKeyboardBindingIndices(i);

                // Primary binding (first keyboard binding)
                if (indices.Count > 0)
                {
                    string display = KeybindManager.GetBindingDisplayString(i, indices[0]);
                    _rebindButtonTexts[i].text = string.IsNullOrEmpty(display) ? "---" : display;
                }
                else
                {
                    _rebindButtonTexts[i].text = "---";
                }

                // Secondary binding (second keyboard binding, if exists)
                if (indices.Count > 1)
                {
                    string display = KeybindManager.GetBindingDisplayString(i, indices[1]);
                    _secondaryTexts[i].text = string.IsNullOrEmpty(display) ? "---" : display;
                    _secondaryButtons[i].gameObject.SetActive(true);
                }
                else
                {
                    _secondaryTexts[i].text = "---";
                    _secondaryButtons[i].gameObject.SetActive(false);
                }
            }
        }

        private void ShowConflictWarning(string msg)
        {
            if (_conflictWarning != null)
            {
                _conflictWarning.text = msg;
                _conflictWarning.gameObject.SetActive(true);
            }
        }

        private void HideConflictWarning()
        {
            if (_conflictWarning != null)
                _conflictWarning.gameObject.SetActive(false);
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

            UIEventSystemProvider.EnsureEventSystem();

            // Background
            GameObject bgGO = MakePanel(_canvasRT, "BG",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                new Color(0.06f, 0.06f, 0.1f));
            bgGO.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            bgGO.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // Accent line
            MakePanel(_canvasRT, "AccentLine",
                new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(200, 1),
                new Color(0.4f, 0.3f, 0.6f, 0.4f));

            // Title
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

            // Buttons
            float btnW = 80f;
            float btnH = 16f;
            float startY = -68f;
            float gap = 20f;

            _newRunBtn = MakeMenuButton("New Run", startY, btnW, btnH, new Color(0.2f, 0.45f, 0.2f), OnNewRun);
            CreateSeedEntry(startY - gap, btnW, btnH);
            _settingsBtn = MakeMenuButton("Settings", startY - gap * 2, btnW, btnH, new Color(0.3f, 0.3f, 0.45f), OnSettings);
            _quitBtn = MakeMenuButton("Quit", startY - gap * 3, btnW, btnH, new Color(0.4f, 0.2f, 0.2f), OnQuit);

            // Version
            Text version = MakeText(_canvasRT, "Version",
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-4, 4), new Vector2(80, 8),
                4, TextAnchor.MiddleRight, new Color(0.35f, 0.35f, 0.35f));
            version.text = _versionText;

            // Settings panel
            CreateSettingsPanel();
        }

        // =================================================================
        // SEED ENTRY
        // =================================================================

        private void CreateSeedEntry(float y, float btnW, float btnH)
        {
            GameObject container = new GameObject("SeedEntry", typeof(RectTransform));
            container.transform.SetParent(_canvasRT, false);

            RectTransform crt = container.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 1f);
            crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(0, y);
            crt.sizeDelta = new Vector2(btnW + 40, btnH);

            // Input field
            GameObject inputGO = new GameObject("SeedInput", typeof(RectTransform), typeof(Image), typeof(InputField));
            inputGO.transform.SetParent(crt, false);

            RectTransform irt = inputGO.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0, 0.5f);
            irt.anchorMax = new Vector2(0, 0.5f);
            irt.pivot = new Vector2(0, 0.5f);
            irt.anchoredPosition = new Vector2(-btnW * 0.5f - 20, 0);
            irt.sizeDelta = new Vector2(btnW - 10, btnH);

            inputGO.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f);

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

            // Card
            GameObject card = MakePanel(panelRT, "Card",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(220, 160),
                new Color(0.1f, 0.1f, 0.15f, 0.95f));
            RectTransform cardRT = card.GetComponent<RectTransform>();

            // Title
            Text settingsTitle = MakeText(cardRT, "SettingsTitle",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -8), new Vector2(200, 14),
                8, TextAnchor.MiddleCenter, Color.white);
            settingsTitle.fontStyle = FontStyle.Bold;
            settingsTitle.text = "Settings";

            // Tab buttons
            float tabY = -22f;
            float tabW = 60f;
            float tabH = 12f;

            GameObject audioTabGO = MakePanel(cardRT, "AudioTab",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-35, tabY), new Vector2(tabW, tabH),
                new Color(0.3f, 0.3f, 0.5f));
            _audioTabBtn = audioTabGO.AddComponent<Button>();
            _audioTabBtn.onClick.AddListener(ShowAudioTab);
            MakeText(audioTabGO.GetComponent<RectTransform>(), "T",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(tabW, tabH),
                5, TextAnchor.MiddleCenter, Color.white).text = "Audio";

            GameObject ctrlTabGO = MakePanel(cardRT, "ControlsTab",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(35, tabY), new Vector2(tabW, tabH),
                new Color(0.15f, 0.15f, 0.2f));
            _controlsTabBtn = ctrlTabGO.AddComponent<Button>();
            _controlsTabBtn.onClick.AddListener(ShowControlsTab);
            MakeText(ctrlTabGO.GetComponent<RectTransform>(), "T",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(tabW, tabH),
                5, TextAnchor.MiddleCenter, Color.white).text = "Controls";

            // --- AUDIO PANEL ---
            CreateAudioPanel(cardRT);

            // --- CONTROLS PANEL ---
            CreateControlsPanel(cardRT);

            // Close button (shared, at bottom of card)
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

        private void CreateAudioPanel(RectTransform cardRT)
        {
            _audioPanel = new GameObject("AudioPanel", typeof(RectTransform));
            _audioPanel.transform.SetParent(cardRT, false);
            RectTransform apRT = _audioPanel.GetComponent<RectTransform>();
            apRT.anchorMin = Vector2.zero;
            apRT.anchorMax = Vector2.one;
            apRT.offsetMin = Vector2.zero;
            apRT.offsetMax = Vector2.zero;

            float sliderY = -38f;
            float sliderGap = 24f;

            float savedOffset = PlayerPrefs.GetFloat("audioOffset", 0f);
            _offsetSlider = CreateSliderRow(apRT, "Audio Offset", sliderY,
                -100f, 100f, savedOffset, out _offsetValue);
            _offsetSlider.wholeNumbers = true;
            _offsetSlider.onValueChanged.AddListener(v =>
                _offsetValue.text = $"{v:+0;-0;0} ms");
            _offsetValue.text = $"{savedOffset:+0;-0;0} ms";

            float savedMaster = PlayerPrefs.GetFloat("masterVolume", 1f);
            _masterVolSlider = CreateSliderRow(apRT, "Master Vol", sliderY - sliderGap,
                0f, 1f, savedMaster, out _);

            float savedMusic = PlayerPrefs.GetFloat("musicVolume", 1f);
            _musicVolSlider = CreateSliderRow(apRT, "Music Vol", sliderY - sliderGap * 2,
                0f, 1f, savedMusic, out _);

            float savedSFX = PlayerPrefs.GetFloat("sfxVolume", 1f);
            _sfxVolSlider = CreateSliderRow(apRT, "SFX Vol", sliderY - sliderGap * 3,
                0f, 1f, savedSFX, out _);
        }

        private void CreateControlsPanel(RectTransform cardRT)
        {
            _controlsPanel = new GameObject("ControlsPanel", typeof(RectTransform));
            _controlsPanel.transform.SetParent(cardRT, false);
            RectTransform cpRT = _controlsPanel.GetComponent<RectTransform>();
            cpRT.anchorMin = Vector2.zero;
            cpRT.anchorMax = Vector2.one;
            cpRT.offsetMin = Vector2.zero;
            cpRT.offsetMax = Vector2.zero;

            _bindingLabels = new Text[4];
            _rebindButtons = new Button[4];
            _rebindButtonTexts = new Text[4];
            _secondaryLabels = new Text[4];
            _secondaryButtons = new Button[4];
            _secondaryTexts = new Text[4];

            float rowY = -38f;
            float rowGap = 18f;

            // Column headers
            MakeText(cpRT, "HdrLane",
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(10, rowY + 12), new Vector2(40, 10),
                4, TextAnchor.MiddleLeft, new Color(0.5f, 0.5f, 0.5f)).text = "Lane";

            MakeText(cpRT, "HdrPrimary",
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(55, rowY + 12), new Vector2(60, 10),
                4, TextAnchor.MiddleCenter, new Color(0.5f, 0.5f, 0.5f)).text = "Primary";

            MakeText(cpRT, "HdrSecondary",
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(130, rowY + 12), new Vector2(60, 10),
                4, TextAnchor.MiddleCenter, new Color(0.5f, 0.5f, 0.5f)).text = "Alt";

            for (int i = 0; i < 4; i++)
            {
                float y = rowY - i * rowGap;
                int lane = i;

                // Lane label
                _bindingLabels[i] = MakeText(cpRT, $"Lane{i}Label",
                    new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(10, y), new Vector2(40, 12),
                    5, TextAnchor.MiddleLeft, new Color(0.8f, 0.8f, 0.8f));
                _bindingLabels[i].text = KeybindManager.LaneNames[i];

                // Primary rebind button
                GameObject primaryGO = MakePanel(cpRT, $"Rebind{i}",
                    new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(55, y), new Vector2(60, 12),
                    new Color(0.2f, 0.2f, 0.25f));
                _rebindButtons[i] = primaryGO.AddComponent<Button>();
                _rebindButtonTexts[i] = MakeText(primaryGO.GetComponent<RectTransform>(), "KeyText",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(56, 12),
                    5, TextAnchor.MiddleCenter, Color.white);
                _rebindButtonTexts[i].text = "---";

                // Primary click handler
                int primaryLane = lane;
                _rebindButtons[i].onClick.AddListener(() =>
                {
                    var indices = KeybindManager.GetKeyboardBindingIndices(primaryLane);
                    if (indices.Count > 0)
                        StartRebind(primaryLane, indices[0], _rebindButtonTexts[primaryLane]);
                });

                // Secondary rebind button
                GameObject secondaryGO = MakePanel(cpRT, $"Rebind{i}Alt",
                    new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(130, y), new Vector2(60, 12),
                    new Color(0.2f, 0.2f, 0.25f));
                _secondaryButtons[i] = secondaryGO.AddComponent<Button>();
                _secondaryTexts[i] = MakeText(secondaryGO.GetComponent<RectTransform>(), "KeyText",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(56, 12),
                    5, TextAnchor.MiddleCenter, Color.white);
                _secondaryTexts[i].text = "---";

                int secLane = lane;
                _secondaryButtons[i].onClick.AddListener(() =>
                {
                    var indices = KeybindManager.GetKeyboardBindingIndices(secLane);
                    if (indices.Count > 1)
                        StartRebind(secLane, indices[1], _secondaryTexts[secLane]);
                });
            }

            // Reset defaults button
            GameObject resetGO = MakePanel(cpRT, "ResetDefaults",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, rowY - 4 * rowGap - 4), new Vector2(80, 12),
                new Color(0.35f, 0.25f, 0.15f));
            _resetDefaultsBtn = resetGO.AddComponent<Button>();
            _resetDefaultsBtn.onClick.AddListener(OnResetDefaults);
            MakeText(resetGO.GetComponent<RectTransform>(), "ResetText",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(76, 12),
                5, TextAnchor.MiddleCenter, Color.white).text = "Reset Defaults";

            // Conflict warning
            _conflictWarning = MakeText(cpRT, "ConflictWarn",
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 24), new Vector2(180, 10),
                5, TextAnchor.MiddleCenter, new Color(1f, 0.4f, 0.3f));
            _conflictWarning.text = "";
            _conflictWarning.gameObject.SetActive(false);

            _controlsPanel.SetActive(false);
        }

        // =================================================================
        // MENU BUTTON HELPER
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
        // SLIDER ROW HELPER
        // =================================================================

        private Slider CreateSliderRow(RectTransform parent, string label, float y,
            float min, float max, float value, out Text valueText)
        {
            MakeText(parent, $"{label}_Label",
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(10, y), new Vector2(60, 10),
                5, TextAnchor.MiddleLeft, new Color(0.7f, 0.7f, 0.7f)).text = label;

            GameObject sliderGO = CreateSliderGO(parent, $"{label}_Slider",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(15, y - 10), new Vector2(120, 8));

            Slider slider = sliderGO.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;

            valueText = MakeText(parent, $"{label}_Value",
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-10, y), new Vector2(40, 10),
                5, TextAnchor.MiddleRight, Color.white);

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