using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using RhythmRogue.Core;
using RhythmRogue.Core.Audio;
using RhythmRogue.Core.Display;
using RhythmRogue.Battle;
using RhythmRogue.Data;
using RhythmRogue.Util;
using RhythmRogue.UI.Navigation;

namespace RhythmRogue.UI
{
    /// <summary>
    /// Main menu screen. Owns the menu buttons + seed entry, and delegates the entire
    /// settings UI to the shared SettingsPanel component (same panel the in-run pause menu
    /// uses), so Audio / Controls / Display settings have a single implementation.
    /// </summary>
    public class MainMenuScreen : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RunState _runState;
        [SerializeField] private InputActionAsset _rhythmActions;
        [Tooltip("The onboarding area launched by the How to Play button: a short, gentle, single-" +
                 "file path that teaches the basics. Leave unassigned and the button is hidden.")]
        [SerializeField] private Area _onboardingArea;
        [Tooltip("If on, a brand-new player (who has never seen the onboarding) is sent into it the " +
                 "first time they click New Run, instead of straight into a normal run. Skippable: " +
                 "they can pause and quit back to the menu, and the flag is set the moment they are " +
                 "routed in, so it only forces once. Needs onboardingArea assigned. Turn off to " +
                 "disable the gate entirely.")]
        [SerializeField] private bool _forceOnboardingOnFirstLaunch = true;
        [Header("Version")]
        [SerializeField] private string _versionText = "Prototype v0.1";

        private Canvas _canvas;
        private RectTransform _canvasRT;
        private InputField _seedInput;
        private Button _newRunBtn, _seedGoBtn, _settingsBtn, _quitBtn, _howToPlayBtn;
        private SettingsPanel _settings;
        private UIFocusSetter _focusSetter;
        private UICancelHandler _cancelHandler;

        // Run difficulty (forgiveness) tier selector. Display order is Relaxed / Normal / Hard;
        // the chosen tier is written to RunState.Tier when a run starts. Defaults to Normal.
        private static readonly DifficultyTier[] TierOrder =
            { DifficultyTier.Relaxed, DifficultyTier.Normal, DifficultyTier.Hard };
        private DifficultyTier _selectedTier = DifficultyTier.Normal;
        private Button[] _tierButtons;
        private Text[] _tierButtonLabels;
        private Text _tierDescription;

        private void Start()
        {
            if (_rhythmActions != null) KeybindManager.Initialize(_rhythmActions);
            DisplaySettings.ApplyAll();
            MusicManager.Instance.Play(MusicTrack.MenuDrone);
            CreateUI();
            SetupNavigation();
        }

        private void SetupNavigation()
        {
            // Vertical chain adapts to whether the optional How to Play button exists.
            if (_howToPlayBtn != null)
            {
                UINavigationHelper.WireVerticalNoWrap(_newRunBtn, _howToPlayBtn, _seedGoBtn, _settingsBtn, _quitBtn);
                UINavigationHelper.Wire(_seedInput, up: _howToPlayBtn, down: _settingsBtn, right: _seedGoBtn);
                UISelectableStyle.Apply(_howToPlayBtn);
            }
            else
            {
                UINavigationHelper.WireVerticalNoWrap(_newRunBtn, _seedGoBtn, _settingsBtn, _quitBtn);
                UINavigationHelper.Wire(_seedInput, up: _newRunBtn, down: _settingsBtn, right: _seedGoBtn);
            }
            UINavigationHelper.AddLink(_seedGoBtn, left: _seedInput);
            _seedInput.onEndEdit.AddListener(_ => { if (!_seedInput.isFocused) _focusSetter.FocusOn(_seedGoBtn.gameObject); });
            UISelectableStyle.Apply(_newRunBtn); UISelectableStyle.Apply(_seedGoBtn);
            UISelectableStyle.Apply(_settingsBtn); UISelectableStyle.Apply(_quitBtn);
            UISelectableStyle.Apply(_seedInput);

            // Tier selector navigation: horizontal row; each tier drops to New Run, and New Run
            // rises to the middle (Normal) tier. UISelectableStyle gives focus visuals + SFX.
            if (_tierButtons != null && _tierButtons.Length == 3)
            {
                UINavigationHelper.WireHorizontalNoWrap(_tierButtons[0], _tierButtons[1], _tierButtons[2]);
                for (int i = 0; i < _tierButtons.Length; i++)
                {
                    UINavigationHelper.AddLink(_tierButtons[i], down: _newRunBtn);
                    UISelectableStyle.Apply(_tierButtons[i]);
                }
                UINavigationHelper.AddLink(_newRunBtn, up: _tierButtons[1]);
            }

            _focusSetter = gameObject.AddComponent<UIFocusSetter>();
            _focusSetter.SetDefault(_newRunBtn.gameObject);
            _cancelHandler = gameObject.AddComponent<UICancelHandler>();
            _cancelHandler.SetBaseAction(OnQuit);

            // Build the shared settings panel now that focus + cancel handlers exist.
            _settings = gameObject.AddComponent<SettingsPanel>();
            _settings.Build(_canvasRT, _focusSetter, _cancelHandler);
            _settings.OnCloseRequested += OnSettingsClosed;
        }

        // New Run doubles as the first-timer gate: a player who has never seen the onboarding is
        // sent there once instead of straight into a normal run. The flag is set at the moment of
        // routing, so leaving early (pause -> quit back to the menu) still counts and the next
        // New Run is a normal run. Skippable, never a lock. How to Play remains the manual replay.
        private void OnNewRun()
        {
            if (_forceOnboardingOnFirstLaunch && _onboardingArea != null && !OnboardingState.IsComplete)
            {
                OnboardingState.MarkComplete();
                GameLog.Info("[MainMenu] First New Run: routing into onboarding.");
                StartRun(null, _onboardingArea);
                return;
            }
            StartRun(null, null);
        }
        private void OnSeededRun()
        {
            string seed = _seedInput != null ? _seedInput.text.Trim() : "";
            StartRun(string.IsNullOrEmpty(seed) ? null : seed, null);
        }
        // Manual replay of the onboarding. Also marks it seen, so a first-timer who comes here
        // instead of New Run is not forced into it again on their next New Run.
        private void OnHowToPlay()
        {
            OnboardingState.MarkComplete();
            StartRun(null, _onboardingArea);
        }

        private void StartRun(string seed, Area area)
        {
            if (_runState == null) { GameLog.Error("[MainMenu] No RunState!"); return; }
            if (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsTransitioning) return;
            _runState.Tier = _selectedTier;
            // null = use the map scene's default area; set = launch that area (e.g. onboarding).
            // Cleared by EndRun when the previous run finished, so a normal New Run starts clean.
            _runState.SelectedArea = area;
            _runState.StartNewRun(seed);
            var ph = PlayerHealth.Instance; if (ph != null) ph.ResetForNewRun();
            GameLog.Info($"[MainMenu] Starting run. Seed: {_runState.Seed}{(area != null ? $" | area: {area.areaName}" : "")}");
            var tm = SceneTransitionManager.Instance;
            if (tm != null) tm.GoToMap();
            else UnityEngine.SceneManagement.SceneManager.LoadScene(SceneTransitionManager.MAP_SCENE);
        }

        private void OnSettings() => _settings.Open();

        private void OnSettingsClosed()
        {
            // SettingsPanel already saved + hid itself; just restore menu focus.
            _focusSetter.FocusOn(_settingsBtn.gameObject);
        }

        private void OnQuit()
        {
            GameLog.Info("[MainMenu] Quit"); Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

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

            var title = MakeText(_canvasRT, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(1500, 120), 64, TextAnchor.MiddleCenter, UIHelpers.WarmGold);
            title.fontStyle = FontStyle.Bold; title.text = "RHYTHM ROGUE";

            var subtitle = MakeText(_canvasRT, "Subtitle", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -230), new Vector2(1000, 50), 22, TextAnchor.MiddleCenter, UIHelpers.AmberOrange);
            subtitle.text = "a rhythm roguelike";

            MakePanel(_canvasRT, "AccentLine", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -268), new Vector2(560, 2), new Color(UIHelpers.BgLight.r, UIHelpers.BgLight.g, UIHelpers.BgLight.b, 0.5f));

            // Difficulty (forgiveness) tier selector sits between the subtitle and the run buttons.
            CreateTierSelector(labelY: -302f, rowY: -350f, descY: -396f);

            float btnW = 400f, btnH = 80f, startY = -462f, gap = 98f;
            _newRunBtn = MakeMenuButton("New Run", startY, btnW, btnH, UIHelpers.RustOrange, OnNewRun);
            int row = 1;
            if (_onboardingArea != null)
                _howToPlayBtn = MakeMenuButton("How to Play", startY - gap * row++, btnW, btnH, UIHelpers.AmberOrange, OnHowToPlay);
            CreateSeedEntry(startY - gap * row++, btnW, btnH);
            _settingsBtn = MakeMenuButton("Settings", startY - gap * row++, btnW, btnH, UIHelpers.BgLight, OnSettings);
            _quitBtn = MakeMenuButton("Quit", startY - gap * row++, btnW, btnH, UIHelpers.Shadow, OnQuit);

            var version = MakeText(_canvasRT, "Version", new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 20), new Vector2(400, 40), 18, TextAnchor.MiddleRight, UIHelpers.Shadow);
            version.text = _versionText;
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

        // ---- Difficulty tier selector ----

        private void CreateTierSelector(float labelY, float rowY, float descY)
        {
            if (_runState != null) _selectedTier = _runState.Tier;

            var label = MakeText(_canvasRT, "TierLabel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, labelY), new Vector2(600, 34), 22, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
            label.text = "DIFFICULTY";

            _tierButtons = new Button[TierOrder.Length];
            _tierButtonLabels = new Text[TierOrder.Length];

            float bW = 150f, bH = 56f, step = bW + 14f;
            float x0 = -step; // three buttons, middle one centered at x = 0

            for (int i = 0; i < TierOrder.Length; i++)
            {
                DifficultyTier tier = TierOrder[i];
                var go = MakePanel(_canvasRT, $"Tier_{tier}", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x0 + i * step, rowY), new Vector2(bW, bH), UIHelpers.BgLight);
                var btn = go.AddComponent<Button>();
                btn.onClick.AddListener(() => SelectTier(tier));
                var txt = MakeText(go.GetComponent<RectTransform>(), "Text", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(bW, bH), 26, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
                txt.fontStyle = FontStyle.Bold; txt.text = TierLabel(tier);
                _tierButtons[i] = btn;
                _tierButtonLabels[i] = txt;
            }

            _tierDescription = MakeText(_canvasRT, "TierDesc", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, descY), new Vector2(820, 28), 20, TextAnchor.MiddleCenter, UIHelpers.AmberOrange);
            _tierDescription.fontStyle = FontStyle.Italic;

            RefreshTierVisuals();
        }

        private void SelectTier(DifficultyTier tier)
        {
            _selectedTier = tier;
            if (_runState != null) _runState.Tier = tier;
            RefreshTierVisuals();
        }

        private void RefreshTierVisuals()
        {
            if (_tierButtons == null) return;
            for (int i = 0; i < _tierButtons.Length; i++)
            {
                bool selected = TierOrder[i] == _selectedTier;
                var img = _tierButtons[i] != null ? _tierButtons[i].GetComponent<Image>() : null;
                if (img != null) img.color = selected ? UIHelpers.WarmGold : UIHelpers.BgLight;
                if (_tierButtonLabels[i] != null)
                    _tierButtonLabels[i].color = selected ? UIHelpers.BgDeep : UIHelpers.OffWhite;
            }
            if (_tierDescription != null) _tierDescription.text = TierDescriptionText(_selectedTier);
        }

        private static string TierLabel(DifficultyTier tier) => tier switch
        {
            DifficultyTier.Relaxed => "Relaxed",
            DifficultyTier.Hard => "Hard",
            _ => "Normal"
        };

        private static string TierDescriptionText(DifficultyTier tier) => tier switch
        {
            DifficultyTier.Relaxed => "Wider timing windows and a lighter chart. A gentler way in.",
            DifficultyTier.Hard => "Tighter timing and a denser chart. For when Normal feels easy.",
            _ => "The intended balance of timing and note density."
        };

        private Button MakeMenuButton(string label, float y, float w, float h, Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            var btnGO = MakePanel(_canvasRT, $"Btn_{label}", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(w, h), bgColor);
            var btn = btnGO.AddComponent<Button>(); btn.onClick.AddListener(onClick);
            var txt = MakeText(btnGO.GetComponent<RectTransform>(), "Text", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h), 30, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
            txt.fontStyle = FontStyle.Bold; txt.text = label;
            return btn;
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
