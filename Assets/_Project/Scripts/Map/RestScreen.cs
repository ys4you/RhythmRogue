using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.Core;
using RhythmRogue.Battle;
using RhythmRogue.UI;
using RhythmRogue.Util;

namespace RhythmRogue.Map
{
    /// <summary>
    /// Rest node scene. Shows current HP, heal preview, animated
    /// HP bar fill, and a continue button to return to the map.
    /// 
    /// A breather between battles — warm, simple, satisfying.
    /// 
    /// Reads/writes player HP via PlayerHealth singleton.
    /// Completes the selected node in RunState on confirm.
    /// </summary>
    public class RestScreen : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("References")]
        [SerializeField] private RunState _runState;

        [Header("Healing")]
        [Tooltip("Fraction of max HP to heal (0.3 = 30%).")]
        [SerializeField] private float _healPercent = 0.3f;

        [Header("Animation")]
        [SerializeField] private float _healAnimDuration = 0.8f;

        [Header("Audio")]
        [SerializeField] private AudioClip _healSound;

        // =================================================================
        // UI — generated in code
        // =================================================================

        private Canvas _canvas;
        private Image _hpBarBG;
        private Image _hpBarFill;
        private Image _hpBarPreview;
        private Text _flavorText;
        private Text _hpText;
        private Text _healPreviewText;
        private Button _restButton;
        private Text _restButtonText;
        private AudioSource _sfx;

        private int _currentHP;
        private int _maxHP;
        private int _healAmount;
        private int _newHP;
        private bool _healed;

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Awake()
        {
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
        }

        private void Start()
        {
            var ph = PlayerHealth.Instance;
            if (ph == null)
            {
                GameLog.Error("[RestScreen] No PlayerHealth found.");
                return;
            }

            _currentHP = ph.CurrentHP;
            _maxHP = ph.MaxHP;
            _healAmount = Mathf.CeilToInt(_maxHP * _healPercent);
            _newHP = Mathf.Min(_currentHP + _healAmount, _maxHP);
            _healAmount = _newHP - _currentHP; // Actual heal after clamp

            CreateUI();
        }

        // =================================================================
        // ACTIONS
        // =================================================================

        private void OnRestClicked()
        {
            if (_healed) return;
            _healed = true;

            _restButton.interactable = false;
            StartCoroutine(HealSequence());
        }

        private IEnumerator HealSequence()
        {
            // Play sound
            if (_healSound != null)
                _sfx.PlayOneShot(_healSound, 0.5f);

            // Animate HP bar from current to new
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

                int displayHP = Mathf.RoundToInt(Mathf.Lerp(_currentHP, _newHP, t));
                _hpText.text = $"{displayHP} / {_maxHP}";

                yield return null;
            }

            // Ensure final values
            _hpBarFill.fillAmount = endFill;
            _hpBarFill.color = UIHelpers.HPColor(endFill);
            _hpText.text = $"{_newHP} / {_maxHP}";

            // Apply heal
            var ph = PlayerHealth.Instance;
            if (ph != null)
                ph.Heal(_healAmount);

            // Update text
            _flavorText.text = _healAmount > 0
                ? "You feel refreshed."
                : "You're already at full health.";

            _healPreviewText.text = "";

            // Hide preview bar
            _hpBarPreview.fillAmount = _hpBarFill.fillAmount;

            // Wait a moment then show continue
            yield return new WaitForSeconds(0.3f);

            _restButtonText.text = "Continue";
            _restButton.interactable = true;
            _restButton.onClick.RemoveAllListeners();
            _restButton.onClick.AddListener(OnContinueClicked);
        }

        private void OnContinueClicked()
        {
            _restButton.interactable = false;

            // Complete node in RunState
            if (_runState != null)
                _runState.CompleteSelectedNode();

            // Return to map
            var tm = SceneTransitionManager.Instance;
            if (tm != null)
            {
                tm.GoToMap();
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    SceneTransitionManager.MAP_SCENE);
            }
        }

        // =================================================================
        // UI CREATION
        // =================================================================

        private void CreateUI()
        {
            // Canvas
            GameObject canvasGO = new GameObject("RestCanvas");
            canvasGO.transform.SetParent(transform);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(384, 216);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // EventSystem
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                GameObject esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();

            // Background tint — warm dark
            GameObject bgGO = MakePanel(canvasRT, "BG",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                new Color(0.12f, 0.08f, 0.06f));
            bgGO.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            bgGO.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            // Campfire glow (simple warm circle in center-bottom)
            GameObject glowGO = MakePanel(canvasRT, "Glow",
                new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(80, 80),
                new Color(1f, 0.6f, 0.2f, 0.15f));

            // Flavor text — top
            _flavorText = MakeText(canvasRT, "Flavor",
                new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(300, 16),
                8, TextAnchor.MiddleCenter, new Color(0.9f, 0.8f, 0.6f));
            _flavorText.fontStyle = FontStyle.Italic;
            _flavorText.text = "You take a moment to rest...";

            // HP bar background
            _hpBarBG = MakePanel(canvasRT, "HPBarBG",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 5), new Vector2(160, 10),
                new Color(0.15f, 0.15f, 0.15f, 0.9f)).GetComponent<Image>();

            RectTransform barParent = _hpBarBG.GetComponent<RectTransform>();

            // HP bar preview fill (lighter, shows what HP will be after heal)
            GameObject previewGO = MakePanel(barParent, "PreviewFill",
                Vector2.zero, Vector2.one, new Vector2(0, 0.5f),
                Vector2.zero, Vector2.zero,
                new Color(1f, 1f, 1f, 0.25f));
            previewGO.GetComponent<RectTransform>().offsetMin = new Vector2(1, 1);
            previewGO.GetComponent<RectTransform>().offsetMax = new Vector2(-1, -1);
            _hpBarPreview = previewGO.GetComponent<Image>();
            _hpBarPreview.type = Image.Type.Filled;
            _hpBarPreview.fillMethod = Image.FillMethod.Horizontal;
            _hpBarPreview.fillAmount = (float)_newHP / _maxHP;

            // HP bar current fill
            GameObject fillGO = MakePanel(barParent, "HPFill",
                Vector2.zero, Vector2.one, new Vector2(0, 0.5f),
                Vector2.zero, Vector2.zero,
                UIHelpers.HPColor((float)_currentHP / _maxHP));
            fillGO.GetComponent<RectTransform>().offsetMin = new Vector2(1, 1);
            fillGO.GetComponent<RectTransform>().offsetMax = new Vector2(-1, -1);
            _hpBarFill = fillGO.GetComponent<Image>();
            _hpBarFill.type = Image.Type.Filled;
            _hpBarFill.fillMethod = Image.FillMethod.Horizontal;
            _hpBarFill.fillAmount = (float)_currentHP / _maxHP;

            // HP text
            _hpText = MakeText(canvasRT, "HPText",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -8), new Vector2(160, 12),
                7, TextAnchor.MiddleCenter, Color.white);
            _hpText.text = $"{_currentHP} / {_maxHP}";

            // Heal preview text
            _healPreviewText = MakeText(canvasRT, "HealPreview",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -20), new Vector2(160, 10),
                6, TextAnchor.MiddleCenter, new Color(0.4f, 1f, 0.4f));

            if (_healAmount > 0)
                _healPreviewText.text = $"+{_healAmount} HP  →  {_newHP} / {_maxHP}";
            else
                _healPreviewText.text = "Already at full HP";

            // Rest button
            GameObject btnGO = MakePanel(canvasRT, "RestBtn",
                new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(70, 18),
                new Color(0.2f, 0.5f, 0.2f));
            btnGO.AddComponent<Button>();

            _restButton = btnGO.GetComponent<Button>();
            _restButton.onClick.AddListener(OnRestClicked);

            _restButtonText = MakeText(btnGO.GetComponent<RectTransform>(), "BtnText",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(70, 18),
                7, TextAnchor.MiddleCenter, Color.white);
            _restButtonText.text = "Rest";
            _restButtonText.fontStyle = FontStyle.Bold;
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
    }
}
