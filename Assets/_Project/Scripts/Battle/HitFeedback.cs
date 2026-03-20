using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Visual and audio feedback for every hit judgment.
    /// 
    /// Subscribes to JudgmentSystem.OnJudgment and ComboSystem.OnComboMilestone.
    /// Handles:
    ///   - Judgment text ("PERFECT", "GOOD", etc.) with scale+fade animation
    ///   - Early/Late subtitle
    ///   - Screen shake on Miss
    ///   - Hit SFX via PlayOneShot
    ///   - Receptor glow tint on hit
    ///   - Combo milestone banners
    /// 
    /// All text instances are pooled to avoid GC during dense sections.
    /// Sized for 384×216 reference resolution.
    /// </summary>
    public class HitFeedback : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("References")]
        [SerializeField] private JudgmentSystem _judgmentSystem;
        [SerializeField] private ComboSystem _comboSystem;
        [SerializeField] private NoteHighway _highway;
        [SerializeField] private Camera _mainCamera;

        [Header("Screen Shake")]
        [Tooltip("Pixel displacement on Miss. 0 = disabled.")]
        [SerializeField] private float _shakeIntensity = 2f;
        [Tooltip("Shake duration in seconds.")]
        [SerializeField] private float _shakeDuration = 0.1f;

        [Header("Hit SFX")]
        [SerializeField] private AudioClip _hitSound;
        [Tooltip("Volume for hit SFX. Keep low to not overpower music.")]
        [SerializeField] [Range(0f, 1f)] private float _hitVolume = 0.3f;

        [Header("Pool Size")]
        [SerializeField] private int _textPoolSize = 8;

        // =================================================================
        // STATE
        // =================================================================

        private Canvas _canvas;
        private RectTransform _canvasRT;
        private AudioSource _sfxSource;

        // Judgment text pool
        private readonly List<JudgmentTextInstance> _textPool = new();
        private int _nextTextIndex;

        // Milestone text
        private JudgmentTextInstance _milestoneText;

        // Screen shake
        private Vector3 _cameraOriginalPos;
        private float _shakeTimer;

        // Receptor glow
        private readonly float[] _receptorGlow = new float[4];

        // =================================================================
        // COLORS
        // =================================================================

        private static readonly Color PerfectColor = new Color(1f, 0.85f, 0f);   // Gold
        private static readonly Color GoodColor = new Color(0.3f, 1f, 0.3f);     // Green
        private static readonly Color BadColor = new Color(1f, 0.6f, 0.2f);      // Orange
        private static readonly Color MissColor = new Color(1f, 0.25f, 0.25f);   // Red

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Awake()
        {
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            _cameraOriginalPos = _mainCamera != null ? _mainCamera.transform.localPosition : Vector3.zero;

            // SFX source — separate from music
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;

            CreateCanvas();
            CreateTextPool();
        }

        private void OnEnable()
        {
            if (_judgmentSystem != null)
                _judgmentSystem.OnJudgment += HandleJudgment;

            if (_comboSystem != null)
                _comboSystem.OnComboMilestone += HandleMilestone;
        }

        private void OnDisable()
        {
            if (_judgmentSystem != null)
                _judgmentSystem.OnJudgment -= HandleJudgment;

            if (_comboSystem != null)
                _comboSystem.OnComboMilestone -= HandleMilestone;
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;

            // Animate text pool
            foreach (var inst in _textPool)
                UpdateTextInstance(inst, dt);

            // Animate milestone text
            if (_milestoneText != null)
                UpdateTextInstance(_milestoneText, dt);

            // Screen shake decay
            if (_shakeTimer > 0f && _mainCamera != null)
            {
                _shakeTimer -= dt;

                if (_shakeTimer > 0f)
                {
                    float intensity = _shakeIntensity * (_shakeTimer / _shakeDuration);
                    float ox = Random.Range(-intensity, intensity);
                    float oy = Random.Range(-intensity, intensity);
                    // Convert pixel offset to world units (at 32 PPU)
                    _mainCamera.transform.localPosition = _cameraOriginalPos +
                        new Vector3(ox / 32f, oy / 32f, 0f);
                }
                else
                {
                    _mainCamera.transform.localPosition = _cameraOriginalPos;
                }
            }

            // Receptor glow decay
            for (int i = 0; i < 4; i++)
            {
                if (_receptorGlow[i] > 0f)
                {
                    _receptorGlow[i] -= dt * 8f;
                    if (_receptorGlow[i] <= 0f)
                        _receptorGlow[i] = 0f;
                }
            }
        }

        // =================================================================
        // EVENT HANDLERS
        // =================================================================

        private void HandleJudgment(JudgmentResult result)
        {
            // Judgment text
            ShowJudgmentText(result);

            // Receptor glow
            if (result.Judgment != Judgment.Miss)
                _receptorGlow[Mathf.Clamp(result.Lane, 0, 3)] = 1f;

            // Screen shake on Miss
            if (result.Judgment == Judgment.Miss && _shakeIntensity > 0f)
                _shakeTimer = _shakeDuration;

            // Hit SFX (not on Miss — silence = failure)
            if (result.Judgment != Judgment.Miss && _hitSound != null)
            {
                float pitch = result.Judgment switch
                {
                    Judgment.Perfect => 1.1f,
                    Judgment.Good => 1.0f,
                    _ => 0.9f
                };
                _sfxSource.pitch = pitch;
                _sfxSource.PlayOneShot(_hitSound, _hitVolume);
            }
        }

        private void HandleMilestone(int milestone)
        {
            ShowMilestoneText(milestone);
        }

        // =================================================================
        // JUDGMENT TEXT
        // =================================================================

        private void ShowJudgmentText(JudgmentResult result)
        {
            var inst = GetNextText();

            // Main text
            string label = result.Judgment switch
            {
                Judgment.Perfect => "PERFECT",
                Judgment.Good => "GOOD",
                Judgment.Bad => "BAD",
                _ => "MISS"
            };

            Color color = result.Judgment switch
            {
                Judgment.Perfect => PerfectColor,
                Judgment.Good => GoodColor,
                Judgment.Bad => BadColor,
                _ => MissColor
            };

            inst.MainText.text = label;
            inst.MainText.color = color;

            // Early/Late subtitle
            if (!result.IsAutoMiss && result.Judgment != Judgment.Miss)
            {
                string dir = result.AdjustedOffsetMs < 0 ? "EARLY" : "LATE";
                inst.SubText.text = dir;
                inst.SubText.color = new Color(color.r, color.g, color.b, 0.7f);
                inst.SubText.gameObject.SetActive(true);
            }
            else
            {
                inst.SubText.gameObject.SetActive(false);
            }

            // Position at the lane's hit line location
            int lane = Mathf.Clamp(result.Lane, 0, 3);
            Vector3 worldPos = new Vector3(
                _highway != null ? GetLaneX(lane) : 0f,
                _highway != null ? _highway.HitLineY + 0.5f : 0f,
                0f);

            Vector2 screenPos = _mainCamera.WorldToScreenPoint(worldPos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRT, screenPos, null, out Vector2 localPos);
            inst.Root.anchoredPosition = localPos;

            // Start animation
            inst.Timer = 0.5f;
            inst.Root.localScale = Vector3.one * 1.4f;
            inst.Root.gameObject.SetActive(true);
        }

        private void ShowMilestoneText(int milestone)
        {
            if (_milestoneText == null) return;

            _milestoneText.MainText.text = $"{milestone} COMBO!";
            _milestoneText.MainText.color = PerfectColor;
            _milestoneText.SubText.gameObject.SetActive(false);

            _milestoneText.Root.anchoredPosition = new Vector2(0, 20);
            _milestoneText.Root.localScale = Vector3.one * 1.6f;
            _milestoneText.Timer = 0.8f;
            _milestoneText.Root.gameObject.SetActive(true);
        }

        private void UpdateTextInstance(JudgmentTextInstance inst, float dt)
        {
            if (!inst.Root.gameObject.activeSelf) return;

            inst.Timer -= dt;

            if (inst.Timer <= 0f)
            {
                inst.Root.gameObject.SetActive(false);
                return;
            }

            // Scale: 1.4 → 1.0 quickly, then hold
            float t = 1f - (inst.Timer / 0.5f);
            float scale = Mathf.Lerp(1.4f, 1f, Mathf.Min(t * 3f, 1f));
            inst.Root.localScale = Vector3.one * scale;

            // Fade out in last 0.2s
            float alpha = inst.Timer < 0.2f ? inst.Timer / 0.2f : 1f;
            Color mc = inst.MainText.color;
            mc.a = alpha;
            inst.MainText.color = mc;

            if (inst.SubText.gameObject.activeSelf)
            {
                Color sc = inst.SubText.color;
                sc.a = alpha * 0.7f;
                inst.SubText.color = sc;
            }

            // Drift upward
            inst.Root.anchoredPosition += new Vector2(0, dt * 20f);
        }

        // =================================================================
        // LANE POSITIONS — read from highway lane config
        // =================================================================

        private float GetLaneX(int lane)
        {
            // Match NoteHighway default lane positions
            float[] defaults = { -1.5f, -0.5f, 0.5f, 1.5f };
            return lane >= 0 && lane < defaults.Length ? defaults[lane] : 0f;
        }

        // =================================================================
        // UI CREATION
        // =================================================================

        private void CreateCanvas()
        {
            GameObject canvasGO = new GameObject("FeedbackCanvas");
            canvasGO.transform.SetParent(transform);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 110;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(384, 216);
            scaler.matchWidthOrHeight = 0.5f;

            _canvasRT = canvasGO.GetComponent<RectTransform>();
        }

        private void CreateTextPool()
        {
            for (int i = 0; i < _textPoolSize; i++)
            {
                var inst = CreateTextInstance($"JudgText_{i}");
                inst.Root.gameObject.SetActive(false);
                _textPool.Add(inst);
            }

            // Milestone text (separate, centered)
            _milestoneText = CreateTextInstance("MilestoneText");
            _milestoneText.MainText.fontSize = 10;
            _milestoneText.Root.gameObject.SetActive(false);
        }

        private JudgmentTextInstance CreateTextInstance(string name)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(_canvasRT, false);

            RectTransform rt = root.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(60, 20);

            // Main text
            GameObject mainGO = new GameObject("Main", typeof(RectTransform), typeof(Text));
            mainGO.transform.SetParent(root.transform, false);

            RectTransform mainRT = mainGO.GetComponent<RectTransform>();
            mainRT.anchorMin = new Vector2(0.5f, 0.5f);
            mainRT.anchorMax = new Vector2(0.5f, 0.5f);
            mainRT.pivot = new Vector2(0.5f, 0.5f);
            mainRT.anchoredPosition = Vector2.zero;
            mainRT.sizeDelta = new Vector2(60, 12);

            Text mainText = mainGO.GetComponent<Text>();
            mainText.font = Font.CreateDynamicFontFromOSFont("Arial", 8);
            mainText.fontSize = 8;
            mainText.fontStyle = FontStyle.Bold;
            mainText.alignment = TextAnchor.MiddleCenter;
            mainText.color = Color.white;
            mainText.horizontalOverflow = HorizontalWrapMode.Overflow;
            mainText.verticalOverflow = VerticalWrapMode.Overflow;

            // Sub text (EARLY/LATE)
            GameObject subGO = new GameObject("Sub", typeof(RectTransform), typeof(Text));
            subGO.transform.SetParent(root.transform, false);

            RectTransform subRT = subGO.GetComponent<RectTransform>();
            subRT.anchorMin = new Vector2(0.5f, 0.5f);
            subRT.anchorMax = new Vector2(0.5f, 0.5f);
            subRT.pivot = new Vector2(0.5f, 0.5f);
            subRT.anchoredPosition = new Vector2(0, -7);
            subRT.sizeDelta = new Vector2(40, 8);

            Text subText = subGO.GetComponent<Text>();
            subText.font = Font.CreateDynamicFontFromOSFont("Arial", 5);
            subText.fontSize = 5;
            subText.alignment = TextAnchor.MiddleCenter;
            subText.color = new Color(1, 1, 1, 0.6f);
            subText.horizontalOverflow = HorizontalWrapMode.Overflow;
            subText.verticalOverflow = VerticalWrapMode.Overflow;

            return new JudgmentTextInstance
            {
                Root = rt,
                MainText = mainText,
                SubText = subText,
                Timer = 0f
            };
        }

        private JudgmentTextInstance GetNextText()
        {
            var inst = _textPool[_nextTextIndex];
            _nextTextIndex = (_nextTextIndex + 1) % _textPool.Count;
            return inst;
        }

        // =================================================================
        // INNER TYPE
        // =================================================================

        private class JudgmentTextInstance
        {
            public RectTransform Root;
            public Text MainText;
            public Text SubText;
            public float Timer;
        }
    }
}
