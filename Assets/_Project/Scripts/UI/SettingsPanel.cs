using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.Core;
using RhythmRogue.Core.Audio;
using RhythmRogue.Core.Display;
using RhythmRogue.Data;
using RhythmRogue.UI.Navigation;
using AudioSettings = RhythmRogue.Core.Audio.AudioSettings;

namespace RhythmRogue.UI
{
    /// <summary>
    /// The full settings UI (Audio / Controls / Display tabs), as a single reusable component
    /// so the main menu and the in-run pause menu share ONE implementation instead of each
    /// building their own. Previously this logic lived inline in MainMenuScreen; the pause
    /// menu only had audio sliders. Extracting it here gives both screens identical, fully
    /// featured settings with no duplication.
    ///
    /// Usage:
    ///   var settings = host.AddComponent&lt;SettingsPanel&gt;();
    ///   settings.Build(parentRect, focusSetter, cancelHandler);
    ///   settings.OnCloseRequested += () => { ...hide my screen's settings... };
    ///   settings.Open();   // show + focus first tab, pushes a cancel handler
    ///   settings.Close();  // hide + save, pops the cancel handler
    ///
    /// All values are read/written through the existing singletons (KeybindManager,
    /// DisplaySettings, AudioSettings, ScrollSpeedSetting), so settings are global and
    /// consistent no matter which screen opened the panel.
    ///
    /// SOLID:
    ///   S - Owns only the settings UI + its wiring to the settings singletons. It does not
    ///       own the screen it sits in; the host decides where/when to Open/Close it.
    ///   O - New tabs/rows are added here without touching either host screen.
    ///   D - Depends on the settings-data abstractions, not on MainMenu or PauseMenu.
    /// </summary>
    [DisallowMultipleComponent]
    public class SettingsPanel : MonoBehaviour
    {
        /// <summary>Raised when the panel's Back button (or cancel) asks to close. Host hides itself.</summary>
        public event Action OnCloseRequested;

        private RectTransform _root;          // the dim overlay panel (toggled active)
        private UIFocusSetter _focusSetter;   // host-provided focus driver
        private UICancelHandler _cancelHandler;// host-provided cancel router

        // Tabs + sub-panels
        private GameObject _audioPanel, _controlsPanel, _displayPanel, _gameplayPanel;
        private Button _audioTabBtn, _controlsTabBtn, _displayTabBtn, _gameplayTabBtn;
        private Button _settingsCloseBtn;

        // Audio controls
        private Slider _offsetSlider, _masterVolSlider, _musicVolSlider, _sfxVolSlider;
        private Text _offsetValue;

        // Gameplay controls
        private Button _scrollDirToggle;
        private Text _scrollDirValue;
        private InputField _scrollSpeedInput;

        // Controls (rebind) widgets
        private Text[] _rebindButtonTexts, _secondaryTexts;
        private Button[] _rebindButtons, _secondaryButtons;
        private Button _resetDefaultsBtn;
        private Text _conflictWarning;

        // Display controls
        private Dropdown _resolutionDropdown;
        private Button _fullscreenPrevBtn, _fullscreenNextBtn, _vsyncToggleBtn, _crtToggleBtn;
        private Text _fullscreenValueText, _vsyncValueText, _crtValueText;
        private List<Resolution> _availableResolutions;
        private int _currentResolutionIndex;

        private bool _built;
        private bool _cancelPushed; // true while our close handler is on the cancel stack

        /// <summary>
        /// Build the settings UI under the given parent. Pass the host's focus setter and
        /// cancel handler so navigation + Escape integrate with the rest of the screen.
        /// </summary>
        public void Build(RectTransform parent, UIFocusSetter focusSetter, UICancelHandler cancelHandler)
        {
            if (_built) return;
            _focusSetter = focusSetter;
            _cancelHandler = cancelHandler;
            CreatePanel(parent);
            _built = true;
        }

        /// <summary>Show the panel, default to the Audio tab, and route cancel to close.</summary>
        public void Open()
        {
            if (!_built) return;
            _root.gameObject.SetActive(true);
            ShowAudioTab();
            if (_cancelHandler != null && !_cancelPushed)
            {
                _cancelHandler.Push(RequestClose);
                _cancelPushed = true;
            }
        }

        /// <summary>Hide the panel, persist pending values, and notify the host.</summary>
        public void Close()
        {
            if (!_built) return;
            if (KeybindManager.IsRebinding) KeybindManager.CancelRebind();
            // Audio offset now persists live through AudioSettings.CalibrationOffsetMs (set on the
            // slider's value-changed), so there is nothing to flush here.
            _root.gameObject.SetActive(false);
        }

        public bool IsOpen => _built && _root != null && _root.gameObject.activeSelf;

        // Internal: cancel-stack target. Pops itself by asking the host to close.
        private void RequestClose()
        {
            // Invoked either by Escape (UICancelHandler already popped us) or by the Back
            // button (still on the stack). Mark as no longer pushed; Close() pops if needed.
            bool wasPushedViaStack = _cancelPushed;
            _cancelPushed = false;
            Close();
            // If Close was reached by the Back button, our handler is still on the stack;
            // but we can't know here which path fired. To stay consistent, the Back button
            // path calls CloseFromButton instead (which pops). RequestClose is the stack path.
            OnCloseRequested?.Invoke();
            _ = wasPushedViaStack;
        }

        // Called by the Back button: pop our cancel handler (since Escape didn't), then close.
        private void CloseFromButton()
        {
            if (_cancelPushed && _cancelHandler != null) { _cancelHandler.Pop(); _cancelPushed = false; }
            Close();
            OnCloseRequested?.Invoke();
        }

        // ============================================================
        // Build
        // ============================================================

        private void CreatePanel(RectTransform parent)
        {
            _root = MakePanel(parent, "SettingsPanel", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
                new Color(UIHelpers.BgDeep.r, UIHelpers.BgDeep.g, UIHelpers.BgDeep.b, 0.85f)).GetComponent<RectTransform>();
            _root.offsetMin = Vector2.zero; _root.offsetMax = Vector2.zero;

            var card = MakePanel(_root, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 720), UIHelpers.BgSurface);
            var cardRT = card.GetComponent<RectTransform>();

            var st = MakeText(cardRT, "SettingsTitle", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -25), new Vector2(1000, 50), 32, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
            st.fontStyle = FontStyle.Bold; st.text = "Settings";

            // Four tabs, centered: Audio | Gameplay | Controls | Display.
            float tabY = -80f, tabW = 230f, tabH = 50f, tabSpacing = 250f;
            var audioTabGO = MakePanel(cardRT, "AudioTab", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-1.5f * tabSpacing, tabY), new Vector2(tabW, tabH), UIHelpers.BgLight);
            _audioTabBtn = audioTabGO.AddComponent<Button>(); _audioTabBtn.onClick.AddListener(ShowAudioTab);
            MakeText(audioTabGO.GetComponent<RectTransform>(), "T", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(tabW, tabH), 24, TextAnchor.MiddleCenter, UIHelpers.OffWhite).text = "Audio";

            var gameTabGO = MakePanel(cardRT, "GameplayTab", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-0.5f * tabSpacing, tabY), new Vector2(tabW, tabH), UIHelpers.BgSurface);
            _gameplayTabBtn = gameTabGO.AddComponent<Button>(); _gameplayTabBtn.onClick.AddListener(ShowGameplayTab);
            MakeText(gameTabGO.GetComponent<RectTransform>(), "T", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(tabW, tabH), 24, TextAnchor.MiddleCenter, UIHelpers.OffWhite).text = "Gameplay";

            var ctrlTabGO = MakePanel(cardRT, "ControlsTab", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f * tabSpacing, tabY), new Vector2(tabW, tabH), UIHelpers.BgSurface);
            _controlsTabBtn = ctrlTabGO.AddComponent<Button>(); _controlsTabBtn.onClick.AddListener(ShowControlsTab);
            MakeText(ctrlTabGO.GetComponent<RectTransform>(), "T", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(tabW, tabH), 24, TextAnchor.MiddleCenter, UIHelpers.OffWhite).text = "Controls";

            var dispTabGO = MakePanel(cardRT, "DisplayTab", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(1.5f * tabSpacing, tabY), new Vector2(tabW, tabH), UIHelpers.BgSurface);
            _displayTabBtn = dispTabGO.AddComponent<Button>(); _displayTabBtn.onClick.AddListener(ShowDisplayTab);
            MakeText(dispTabGO.GetComponent<RectTransform>(), "T", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(tabW, tabH), 24, TextAnchor.MiddleCenter, UIHelpers.OffWhite).text = "Display";

            CreateAudioPanel(cardRT);
            CreateGameplayPanel(cardRT);
            CreateControlsPanel(cardRT);
            CreateDisplayPanel(cardRT);

            var closeBtnGO = MakePanel(cardRT, "CloseBtn", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(240, 55), UIHelpers.Shadow);
            _settingsCloseBtn = closeBtnGO.AddComponent<Button>(); _settingsCloseBtn.onClick.AddListener(CloseFromButton);
            MakeText(closeBtnGO.GetComponent<RectTransform>(), "CloseText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240, 55), 24, TextAnchor.MiddleCenter, UIHelpers.OffWhite).text = "Back";

            _root.gameObject.SetActive(false);
        }

        // ============================================================
        // Tabs
        // ============================================================

        // Set the active tab's color to BgLight and the rest to BgSurface.
        private void HighlightTab(Button active)
        {
            _audioTabBtn.GetComponent<Image>().color = active == _audioTabBtn ? UIHelpers.BgLight : UIHelpers.BgSurface;
            _gameplayTabBtn.GetComponent<Image>().color = active == _gameplayTabBtn ? UIHelpers.BgLight : UIHelpers.BgSurface;
            _controlsTabBtn.GetComponent<Image>().color = active == _controlsTabBtn ? UIHelpers.BgLight : UIHelpers.BgSurface;
            _displayTabBtn.GetComponent<Image>().color = active == _displayTabBtn ? UIHelpers.BgLight : UIHelpers.BgSurface;
        }

        // Left/right navigation across the four tab buttons + their selectable styling.
        private void WireTabRow()
        {
            UINavigationHelper.WireHorizontal(_audioTabBtn, _gameplayTabBtn, _controlsTabBtn, _displayTabBtn);
            UISelectableStyle.Apply(_audioTabBtn); UISelectableStyle.Apply(_gameplayTabBtn);
            UISelectableStyle.Apply(_controlsTabBtn); UISelectableStyle.Apply(_displayTabBtn);
        }

        private void ShowAudioTab()
        {
            _audioPanel.SetActive(true); _gameplayPanel.SetActive(false); _controlsPanel.SetActive(false); _displayPanel.SetActive(false);
            HighlightTab(_audioTabBtn);
            WireTabRow();
            UINavigationHelper.AddLink(_audioTabBtn, down: _offsetSlider);
            UINavigationHelper.AddLink(_gameplayTabBtn, down: _offsetSlider);
            UINavigationHelper.AddLink(_controlsTabBtn, down: _offsetSlider);
            UINavigationHelper.AddLink(_displayTabBtn, down: _offsetSlider);
            UINavigationHelper.WireVerticalNoWrap(_offsetSlider, _masterVolSlider, _musicVolSlider, _sfxVolSlider);
            UINavigationHelper.AddLink(_offsetSlider, up: _audioTabBtn);
            UINavigationHelper.AddLink(_sfxVolSlider, down: _settingsCloseBtn);
            UINavigationHelper.Wire(_settingsCloseBtn, up: _sfxVolSlider);
            UISelectableStyle.ApplySlider(_offsetSlider); UISelectableStyle.ApplySlider(_masterVolSlider);
            UISelectableStyle.ApplySlider(_musicVolSlider); UISelectableStyle.ApplySlider(_sfxVolSlider);
            UISelectableStyle.Apply(_settingsCloseBtn);
            if (_focusSetter != null) _focusSetter.FocusOn(_audioTabBtn.gameObject);
        }

        private void ShowControlsTab()
        {
            _audioPanel.SetActive(false); _gameplayPanel.SetActive(false); _controlsPanel.SetActive(true); _displayPanel.SetActive(false);
            HighlightTab(_controlsTabBtn);
            RefreshBindingDisplay();
            WireTabRow();
            UINavigationHelper.AddLink(_audioTabBtn, down: _rebindButtons[0]);
            UINavigationHelper.AddLink(_gameplayTabBtn, down: _rebindButtons[0]);
            UINavigationHelper.AddLink(_controlsTabBtn, down: _rebindButtons[0]);
            UINavigationHelper.AddLink(_displayTabBtn, down: _rebindButtons[0]);
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
            UISelectableStyle.Apply(_audioTabBtn); UISelectableStyle.Apply(_controlsTabBtn); UISelectableStyle.Apply(_displayTabBtn);
            for (int i = 0; i < 4; i++) { UISelectableStyle.Apply(_rebindButtons[i]); UISelectableStyle.Apply(_secondaryButtons[i]); }
            UISelectableStyle.Apply(_resetDefaultsBtn); UISelectableStyle.Apply(_settingsCloseBtn);
            if (_focusSetter != null) _focusSetter.FocusOn(_controlsTabBtn.gameObject);
        }

        private void ShowDisplayTab()
        {
            _audioPanel.SetActive(false); _gameplayPanel.SetActive(false); _controlsPanel.SetActive(false); _displayPanel.SetActive(true);
            HighlightTab(_displayTabBtn);
            RefreshDisplayValues();
            WireTabRow();
            UINavigationHelper.AddLink(_audioTabBtn, down: _resolutionDropdown);
            UINavigationHelper.AddLink(_gameplayTabBtn, down: _resolutionDropdown);
            UINavigationHelper.AddLink(_controlsTabBtn, down: _resolutionDropdown);
            UINavigationHelper.AddLink(_displayTabBtn, down: _resolutionDropdown);
            UINavigationHelper.AddLink(_resolutionDropdown, up: _displayTabBtn);
            UINavigationHelper.AddLink(_fullscreenPrevBtn, right: _fullscreenNextBtn);
            UINavigationHelper.AddLink(_fullscreenNextBtn, left: _fullscreenPrevBtn);
            UINavigationHelper.WireVerticalNoWrap(_resolutionDropdown, _fullscreenPrevBtn, _vsyncToggleBtn, _crtToggleBtn);
            UINavigationHelper.AddLink(_fullscreenNextBtn, up: _resolutionDropdown, down: _vsyncToggleBtn);
            UINavigationHelper.AddLink(_crtToggleBtn, down: _settingsCloseBtn);
            UINavigationHelper.Wire(_settingsCloseBtn, up: _crtToggleBtn);
            UISelectableStyle.Apply(_audioTabBtn); UISelectableStyle.Apply(_controlsTabBtn); UISelectableStyle.Apply(_displayTabBtn);
            UISelectableStyle.Apply(_resolutionDropdown);
            UISelectableStyle.Apply(_fullscreenPrevBtn); UISelectableStyle.Apply(_fullscreenNextBtn);
            UISelectableStyle.Apply(_vsyncToggleBtn); UISelectableStyle.Apply(_crtToggleBtn);
            UISelectableStyle.Apply(_settingsCloseBtn);
            if (_focusSetter != null) _focusSetter.FocusOn(_displayTabBtn.gameObject);
        }

        private void ShowGameplayTab()
        {
            _audioPanel.SetActive(false); _gameplayPanel.SetActive(true); _controlsPanel.SetActive(false); _displayPanel.SetActive(false);
            HighlightTab(_gameplayTabBtn);
            RefreshGameplayValues();
            WireTabRow();
            UINavigationHelper.AddLink(_audioTabBtn, down: _scrollDirToggle);
            UINavigationHelper.AddLink(_gameplayTabBtn, down: _scrollDirToggle);
            UINavigationHelper.AddLink(_controlsTabBtn, down: _scrollDirToggle);
            UINavigationHelper.AddLink(_displayTabBtn, down: _scrollDirToggle);
            UINavigationHelper.WireVerticalNoWrap(_scrollDirToggle, _scrollSpeedInput);
            UINavigationHelper.AddLink(_scrollDirToggle, up: _gameplayTabBtn);
            UINavigationHelper.AddLink(_scrollSpeedInput, down: _settingsCloseBtn);
            UINavigationHelper.Wire(_settingsCloseBtn, up: _scrollSpeedInput);
            UISelectableStyle.Apply(_scrollDirToggle); UISelectableStyle.Apply(_scrollSpeedInput); UISelectableStyle.Apply(_settingsCloseBtn);
            if (_focusSetter != null) _focusSetter.FocusOn(_gameplayTabBtn.gameObject);
        }

        // ============================================================
        // Audio panel
        // ============================================================

        private void CreateAudioPanel(RectTransform cardRT)
        {
            _audioPanel = new GameObject("AudioPanel", typeof(RectTransform));
            _audioPanel.transform.SetParent(cardRT, false);
            var apRT = _audioPanel.GetComponent<RectTransform>();
            apRT.anchorMin = Vector2.zero; apRT.anchorMax = Vector2.one;
            apRT.offsetMin = Vector2.zero; apRT.offsetMax = Vector2.zero;

            // Scroll speed moved to the Gameplay tab (as a typed field). Audio rows now start
            // with the calibration offset.
            float sliderY = -220f, sliderGap = 70f;

            float savedOffset = AudioSettings.CalibrationOffsetMs;
            _offsetSlider = CreateSliderRow(apRT, "Audio Offset", sliderY, -200f, 200f, savedOffset, out _offsetValue);
            _offsetSlider.wholeNumbers = true;
            _offsetSlider.onValueChanged.AddListener(v => { AudioSettings.CalibrationOffsetMs = v; _offsetValue.text = $"{v:+0;-0;0} ms"; });
            _offsetValue.text = $"{savedOffset:+0;-0;0} ms";

            _masterVolSlider = CreateSliderRow(apRT, "Master Vol", sliderY - sliderGap, 0f, 1f, AudioSettings.MasterVolume, out _);
            _masterVolSlider.onValueChanged.AddListener(v => AudioSettings.MasterVolume = v);

            _musicVolSlider = CreateSliderRow(apRT, "Music Vol", sliderY - sliderGap * 2, 0f, 1f, AudioSettings.MusicVolume, out _);
            _musicVolSlider.onValueChanged.AddListener(v => AudioSettings.MusicVolume = v);

            _sfxVolSlider = CreateSliderRow(apRT, "SFX Vol", sliderY - sliderGap * 3, 0f, 1f, AudioSettings.SfxVolume, out _);
            _sfxVolSlider.onValueChanged.AddListener(v => AudioSettings.SfxVolume = v);
        }

        // ============================================================
        // Gameplay panel
        // ============================================================

        private void CreateGameplayPanel(RectTransform cardRT)
        {
            _gameplayPanel = new GameObject("GameplayPanel", typeof(RectTransform));
            _gameplayPanel.transform.SetParent(cardRT, false);
            var gpRT = _gameplayPanel.GetComponent<RectTransform>();
            gpRT.anchorMin = Vector2.zero; gpRT.anchorMax = Vector2.one;
            gpRT.offsetMin = Vector2.zero; gpRT.offsetMax = Vector2.zero;

            float rowY = -235f, rowGap = 80f;

            // Scroll direction: one toggle that flips Down/Up. The highways re-read this every
            // frame (even while paused), so an in-progress battle flips the instant it changes.
            _scrollDirToggle = CreateToggleRow(gpRT, "Scroll Direction", rowY, out _scrollDirValue,
                onClick: () => { ScrollDirectionSetting.Toggle(); RefreshGameplayValues(); });

            // Scroll speed: a typed float field instead of a slider (a slider is fiddly to land on
            // an exact value). This is a constant velocity in world units per second, so notes scroll
            // the same speed at any BPM. Out-of-range or junk input snaps back on commit.
            _scrollSpeedInput = CreateFloatInputRow(gpRT, "Scroll Speed", rowY - rowGap, $"{ScrollSpeedSetting.Min:0} to {ScrollSpeedSetting.Max:0} u/s", OnScrollSpeedCommitted);

            _gameplayPanel.SetActive(false);
        }

        private void RefreshGameplayValues()
        {
            if (_scrollDirValue != null) _scrollDirValue.text = ScrollDirectionSetting.DisplayString;
            if (_scrollSpeedInput != null) _scrollSpeedInput.text = ScrollSpeedSetting.UnitsPerSecond.ToString("0.0");
        }

        // Commit handler for the scroll-speed field. Normalizes a comma decimal to a dot so it
        // parses on any locale, writes it (the setter clamps to the 2-16 u/s range), then reflects
        // the stored value back into the field so bad or out-of-range input visibly corrects itself.
        private void OnScrollSpeedCommitted(string raw)
        {
            string norm = (raw ?? string.Empty).Replace(',', '.').Trim();
            if (float.TryParse(norm, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v))
                ScrollSpeedSetting.UnitsPerSecond = v;
            RefreshGameplayValues();
        }

        // ============================================================
        // Controls panel
        // ============================================================

        private void CreateControlsPanel(RectTransform cardRT)
        {
            _controlsPanel = new GameObject("ControlsPanel", typeof(RectTransform));
            _controlsPanel.transform.SetParent(cardRT, false);
            var cpRT = _controlsPanel.GetComponent<RectTransform>();
            cpRT.anchorMin = Vector2.zero; cpRT.anchorMax = Vector2.one;
            cpRT.offsetMin = Vector2.zero; cpRT.offsetMax = Vector2.zero;

            _rebindButtons = new Button[4]; _rebindButtonTexts = new Text[4];
            _secondaryButtons = new Button[4]; _secondaryTexts = new Text[4];

            float rowY = -190f, rowGap = 90f;
            MakeText(cpRT, "HdrLane", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, rowY + 60), new Vector2(200, 50), 18, TextAnchor.MiddleLeft, UIHelpers.Shadow).text = "Lane";
            MakeText(cpRT, "HdrPrimary", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(275, rowY + 60), new Vector2(300, 50), 18, TextAnchor.MiddleCenter, UIHelpers.Shadow).text = "Primary";
            MakeText(cpRT, "HdrSecondary", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(650, rowY + 60), new Vector2(300, 50), 18, TextAnchor.MiddleCenter, UIHelpers.Shadow).text = "Alt";

            for (int i = 0; i < 4; i++)
            {
                float ry = rowY - i * rowGap; int lane = i;
                MakeText(cpRT, $"Lane{i}Label", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, ry), new Vector2(200, 60), 22, TextAnchor.MiddleLeft, UIHelpers.OffWhite).text = KeybindManager.LaneNames[i];

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

            var resetGO = MakePanel(cpRT, "ResetDefaults", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, rowY - 4 * rowGap - 5), new Vector2(400, 50), UIHelpers.RustOrange);
            _resetDefaultsBtn = resetGO.AddComponent<Button>(); _resetDefaultsBtn.onClick.AddListener(OnResetDefaults);
            MakeText(resetGO.GetComponent<RectTransform>(), "ResetText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380, 50), 20, TextAnchor.MiddleCenter, UIHelpers.OffWhite).text = "Reset Defaults";

            _conflictWarning = MakeText(cpRT, "ConflictWarn", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(900, 50), 22, TextAnchor.MiddleCenter, UIHelpers.RustOrange);
            _conflictWarning.text = ""; _conflictWarning.gameObject.SetActive(false);
            _controlsPanel.SetActive(false);
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

        // ============================================================
        // Display panel
        // ============================================================

        private void CreateDisplayPanel(RectTransform cardRT)
        {
            _displayPanel = new GameObject("DisplayPanel", typeof(RectTransform));
            _displayPanel.transform.SetParent(cardRT, false);
            var dpRT = _displayPanel.GetComponent<RectTransform>();
            dpRT.anchorMin = Vector2.zero; dpRT.anchorMax = Vector2.one;
            dpRT.offsetMin = Vector2.zero; dpRT.offsetMax = Vector2.zero;

            BuildResolutionList();

            float rowY = -235f, rowGap = 80f;

            _resolutionDropdown = CreateDropdownRow(dpRT, "Resolution", rowY, BuildResolutionOptions(), _currentResolutionIndex,
                onValueChanged: i => { _currentResolutionIndex = i; var r = _availableResolutions[i]; DisplaySettings.SetResolution(r.width, r.height); });

            CreateValueStepperRow(dpRT, "Window Mode", rowY - rowGap, out _fullscreenPrevBtn, out _fullscreenNextBtn, out _fullscreenValueText,
                onPrev: () => CycleFullscreenMode(-1), onNext: () => CycleFullscreenMode(+1));

            _vsyncToggleBtn = CreateToggleRow(dpRT, "V-Sync", rowY - rowGap * 2, out _vsyncValueText,
                onClick: () => { DisplaySettings.VSync = !DisplaySettings.VSync; RefreshDisplayValues(); });

            _crtToggleBtn = CreateToggleRow(dpRT, "CRT Effect", rowY - rowGap * 3, out _crtValueText,
                onClick: () => { DisplaySettings.CRTEffect = !DisplaySettings.CRTEffect; var crt = CRTOverlay.Instance; if (crt != null) crt.ApplyVisibility(); RefreshDisplayValues(); });

            _displayPanel.SetActive(false);
        }

        private List<string> BuildResolutionOptions()
        {
            var options = new List<string>();
            if (_availableResolutions != null)
                foreach (var r in _availableResolutions) options.Add($"{r.width} x {r.height}");
            return options;
        }

        private void BuildResolutionList()
        {
            _availableResolutions = new List<Resolution>();
            var seen = new HashSet<(int, int)>();
            foreach (var res in Screen.resolutions)
            {
                var key = (res.width, res.height);
                if (seen.Contains(key)) continue;
                if (res.width < 1280 || res.height < 720) continue;
                seen.Add(key);
                _availableResolutions.Add(res);
            }
            _currentResolutionIndex = 0;
            for (int i = 0; i < _availableResolutions.Count; i++)
            {
                if (_availableResolutions[i].width == DisplaySettings.ResolutionWidth &&
                    _availableResolutions[i].height == DisplaySettings.ResolutionHeight)
                { _currentResolutionIndex = i; break; }
            }
        }

        private void CycleFullscreenMode(int direction)
        {
            int next = (DisplaySettings.FullscreenMode + direction + 3) % 3;
            DisplaySettings.FullscreenMode = next;
            RefreshDisplayValues();
        }

        private void RefreshDisplayValues()
        {
            if (_fullscreenValueText != null)
                _fullscreenValueText.text = DisplaySettings.FullscreenMode switch { 0 => "Windowed", 1 => "Borderless", 2 => "Fullscreen", _ => "---" };
            if (_vsyncValueText != null) _vsyncValueText.text = DisplaySettings.VSync ? "On" : "Off";
            if (_crtValueText != null) _crtValueText.text = DisplaySettings.CRTEffect ? "On" : "Off";
        }

        // ============================================================
        // Row builders (ported verbatim from MainMenuScreen)
        // ============================================================

        private void CreateValueStepperRow(RectTransform parent, string label, float y,
            out Button prevBtn, out Button nextBtn, out Text valueText,
            UnityEngine.Events.UnityAction onPrev, UnityEngine.Events.UnityAction onNext)
        {
            MakeText(parent, $"{label}_Label", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(75, y), new Vector2(300, 50), 22, TextAnchor.MiddleLeft, UIHelpers.AmberOrange).text = label;
            float btnY = y - 5f;
            var prevGO = MakePanel(parent, $"{label}_Prev", new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-565, btnY), new Vector2(60, 50), UIHelpers.BgLight);
            prevBtn = prevGO.AddComponent<Button>(); prevBtn.onClick.AddListener(onPrev);
            MakeText(prevGO.GetComponent<RectTransform>(), "Arr", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 50), 22, TextAnchor.MiddleCenter, UIHelpers.OffWhite).text = "<";
            valueText = MakeText(parent, $"{label}_Value", new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-285, y), new Vector2(440, 50), 22, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
            var nextGO = MakePanel(parent, $"{label}_Next", new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-75, btnY), new Vector2(60, 50), UIHelpers.BgLight);
            nextBtn = nextGO.AddComponent<Button>(); nextBtn.onClick.AddListener(onNext);
            MakeText(nextGO.GetComponent<RectTransform>(), "Arr", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 50), 22, TextAnchor.MiddleCenter, UIHelpers.OffWhite).text = ">";
        }

        private Button CreateToggleRow(RectTransform parent, string label, float y, out Text valueText, UnityEngine.Events.UnityAction onClick)
        {
            MakeText(parent, $"{label}_Label", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(75, y), new Vector2(300, 50), 22, TextAnchor.MiddleLeft, UIHelpers.AmberOrange).text = label;
            var btnGO = MakePanel(parent, $"{label}_Toggle", new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-75, y - 5f), new Vector2(180, 50), UIHelpers.BgLight);
            var btn = btnGO.AddComponent<Button>(); btn.onClick.AddListener(onClick);
            valueText = MakeText(btnGO.GetComponent<RectTransform>(), $"{label}_Value", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180, 50), 22, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
            valueText.text = "---";
            return btn;
        }

        // A single-line numeric text field (label on the left, field on the right). DecimalNumber
        // content type keeps typing to digits + a decimal separator; the host's onCommit fires on
        // Enter or focus-loss to parse/clamp the value.
        private InputField CreateFloatInputRow(RectTransform parent, string label, float y, string placeholder, UnityEngine.Events.UnityAction<string> onCommit)
        {
            MakeText(parent, $"{label}_Label", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(75, y), new Vector2(300, 50), 22, TextAnchor.MiddleLeft, UIHelpers.AmberOrange).text = label;

            var fieldGO = MakePanel(parent, $"{label}_Field", new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-75, y - 5f), new Vector2(180, 50), UIHelpers.BgLight);
            var field = fieldGO.AddComponent<InputField>();

            var valueText = MakeText(fieldGO.GetComponent<RectTransform>(), "Text", new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 22, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
            var vtRT = valueText.GetComponent<RectTransform>(); vtRT.offsetMin = new Vector2(12, 0); vtRT.offsetMax = new Vector2(-12, 0);
            valueText.supportRichText = false;

            var ph = MakeText(fieldGO.GetComponent<RectTransform>(), "Placeholder", new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 22, TextAnchor.MiddleCenter, new Color(UIHelpers.OffWhite.r, UIHelpers.OffWhite.g, UIHelpers.OffWhite.b, 0.4f));
            var phRT = ph.GetComponent<RectTransform>(); phRT.offsetMin = new Vector2(12, 0); phRT.offsetMax = new Vector2(-12, 0);
            ph.text = placeholder; ph.fontStyle = FontStyle.Italic;

            field.textComponent = valueText;
            field.placeholder = ph;
            field.targetGraphic = fieldGO.GetComponent<Image>();
            field.contentType = InputField.ContentType.DecimalNumber;
            field.lineType = InputField.LineType.SingleLine;
            field.characterLimit = 4;
            field.onEndEdit.AddListener(onCommit);
            return field;
        }

        private Dropdown CreateDropdownRow(RectTransform parent, string label, float y, List<string> options, int currentIndex, UnityEngine.Events.UnityAction<int> onValueChanged)
        {
            MakeText(parent, $"{label}_Label", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(75, y), new Vector2(300, 50), 22, TextAnchor.MiddleLeft, UIHelpers.AmberOrange).text = label;

            var ddGO = new GameObject($"{label}_Dropdown", typeof(RectTransform), typeof(Image), typeof(Dropdown));
            ddGO.transform.SetParent(parent, false);
            var ddRT = ddGO.GetComponent<RectTransform>();
            ddRT.anchorMin = new Vector2(1, 1); ddRT.anchorMax = new Vector2(1, 1); ddRT.pivot = new Vector2(1, 1);
            ddRT.anchoredPosition = new Vector2(-75, y - 5f);
            ddRT.sizeDelta = new Vector2(440, 50);
            ddGO.GetComponent<Image>().color = UIHelpers.BgLight;
            var dropdown = ddGO.GetComponent<Dropdown>();

            var labelGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGO.transform.SetParent(ddGO.transform, false);
            var lRT = labelGO.GetComponent<RectTransform>();
            lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
            lRT.offsetMin = new Vector2(15, 0); lRT.offsetMax = new Vector2(-30, 0);
            var lText = labelGO.GetComponent<Text>();
            lText.font = UIHelpers.GetDefaultFont(22);
            lText.fontSize = 22; lText.alignment = TextAnchor.MiddleLeft; lText.color = UIHelpers.OffWhite;
            dropdown.captionText = lText;

            var arrowGO = new GameObject("Arrow", typeof(RectTransform), typeof(Text));
            arrowGO.transform.SetParent(ddGO.transform, false);
            var aRT = arrowGO.GetComponent<RectTransform>();
            aRT.anchorMin = new Vector2(1, 0); aRT.anchorMax = new Vector2(1, 1); aRT.pivot = new Vector2(1, 0.5f);
            aRT.anchoredPosition = new Vector2(-10, 0); aRT.sizeDelta = new Vector2(20, 0);
            var aText = arrowGO.GetComponent<Text>();
            aText.font = UIHelpers.GetDefaultFont(20);
            aText.fontSize = 20; aText.alignment = TextAnchor.MiddleCenter; aText.color = UIHelpers.WarmGold;
            aText.text = "v";

            var templateGO = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            templateGO.transform.SetParent(ddGO.transform, false);
            templateGO.SetActive(false);
            var tRT = templateGO.GetComponent<RectTransform>();
            tRT.anchorMin = new Vector2(0, 0); tRT.anchorMax = new Vector2(1, 0); tRT.pivot = new Vector2(0.5f, 1);
            tRT.anchoredPosition = new Vector2(0, 2);
            tRT.sizeDelta = new Vector2(0, 240);
            templateGO.GetComponent<Image>().color = UIHelpers.BgSurface;
            var scroll = templateGO.GetComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;

            var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGO.transform.SetParent(templateGO.transform, false);
            var vRT = viewportGO.GetComponent<RectTransform>();
            vRT.anchorMin = Vector2.zero; vRT.anchorMax = Vector2.one; vRT.offsetMin = Vector2.zero; vRT.offsetMax = Vector2.zero;
            viewportGO.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
            viewportGO.GetComponent<Mask>().showMaskGraphic = false;
            scroll.viewport = viewportGO.GetComponent<RectTransform>();

            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            var cRT = contentGO.GetComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0, 1); cRT.anchorMax = new Vector2(1, 1); cRT.pivot = new Vector2(0.5f, 1);
            cRT.anchoredPosition = Vector2.zero; cRT.sizeDelta = new Vector2(0, 40);
            scroll.content = cRT;

            var itemGO = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            itemGO.transform.SetParent(contentGO.transform, false);
            var iRT = itemGO.GetComponent<RectTransform>();
            iRT.anchorMin = new Vector2(0, 0.5f); iRT.anchorMax = new Vector2(1, 0.5f); iRT.pivot = new Vector2(0.5f, 0.5f);
            iRT.anchoredPosition = Vector2.zero; iRT.sizeDelta = new Vector2(0, 40);

            var itemBgGO = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
            itemBgGO.transform.SetParent(itemGO.transform, false);
            var ibRT = itemBgGO.GetComponent<RectTransform>();
            ibRT.anchorMin = Vector2.zero; ibRT.anchorMax = Vector2.one; ibRT.offsetMin = Vector2.zero; ibRT.offsetMax = Vector2.zero;
            itemBgGO.GetComponent<Image>().color = UIHelpers.BgSurface;

            var checkGO = new GameObject("Item Checkmark", typeof(RectTransform), typeof(Image));
            checkGO.transform.SetParent(itemGO.transform, false);
            var chRT = checkGO.GetComponent<RectTransform>();
            chRT.anchorMin = new Vector2(0, 0.5f); chRT.anchorMax = new Vector2(0, 0.5f); chRT.pivot = new Vector2(0.5f, 0.5f);
            chRT.anchoredPosition = new Vector2(20, 0); chRT.sizeDelta = new Vector2(20, 20);
            checkGO.GetComponent<Image>().color = UIHelpers.WarmGold;

            var itemLabelGO = new GameObject("Item Label", typeof(RectTransform), typeof(Text));
            itemLabelGO.transform.SetParent(itemGO.transform, false);
            var ilRT = itemLabelGO.GetComponent<RectTransform>();
            ilRT.anchorMin = Vector2.zero; ilRT.anchorMax = Vector2.one;
            ilRT.offsetMin = new Vector2(50, 0); ilRT.offsetMax = new Vector2(-10, 0);
            var ilText = itemLabelGO.GetComponent<Text>();
            ilText.font = UIHelpers.GetDefaultFont(22);
            ilText.fontSize = 22; ilText.alignment = TextAnchor.MiddleLeft; ilText.color = UIHelpers.OffWhite;

            var toggle = itemGO.GetComponent<Toggle>();
            toggle.targetGraphic = itemBgGO.GetComponent<Image>();
            toggle.graphic = checkGO.GetComponent<Image>();
            toggle.isOn = true;

            dropdown.template = tRT;
            dropdown.itemText = ilText;
            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            dropdown.value = Mathf.Clamp(currentIndex, 0, options.Count - 1);
            dropdown.RefreshShownValue();
            dropdown.onValueChanged.AddListener(onValueChanged);
            return dropdown;
        }

        private Slider CreateSliderRow(RectTransform parent, string label, float y, float min, float max, float value, out Text valueText)
        {
            MakeText(parent, $"{label}_Label", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(75, y), new Vector2(300, 30), 20, TextAnchor.MiddleLeft, UIHelpers.AmberOrange).text = label;
            var sliderGO = CreateSliderGO(parent, $"{label}_Slider", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y - 35), new Vector2(800, 30));
            var slider = sliderGO.GetComponent<Slider>(); slider.minValue = min; slider.maxValue = max; slider.value = value;
            valueText = MakeText(parent, $"{label}_Value", new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-75, y), new Vector2(200, 30), 20, TextAnchor.MiddleRight, UIHelpers.OffWhite);
            if (max <= 1f) { Text vt = valueText; vt.text = $"{Mathf.RoundToInt(value * 100)}%"; slider.onValueChanged.AddListener(v => vt.text = $"{Mathf.RoundToInt(v * 100)}%"); }
            return slider;
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
