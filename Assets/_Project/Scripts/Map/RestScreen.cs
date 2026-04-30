using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.Core;
using RhythmRogue.Battle;
using RhythmRogue.UI;
using RhythmRogue.UI.Navigation;
using RhythmRogue.Util;

namespace RhythmRogue.Map
{
    /// <summary>
    /// Rest node scene. 1920x1080 reference resolution.
    /// </summary>
    public class RestScreen : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RunState _runState;
        [Header("Healing")]
        [SerializeField] private float _healPercent = 0.3f;
        [Header("Animation")]
        [SerializeField] private float _healAnimDuration = 0.8f;
        [Header("Audio")]
        [SerializeField] private AudioClip _healSound;

        private Canvas _canvas;
        private Image _hpBarBG, _hpBarFill, _hpBarPreview;
        private Text _flavorText, _hpText, _healPreviewText;
        private Button _restButton;
        private Text _restButtonText;
        private AudioSource _sfx;
        private int _currentHP, _maxHP, _healAmount, _newHP;
        private bool _healed;
        private UIFocusSetter _focusSetter;

        private void Awake() { _sfx = gameObject.AddComponent<AudioSource>(); _sfx.playOnAwake = false; }

        private void Start()
        {
            var ph = PlayerHealth.Instance;
            if (ph == null) { GameLog.Error("[RestScreen] No PlayerHealth."); return; }
            _currentHP = ph.CurrentHP; _maxHP = ph.MaxHP;
            _healAmount = Mathf.CeilToInt(_maxHP * _healPercent);
            _newHP = Mathf.Min(_currentHP + _healAmount, _maxHP);
            _healAmount = _newHP - _currentHP;
            CreateUI();
            UISelectableStyle.Apply(_restButton);
            _focusSetter = gameObject.AddComponent<UIFocusSetter>();
            _focusSetter.SetDefault(_restButton.gameObject);
        }

        private void OnRestClicked()
        {
            if (_healed) return;
            _healed = true;
            _restButton.interactable = false;
            StartCoroutine(HealSequence());
        }

        private IEnumerator HealSequence()
        {
            if (_healSound != null) _sfx.PlayOneShot(_healSound, 0.5f);
            float startFill = (float)_currentHP / _maxHP;
            float endFill = (float)_newHP / _maxHP;
            float elapsed = 0f;
            while (elapsed < _healAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / _healAnimDuration);
                float fill = Mathf.Lerp(startFill, endFill, t);
                _hpBarFill.fillAmount = fill;
                _hpBarFill.color = UIHelpers.HPColor(fill);
                _hpText.text = $"{Mathf.RoundToInt(Mathf.Lerp(_currentHP, _newHP, t))} / {_maxHP}";
                yield return null;
            }
            _hpBarFill.fillAmount = endFill;
            _hpBarFill.color = UIHelpers.HPColor(endFill);
            _hpText.text = $"{_newHP} / {_maxHP}";
            var ph = PlayerHealth.Instance;
            if (ph != null) ph.Heal(_healAmount);
            _flavorText.text = _healAmount > 0 ? "You feel refreshed." : "You're already at full health.";
            _healPreviewText.text = "";
            _hpBarPreview.fillAmount = _hpBarFill.fillAmount;
            yield return new WaitForSeconds(0.3f);
            _restButtonText.text = "Continue";
            _restButton.interactable = true;
            _restButton.onClick.RemoveAllListeners();
            _restButton.onClick.AddListener(OnContinueClicked);
            UISelectableStyle.Apply(_restButton);
            _focusSetter.FocusOn(_restButton.gameObject);
        }

        private void OnContinueClicked()
        {
            _restButton.interactable = false;
            if (_runState != null) _runState.CompleteSelectedNode();
            var tm = SceneTransitionManager.Instance;
            if (tm != null) tm.GoToMap();
            else UnityEngine.SceneManagement.SceneManager.LoadScene(SceneTransitionManager.MAP_SCENE);
        }

        private void CreateUI()
        {
            GameObject canvasGO = new GameObject("RestCanvas");
            canvasGO.transform.SetParent(transform);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            UIEventSystemProvider.EnsureEventSystem();
            RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();

            var bgGO = MakePanel(canvasRT, "BG", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.12f, 0.08f, 0.06f));
            bgGO.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            bgGO.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            MakePanel(canvasRT, "Glow", new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 400), new Color(1f, 0.6f, 0.2f, 0.15f));

            _flavorText = MakeText(canvasRT, "Flavor", new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 80), 36, TextAnchor.MiddleCenter, new Color(0.9f, 0.8f, 0.6f));
            _flavorText.fontStyle = FontStyle.Italic;
            _flavorText.text = "You take a moment to rest...";

            _hpBarBG = MakePanel(canvasRT, "HPBarBG", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 25), new Vector2(800, 50), new Color(0.15f, 0.15f, 0.15f, 0.9f)).GetComponent<Image>();
            RectTransform barParent = _hpBarBG.GetComponent<RectTransform>();

            var previewGO = MakePanel(barParent, "PreviewFill", Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.25f));
            previewGO.GetComponent<RectTransform>().offsetMin = new Vector2(4, 4);
            previewGO.GetComponent<RectTransform>().offsetMax = new Vector2(-4, -4);
            _hpBarPreview = previewGO.GetComponent<Image>();
            _hpBarPreview.type = Image.Type.Filled;
            _hpBarPreview.fillMethod = Image.FillMethod.Horizontal;
            _hpBarPreview.fillAmount = (float)_newHP / _maxHP;

            var fillGO = MakePanel(barParent, "HPFill", Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero, UIHelpers.HPColor((float)_currentHP / _maxHP));
            fillGO.GetComponent<RectTransform>().offsetMin = new Vector2(4, 4);
            fillGO.GetComponent<RectTransform>().offsetMax = new Vector2(-4, -4);
            _hpBarFill = fillGO.GetComponent<Image>();
            _hpBarFill.type = Image.Type.Filled;
            _hpBarFill.fillMethod = Image.FillMethod.Horizontal;
            _hpBarFill.fillAmount = (float)_currentHP / _maxHP;

            _hpText = MakeText(canvasRT, "HPText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(800, 60), 32, TextAnchor.MiddleCenter, Color.white);
            _hpText.text = $"{_currentHP} / {_maxHP}";

            _healPreviewText = MakeText(canvasRT, "HealPreview", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -100), new Vector2(800, 50), 26, TextAnchor.MiddleCenter, new Color(0.4f, 1f, 0.4f));
            _healPreviewText.text = _healAmount > 0 ? $"+{_healAmount} HP  →  {_newHP} / {_maxHP}" : "Already at full HP";

            var btnGO = MakePanel(canvasRT, "RestBtn", new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(350, 90), new Color(0.2f, 0.5f, 0.2f));
            _restButton = btnGO.AddComponent<Button>();
            _restButton.onClick.AddListener(OnRestClicked);
            _restButtonText = MakeText(btnGO.GetComponent<RectTransform>(), "BtnText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(350, 90), 32, TextAnchor.MiddleCenter, Color.white);
            _restButtonText.text = "Rest";
            _restButtonText.fontStyle = FontStyle.Bold;
            UINavigationHelper.SetExplicit(_restButton);
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
