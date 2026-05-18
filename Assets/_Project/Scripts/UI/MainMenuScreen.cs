using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using RhythmRogue.Core;
using RhythmRogue.Battle;
using RhythmRogue.Data;
using RhythmRogue.Util;
using RhythmRogue.UI.Navigation;

namespace RhythmRogue.UI
{
    public class MainMenuScreen : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RunState _runState;
        [SerializeField] private InputActionAsset _rhythmActions;
        [Header("Version")]
        [SerializeField] private string _versionText = "Prototype v0.1";

        private Canvas _canvas;
        private RectTransform _canvasRT;
        private InputField _seedInput;
        private GameObject _settingsPanel, _audioPanel, _controlsPanel;
        private Slider _offsetSlider, _masterVolSlider, _musicVolSlider, _sfxVolSlider, _scrollSpeedSlider;
        private Text _offsetValue, _scrollSpeedValue;
        private Text[] _bindingLabels, _rebindButtonTexts, _secondaryLabels, _secondaryTexts;
        private Button[] _rebindButtons, _secondaryButtons;
        private Button _resetDefaultsBtn, _audioTabBtn, _controlsTabBtn;
        private Button _newRunBtn, _seedGoBtn, _settingsBtn, _quitBtn, _settingsCloseBtn;
        private Text _conflictWarning;
        private UIFocusSetter _focusSetter;
        private UICancelHandler _cancelHandler;

        private void Start()
        {
            if (_rhythmActions != null) KeybindManager.Initialize(_rhythmActions);
            CreateUI();
            SetupNavigation();
        }

        private void SetupNavigation()
        {
            UINavigationHelper.WireVerticalNoWrap(_newRunBtn, _seedGoBtn, _settingsBtn, _quitBtn);
            UINavigationHelper.Wire(_seedInput, up: _newRunBtn, down: _settingsBtn, right: _seedGoBtn);
            UINavigationHelper.AddLink(_seedGoBtn, left: _seedInput);
            _seedInput.onEndEdit.AddListener(_ => { if (!_seedInput.isFocused) _focusSetter.FocusOn(_seedGoBtn.gameObject); });
            UISelectableStyle.Apply(_newRunBtn); UISelectableStyle.Apply(_seedGoBtn);
            UISelectableStyle.Apply(_settingsBtn); UISelectableStyle.Apply(_quitBtn);
            UISelectableStyle.Apply(_seedInput);
            _focusSetter = gameObject.AddComponent<UIFocusSetter>();
            _focusSetter.SetDefault(_newRunBtn.gameObject);
            _cancelHandler = gameObject.AddComponent<UICancelHandler>();
            _cancelHandler.SetBaseAction(OnQuit);
        }

        private void OnNewRun() => StartRun(null);
        private void OnSeededRun()
        {
            string seed = _seedInput != null ? _seedInput.text.Trim() : "";
            StartRun(string.IsNullOrEmpty(seed) ? null : seed);
        }

        private void StartRun(string seed)
        {
            if (_runState == null) { GameLog.Error("[MainMenu] No RunState!"); return; }
            if (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsTransitioning) return;
            _runState.StartNewRun(seed);
            var ph = PlayerHealth.Instance; if (ph != null) ph.ResetForNewRun();
            GameLog.Info($"[MainMenu] Starting run. Seed: {_runState.Seed}");
            var tm = SceneTransitionManager.Instance;
            if (tm != null) tm.GoToMap();
            else UnityEngine.SceneManagement.SceneManager.LoadScene(SceneTransitionManager.MAP_SCENE);
        }

        private void OnSettings()
        {
            _settingsPanel.SetActive(true); ShowAudioTab();
            _cancelHandler.Push(OnSettingsClose); _focusSetter.FocusOn(_audioTabBtn.gameObject);
        }

        private void OnSettingsClose()
        {
            if (KeybindManager.IsRebinding) KeybindManager.CancelRebind();
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
            GameLog.Info("[MainMenu] Quit"); Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void ShowAudioTab()
        {
            _audioPanel.SetActive(true); _controlsPanel.SetActive(false);
            _audioTabBtn.GetComponent<Image>().color = UIHelpers.BgLight;
            _controlsTabBtn.GetComponent<Image>().color = UIHelpers.BgSurface;
            UINavigationHelper.WireHorizontal(_audioTabBtn, _controlsTabBtn);
            UINavigationHelper.AddLink(_audioTabBtn, down: _scrollSpeedSlider);
            UINavigationHelper.AddLink(_controlsTabBtn, down: _scrollSpeedSlider);
            UINavigationHelper.WireVerticalNoWrap(_scrollSpeedSlider, _offsetSlider, _masterVolSlider, _musicVolSlider, _sfxVolSlider);
            UINavigationHelper.AddLink(_scrollSpeedSlider, up: _audioTabBtn);
            UINavigationHelper.AddLink(_sfxVolSlider, down: _settingsCloseBtn);
            UINavigationHelper.Wire(_settingsCloseBtn, up: _sfxVolSlider);
            UISelectableStyle.Apply(_audioTabBtn); UISelectableStyle.Apply(_controlsTabBtn);
            UISelectableStyle.ApplySlider(_scrollSpeedSlider); UISelectableStyle.ApplySlider(_offsetSlider);
            UISelectableStyle.ApplySlider(_masterVolSlider); UISelectableStyle.ApplySlider(_musicVolSlider);
            UISelectableStyle.ApplySlider(_sfxVolSlider); UISelectableStyle.Apply(_settingsCloseBtn);
            _focusSetter.FocusOn(_audioTabBtn.gameObject);
        }

        private void ShowControlsTab()
        {
            _audioPanel.SetActive(false); _controlsPanel.SetActive(true);
            _audioTabBtn.GetComponent<Image>().color = UIHelpers.BgSurface;
            _controlsTabBtn.GetComponent<Image>().color = UIHelpers.BgLight;
            RefreshBindingDisplay();
            UINavigationHelper.WireHorizontal(_audioTabBtn, _controlsTabBtn);
            UINavigationHelper.AddLink(_audioTabBtn, down: _rebindButtons[0]);
            UINavigationHelper.AddLink(_controlsTabBtn, down: _rebindButtons[0]);
            UINavigationHelper.WireVerticalNoWrap(_rebindButtons);
            UINavigationHelper.AddLink(_rebindButtons[0], up: _controlsTabBtn);
            UINavigationHelper.AddLink(_rebindButtons[3], down: _resetDefaultsBtn);
            for (int i = 0; i < 4; i++)
            {
                UINavigationHelper.AddLink(_rebindButtons[i], right: _secondaryButtons[i]);
                UINavigationHelper.AddLink(_secondaryButtons[i], left: _rebindButtons[i]);
                if (i > 0) UINavigationHelper.AddLink(_secondaryButtons[i], up: _secondaryButtons[i - 1]);
                if (i < 3) UINavigationHelper.AddLink(_secondaryButtons[i], down: _secondaryButtons[i + 1]);
            }
            UINavigationHelper.AddLink(_secondaryButtons[0], up: _controlsTabBtn);
            UINavigationHelper.AddLink(_secondaryButtons[3], down: _resetDefaultsBtn);
            UINavigationHelper.Wire(_resetDefaultsBtn, up: _rebindButtons[3], down: _settingsCloseBtn);
            UINavigationHelper.Wire(_settingsCloseBtn, up: _resetDefaultsBtn);
            UISelectableStyle.Apply(_audioTabBtn); UISelectableStyle.Apply(_controlsTabBtn);
            for (int i = 0; i < 4; i++) { UISelectableStyle.Apply(_rebindButtons[i]); UISelectableStyle.Apply(_secondaryButtons[i]); }
            UISelectableStyle.Apply(_resetDefaultsBtn); UISelectableStyle.Apply(_settingsCloseBtn);
            _focusSetter.FocusOn(_controlsTabBtn.gameObject);
        }

        private void StartRebind(int lane, int bindingIndex, Text displayText)
        {
            if (KeybindManager.IsRebinding) return;
            string originalText = displayText.text;
            displayText.text = "Press a key..."; displayText.color = UIHelpers.WarmGold;
            HideConflictWarning();
            KeybindManager.StartRebind(lane, bindingIndex,
                onComplete: d => { displayText.text = d; displayText.color = UIHelpers.OffWhite; RefreshBindingDisplay(); },
                onCancel: () => { displayText.text = originalText; displayText.color = UIHelpers.OffWhite; ShowConflictWarning("Key already in use!"); });
        }

        private void OnResetDefaults() { KeybindManager.ResetToDefaults(); RefreshBindingDisplay(); HideConflictWarning(); }

        private void RefreshBindingDisplay()
        {
            for (int i = 0; i < 4; i++)
            {
                var idx = KeybindManager.GetKeyboardBindingIndices(i);
                _rebindButtonTexts[i].text = idx.Count > 0 ? KeybindManager.GetBindingDisplayString(i, idx[0]) ?? "---" : "---";
                if (idx.Count > 1) { _secondaryTexts[i].text = KeybindManager.GetBindingDisplayString(i, idx[1]) ?? "---"; _secondaryButtons[i].gameObject.SetActive(true); }
                else { _secondaryTexts[i].text = "---"; _secondaryButtons[i].gameObject.SetActive(false); }
            }
        }

        private void ShowConflictWarning(string msg) { if (_conflictWarning != null) { _conflictWarning.text = msg; _conflictWarning.gameObject.SetActive(true); } }
        private void HideConflictWarning() { if (_conflictWarning != null) _conflictWarning.gameObject.SetActive(false); }

        private void CreateUI()
        {
            GameObject canvasGO = new GameObject("MenuCanvas");
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

            MakePanel(_canvasRT, "AccentLine", new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 2), new Color(UIHelpers.BgLight.r, UIHelpers.BgLight.g, UIHelpers.BgLight.b, 0.5f));

            var title = MakeText(_canvasRT, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(1500, 120), 64, TextAnchor.MiddleCenter, UIHelpers.WarmGold);
            title.fontStyle = FontStyle.Bold; title.text = "RHYTHM ROGUE";

            var subtitle = MakeText(_canvasRT, "Subtitle", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -230), new Vector2(1000, 50), 22, TextAnchor.MiddleCenter, UIHelpers.AmberOrange);
            subtitle.text = "a rhythm roguelike";

            float btnW = 400f, btnH = 80f, startY = -340f, gap = 100f;
            _newRunBtn = MakeMenuButton("New Run", startY, btnW, btnH, UIHelpers.RustOrange, OnNewRun);
            CreateSeedEntry(startY - gap, btnW, btnH);
            _settingsBtn = MakeMenuButton("Settings", startY - gap * 2, btnW, btnH, UIHelpers.BgLight, OnSettings);
            _quitBtn = MakeMenuButton("Quit", startY - gap * 3, btnW, btnH, UIHelpers.Shadow, OnQuit);

            var version = MakeText(_canvasRT, "Version", new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 20), new Vector2(400, 40), 18, TextAnchor.MiddleRight, UIHelpers.Shadow);
            version.text = _versionText;

            CreateSettingsPanel();
        }

        private void CreateSeedEntry(float y, float btnW, float btnH)
        {
            var container = new GameObject("SeedEntry", typeof(RectTransform));
            container.transform.SetParent(_canvasRT, false);
            var crt = container.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(0, y);
            crt.sizeDelta = new Vector2(btnW + 200, btnH);

            var inputGO = new GameObject("SeedInput", typeof(RectTransform), typeof(Image), typeof(InputField));
            inputGO.transform.SetParent(crt, false);
            var irt = inputGO.GetComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = new Vector2(0, 0.5f);
            irt.pivot = new Vector2(0, 0.5f);
            irt.anchoredPosition = new Vector2(-btnW * 0.5f - 100, 0);
            irt.sizeDelta = new Vector2(btnW - 50, btnH);
            inputGO.GetComponent<Image>().color = UIHelpers.BgSurface;

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(inputGO.transform, false);
            var trt = textGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(15, 0); trt.offsetMax = new Vector2(-15, 0);
            var inputText = textGO.GetComponent<Text>();
            inputText.font = UIHelpers.GetDefaultFont(26);
            inputText.fontSize = 26;
            inputText.alignment = TextAnchor.MiddleLeft;
            inputText.color = UIHelpers.OffWhite;
            inputText.supportRichText = false;

            var placeholderGO = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            placeholderGO.transform.SetParent(inputGO.transform, false);
            var prt = placeholderGO.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(15, 0); prt.offsetMax = new Vector2(-15, 0);
            var placeholder = placeholderGO.GetComponent<Text>();
            placeholder.font = UIHelpers.GetDefaultFont(26);
            placeholder.fontSize = 26;
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.color = UIHelpers.Shadow;
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.text = "Enter seed...";

            _seedInput = inputGO.GetComponent<InputField>();
            _seedInput.textComponent = inputText;
            _seedInput.placeholder = placeholder;
            _seedInput.characterLimit = 20;

            var goBtnGO = MakePanel(crt, "GoBtn", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(btnW * 0.5f + 100, 0), new Vector2(140, btnH), UIHelpers.RustOrange);
            _seedGoBtn = goBtnGO.AddComponent<Button>();
            _seedGoBtn.onClick.AddListener(OnSeededRun);
            var goText = MakeText(goBtnGO.GetComponent<RectTransform>(), "GoText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140, btnH), 28, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
            goText.fontStyle = FontStyle.Bold; goText.text = "Go";
        }

        private void CreateSettingsPanel()
        {
            _settingsPanel = MakePanel(_canvasRT, "SettingsPanel", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(UIHelpers.BgDeep.r, UIHelpers.BgDeep.g, UIHelpers.BgDeep.b, 0.85f));
            _settingsPanel.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            _settingsPanel.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            var card = MakePanel(_settingsPanel.GetComponent<RectTransform>(), "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 800), UIHelpers.BgSurface);
            var cardRT = card.GetComponent<RectTransform>();

            var st = MakeText(cardRT, "SettingsTitle", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(1000, 70), 36, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
            st.fontStyle = FontStyle.Bold; st.text = "Settings";

            float tabY = -110f, tabW = 300f, tabH = 60f;
            var audioTabGO = MakePanel(cardRT, "AudioTab", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-175, tabY), new Vector2(tabW, tabH), UIHelpers.BgLight);
            _audioTabBtn = audioTabGO.AddComponent<Button>(); _audioTabBtn.onClick.AddListener(ShowAudioTab);
            MakeText(audioTabGO.GetComponent<RectTransform>(), "T", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(tabW, tabH), 24, TextAnchor.MiddleCenter, UIHelpers.OffWhite).text = "Audio";

            var ctrlTabGO = MakePanel(cardRT, "ControlsTab", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(175, tabY), new Vector2(tabW, tabH), UIHelpers.BgSurface);
            _controlsTabBtn = ctrlTabGO.AddComponent<Button>(); _controlsTabBtn.onClick.AddListener(ShowControlsTab);
            MakeText(ctrlTabGO.GetComponent<RectTransform>(), "T", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(tabW, tabH), 24, TextAnchor.MiddleCenter, UIHelpers.OffWhite).text = "Controls";

            CreateAudioPanel(cardRT);
            CreateControlsPanel(cardRT);

            var closeBtnGO = MakePanel(cardRT, "CloseBtn", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(250, 70), UIHelpers.Shadow);
            _settingsCloseBtn = closeBtnGO.AddComponent<Button>(); _settingsCloseBtn.onClick.AddListener(OnSettingsClose);
            MakeText(closeBtnGO.GetComponent<RectTransform>(), "CloseText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(250, 70), 26, TextAnchor.MiddleCenter, UIHelpers.OffWhite).text = "Back";
            _settingsPanel.SetActive(false);
        }

        private void CreateAudioPanel(RectTransform cardRT)
        {
            _audioPanel = new GameObject("AudioPanel", typeof(RectTransform));
            _audioPanel.transform.SetParent(cardRT, false);
            var apRT = _audioPanel.GetComponent<RectTransform>();
            apRT.anchorMin = Vector2.zero; apRT.anchorMax = Vector2.one;
            apRT.offsetMin = Vector2.zero; apRT.offsetMax = Vector2.zero;

            float sliderY = -190f, sliderGap = 110f;

            _scrollSpeedSlider = CreateSliderRow(apRT, "Scroll Speed", sliderY, 0.5f, 3.0f, ScrollSpeedSetting.Multiplier, out _scrollSpeedValue);
            _scrollSpeedValue.text = ScrollSpeedSetting.DisplayString;
            _scrollSpeedSlider.onValueChanged.AddListener(v => { float r = Mathf.Round(v * 10f) / 10f; ScrollSpeedSetting.Multiplier = r; _scrollSpeedValue.text = ScrollSpeedSetting.DisplayString; });

            float savedOffset = PlayerPrefs.GetFloat("audioOffset", 0f);
            _offsetSlider = CreateSliderRow(apRT, "Audio Offset", sliderY - sliderGap, -100f, 100f, savedOffset, out _offsetValue);
            _offsetSlider.wholeNumbers = true;
            _offsetSlider.onValueChanged.AddListener(v => _offsetValue.text = $"{v:+0;-0;0} ms");
            _offsetValue.text = $"{savedOffset:+0;-0;0} ms";

            _masterVolSlider = CreateSliderRow(apRT, "Master Vol", sliderY - sliderGap * 2, 0f, 1f, PlayerPrefs.GetFloat("masterVolume", 1f), out _);
            _musicVolSlider = CreateSliderRow(apRT, "Music Vol", sliderY - sliderGap * 3, 0f, 1f, PlayerPrefs.GetFloat("musicVolume", 1f), out _);
            _sfxVolSlider = CreateSliderRow(apRT, "SFX Vol", sliderY - sliderGap * 4, 0f, 1f, PlayerPrefs.GetFloat("sfxVolume", 1f), out _);
        }

        private void CreateControlsPanel(RectTransform cardRT)
        {
            _controlsPanel = new GameObject("ControlsPanel", typeof(RectTransform));
            _controlsPanel.transform.SetParent(cardRT, false);
            var cpRT = _controlsPanel.GetComponent<RectTransform>();
            cpRT.anchorMin = Vector2.zero; cpRT.anchorMax = Vector2.one;
            cpRT.offsetMin = Vector2.zero; cpRT.offsetMax = Vector2.zero;

            _bindingLabels = new Text[4]; _rebindButtons = new Button[4]; _rebindButtonTexts = new Text[4];
            _secondaryLabels = new Text[4]; _secondaryButtons = new Button[4]; _secondaryTexts = new Text[4];

            float rowY = -190f, rowGap = 90f;
            MakeText(cpRT, "HdrLane", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, rowY + 60), new Vector2(200, 50), 18, TextAnchor.MiddleLeft, UIHelpers.Shadow).text = "Lane";
            MakeText(cpRT, "HdrPrimary", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(275, rowY + 60), new Vector2(300, 50), 18, TextAnchor.MiddleCenter, UIHelpers.Shadow).text = "Primary";
            MakeText(cpRT, "HdrSecondary", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(650, rowY + 60), new Vector2(300, 50), 18, TextAnchor.MiddleCenter, UIHelpers.Shadow).text = "Alt";

            for (int i = 0; i < 4; i++)
            {
                float ry = rowY - i * rowGap; int lane = i;
                _bindingLabels[i] = MakeText(cpRT, $"Lane{i}Label", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, ry), new Vector2(200, 60), 22, TextAnchor.MiddleLeft, UIHelpers.OffWhite);
                _bindingLabels[i].text = KeybindManager.LaneNames[i];

                var pGO = MakePanel(cpRT, $"Rebind{i}", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(275, ry), new Vector2(300, 60), UIHelpers.BgLight);
                _rebindButtons[i] = pGO.AddComponent<Button>();
                _rebindButtonTexts[i] = MakeText(pGO.GetComponent<RectTransform>(), "KeyText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280, 60), 22, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
                _rebindButtonTexts[i].text = "---";
                int pL = lane; _rebindButtons[i].onClick.AddListener(() => { var idx = KeybindManager.GetKeyboardBindingIndices(pL); if (idx.Count > 0) StartRebind(pL, idx[0], _rebindButtonTexts[pL]); });

                var sGO = MakePanel(cpRT, $"Rebind{i}Alt", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(650, ry), new Vector2(300, 60), UIHelpers.BgLight);
                _secondaryButtons[i] = sGO.AddComponent<Button>();
                _secondaryTexts[i] = MakeText(sGO.GetComponent<RectTransform>(), "KeyText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280, 60), 22, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
                _secondaryTexts[i].text = "---";
                int sL = lane; _secondaryButtons[i].onClick.AddListener(() => { var idx = KeybindManager.GetKeyboardBindingIndices(sL); if (idx.Count > 1) StartRebind(sL, idx[1], _secondaryTexts[sL]); });
            }

            var resetGO = MakePanel(cpRT, "ResetDefaults", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, rowY - 4 * rowGap - 20), new Vector2(400, 60), UIHelpers.RustOrange);
            _resetDefaultsBtn = resetGO.AddComponent<Button>(); _resetDefaultsBtn.onClick.AddListener(OnResetDefaults);
            MakeText(resetGO.GetComponent<RectTransform>(), "ResetText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380, 60), 22, TextAnchor.MiddleCenter, UIHelpers.OffWhite).text = "Reset Defaults";

            _conflictWarning = MakeText(cpRT, "ConflictWarn", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(900, 50), 22, TextAnchor.MiddleCenter, UIHelpers.RustOrange);
            _conflictWarning.text = ""; _conflictWarning.gameObject.SetActive(false);
            _controlsPanel.SetActive(false);
        }

        private Button MakeMenuButton(string label, float y, float w, float h, Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            var btnGO = MakePanel(_canvasRT, $"Btn_{label}", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(w, h), bgColor);
            var btn = btnGO.AddComponent<Button>(); btn.onClick.AddListener(onClick);
            var txt = MakeText(btnGO.GetComponent<RectTransform>(), "Text", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h), 30, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
            txt.fontStyle = FontStyle.Bold; txt.text = label;
            return btn;
        }

        private Slider CreateSliderRow(RectTransform parent, string label, float y, float min, float max, float value, out Text valueText)
        {
            MakeText(parent, $"{label}_Label", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, y), new Vector2(300, 50), 22, TextAnchor.MiddleLeft, UIHelpers.AmberOrange).text = label;
            var sliderGO = CreateSliderGO(parent, $"{label}_Slider", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(75, y - 50), new Vector2(600, 40));
            var slider = sliderGO.GetComponent<Slider>(); slider.minValue = min; slider.maxValue = max; slider.value = value;
            valueText = MakeText(parent, $"{label}_Value", new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-50, y), new Vector2(200, 50), 22, TextAnchor.MiddleRight, UIHelpers.OffWhite);
            if (max <= 1f) { Text vt = valueText; vt.text = $"{Mathf.RoundToInt(value * 100)}%"; slider.onValueChanged.AddListener(v => vt.text = $"{Mathf.RoundToInt(v * 100)}%"); }
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

        private static GameObject CreateSliderGO(RectTransform parent, string name, Vector2 ancMin, Vector2 ancMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            var rootRT = root.GetComponent<RectTransform>();
            rootRT.anchorMin = ancMin; rootRT.anchorMax = ancMax; rootRT.pivot = pivot;
            rootRT.anchoredPosition = pos; rootRT.sizeDelta = size;

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image)); bg.transform.SetParent(root.transform, false);
            var bgRT = bg.GetComponent<RectTransform>(); bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one; bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = UIHelpers.BgSurface;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform)); fillArea.transform.SetParent(root.transform, false);
            var faRT = fillArea.GetComponent<RectTransform>(); faRT.anchorMin = Vector2.zero; faRT.anchorMax = Vector2.one; faRT.offsetMin = faRT.offsetMax = Vector2.zero;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)); fill.transform.SetParent(fillArea.transform, false);
            var fillRT = fill.GetComponent<RectTransform>(); fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one; fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = UIHelpers.AmberOrange;

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)); handleArea.transform.SetParent(root.transform, false);
            var haRT = handleArea.GetComponent<RectTransform>(); haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one; haRT.offsetMin = haRT.offsetMax = Vector2.zero;

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)); handle.transform.SetParent(handleArea.transform, false);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(30, size.y + 10);
            handle.GetComponent<Image>().color = UIHelpers.WarmGold;

            var slider = root.GetComponent<Slider>();
            slider.fillRect = fillRT; slider.handleRect = handle.GetComponent<RectTransform>(); slider.targetGraphic = handle.GetComponent<Image>();
            return root;
        }
    }
}