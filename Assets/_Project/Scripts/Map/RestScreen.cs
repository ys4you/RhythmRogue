using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.Core;
using RhythmRogue.Core.Audio;
using RhythmRogue.Battle;
using RhythmRogue.UI.Navigation;

namespace RhythmRogue.UI
{
    public class RestScreen : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RunState _runState;
        [Header("Heal")]
        [SerializeField] [Range(0.1f, 1f)] private float _healPercent = 0.3f;

        private Canvas _canvas;
        private RectTransform _canvasRT;
        private Text _titleText, _hpText, _flavorText;
        private Button _restBtn, _continueBtn;
        private Image _hpFill;
        private bool _hasRested;
        private UIFocusSetter _focusSetter;
        private UICancelHandler _cancelHandler;

        private void Start()
        {
            CreateUI();
            _focusSetter = gameObject.AddComponent<UIFocusSetter>();
            _focusSetter.SetDefault(_restBtn.gameObject);
            _cancelHandler = gameObject.AddComponent<UICancelHandler>();
            _cancelHandler.SetBaseAction(OnContinue);
            UpdateHPDisplay();

            // Continue the map's shamanic ambient through the rest screen. Idempotent:
            // if the track is already playing (it usually is, having come from the map
            // before the battle), this is a no-op. If we're returning from a battle
            // (which stopped the music), it fades back in.
            MusicManager.Instance.Play(MusicTrack.MapShamanic);
        }

        private void OnRest()
        {
            if (_hasRested) return;
            _hasRested = true;

            var ph = PlayerHealth.Instance;
            if (ph != null)
            {
                int healAmount = Mathf.RoundToInt(ph.MaxHP * _healPercent);
                ph.Heal(healAmount);
            }

            var mgr = AudioManager.Instance;
            if (mgr != null) mgr.Play(SfxId.Heal);

            _restBtn.interactable = false;
            _restBtn.GetComponent<Image>().color = UIHelpers.Shadow;
            _flavorText.text = "You feel restored.";
            _focusSetter.FocusOn(_continueBtn.gameObject);
            StartCoroutine(AnimateHPRefill());
        }

        private IEnumerator AnimateHPRefill()
        {
            float t = 0f, dur = 0.5f;
            float start = _hpFill.fillAmount;
            var ph = PlayerHealth.Instance;
            float end = ph != null ? ph.HPPercent : 1f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                _hpFill.fillAmount = Mathf.Lerp(start, end, Mathf.SmoothStep(0, 1, t / dur));
                _hpFill.color = UIHelpers.HPColor(_hpFill.fillAmount);
                yield return null;
            }
            _hpFill.fillAmount = end;
            UpdateHPDisplay();
        }

        private void OnContinue()
        {
            if (_runState != null) _runState.CompleteSelectedNode();
            var tm = SceneTransitionManager.Instance;
            if (tm != null) tm.GoToMap();
            else UnityEngine.SceneManagement.SceneManager.LoadScene(SceneTransitionManager.MAP_SCENE);
        }

        private void UpdateHPDisplay()
        {
            var ph = PlayerHealth.Instance;
            if (ph != null)
            {
                _hpText.text = $"HP: {ph.CurrentHP} / {ph.MaxHP}";
                _hpFill.fillAmount = ph.HPPercent;
                _hpFill.color = UIHelpers.HPColor(ph.HPPercent);
            }
        }

        private void CreateUI()
        {
            var canvasGO = new GameObject("RestCanvas");
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

            // Background: deep palette dark
            var bgGO = MakePanel(_canvasRT, "BG", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, UIHelpers.BgDeep);
            bgGO.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            bgGO.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // Warm "campfire" glow at center: subtle rust/amber tint
            var glowGO = MakePanel(_canvasRT, "Glow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 900), new Color(UIHelpers.RustOrange.r, UIHelpers.RustOrange.g, UIHelpers.RustOrange.b, 0.12f));

            // Title
            _titleText = MakeText(_canvasRT, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -120), new Vector2(1500, 100), 56, TextAnchor.MiddleCenter, UIHelpers.WarmGold);
            _titleText.fontStyle = FontStyle.Bold;
            _titleText.text = "REST";

            _flavorText = MakeText(_canvasRT, "Flavor", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -210), new Vector2(1200, 60), 24, TextAnchor.MiddleCenter, UIHelpers.AmberOrange);
            _flavorText.fontStyle = FontStyle.Italic;
            _flavorText.text = "You gather around the fire and catch your breath.";

            // HP display
            _hpText = MakeText(_canvasRT, "HPText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 80), new Vector2(600, 50), 32, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
            _hpText.text = "HP: 100 / 100";

            // HP Bar
            var hpBarBG = MakePanel(_canvasRT, "HPBarBG", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(600, 40), new Color(UIHelpers.BgSurface.r, UIHelpers.BgSurface.g, UIHelpers.BgSurface.b, 0.9f));
            var hpFillObj = MakePanel(hpBarBG.GetComponent<RectTransform>(), "HPFill", new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero, UIHelpers.WarmGold);
            _hpFill = hpFillObj.GetComponent<Image>();
            _hpFill.type = Image.Type.Filled; _hpFill.fillMethod = Image.FillMethod.Horizontal; _hpFill.fillAmount = 1f;
            hpFillObj.GetComponent<RectTransform>().offsetMin = new Vector2(3, 3);
            hpFillObj.GetComponent<RectTransform>().offsetMax = new Vector2(-3, -3);

            // Heal preview text
            int healPct = Mathf.RoundToInt(_healPercent * 100);
            var preview = MakeText(_canvasRT, "Preview", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -50), new Vector2(1000, 50), 22, TextAnchor.MiddleCenter, UIHelpers.AmberOrange);
            preview.text = $"Resting heals {healPct}% of your maximum HP";

            // Buttons
            float btnY = -180f, btnW = 360f, btnH = 80f, btnGap = 40f;
            _restBtn = MakeButton(_canvasRT, "RestBtn", "Rest", new Vector2(0.5f, 0.5f), new Vector2(-(btnW * 0.5f + btnGap * 0.5f), btnY), new Vector2(btnW, btnH), UIHelpers.RustOrange);
            _restBtn.onClick.AddListener(OnRest);

            _continueBtn = MakeButton(_canvasRT, "ContinueBtn", "Continue", new Vector2(0.5f, 0.5f), new Vector2(btnW * 0.5f + btnGap * 0.5f, btnY), new Vector2(btnW, btnH), UIHelpers.BgLight);
            _continueBtn.onClick.AddListener(OnContinue);

            UINavigationHelper.WireHorizontal(_restBtn, _continueBtn);
            UISelectableStyle.Apply(_restBtn); UISelectableStyle.Apply(_continueBtn);
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
            t.font = UIHelpers.GetDefaultFont(30);
            t.fontSize = 30; t.alignment = TextAnchor.MiddleCenter;
            t.color = UIHelpers.OffWhite; t.fontStyle = FontStyle.Bold; t.text = label;
            return obj.GetComponent<Button>();
        }
    }
}