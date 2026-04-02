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
    ///   - Per-judgment screen shake (configurable intensity per tier)
    ///   - Hit SFX via PlayOneShot with pitch variation
    ///   - Receptor glow via ReceptorAnimator
    ///   - Combo milestone banners
    /// 
    /// Lane positions for text placement are read directly from the highway
    /// (no more hardcoded defaults).
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

        [Header("Receptor Animator (optional)")]
        [Tooltip("If assigned, triggers glow effects on judgments. " +
                 "If empty, auto-detects on the highway GameObject.")]
        [SerializeField] private ReceptorAnimator _receptorAnimator;

        [Header("Hit Particles (optional)")]
        [Tooltip("If assigned, spawns particle bursts on judgments. " +
                 "If empty, auto-detects on this GameObject or highway.")]
        [SerializeField] private HitParticles _hitParticles;

        [Header("Screen Shake")]
        [Tooltip("Pixel displacement on Miss. 0 = disabled.")]
        [SerializeField] private float _missShakeIntensity = 2f;
        [Tooltip("Pixel displacement on Bad. 0 = disabled.")]
        [SerializeField] private float _badShakeIntensity = 0f;
        [Tooltip("Subtle pixel bump on Perfect. 0 = disabled.")]
        [SerializeField] private float _perfectShakeIntensity = 0.5f;
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

        private readonly List<JudgmentTextInstance> _textPool = new();
        private int _nextTextIndex;

        private JudgmentTextInstance _milestoneText;

        private Vector3 _cameraOriginalPos;
        private float _shakeTimer;
        private float _shakeIntensity;

        private int _activeTextCount;

        // =================================================================
        // COLORS
        // =================================================================

        private static readonly Color PerfectColor = new(1f, 0.85f, 0f);
        private static readonly Color GoodColor = new(0.3f, 1f, 0.3f);
        private static readonly Color BadColor = new(1f, 0.6f, 0.2f);
        private static readonly Color MissColor = new(1f, 0.25f, 0.25f);

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Awake()
        {
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            _cameraOriginalPos = _mainCamera != null
                ? _mainCamera.transform.localPosition
                : Vector3.zero;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;

            // Auto-detect ReceptorAnimator on the highway if not assigned
            if (_receptorAnimator == null && _highway != null)
                _receptorAnimator = _highway.GetComponent<ReceptorAnimator>();

            // Auto-detect HitParticles
            if (_hitParticles == null)
                _hitParticles = GetComponent<HitParticles>();
            if (_hitParticles == null && _highway != null)
                _hitParticles = _highway.GetComponent<HitParticles>();

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
            if (_activeTextCount > 0)
            {
                foreach (var inst in _textPool)
                {
                    if (!inst.Root.gameObject.activeSelf) continue;

                    UpdateTextInstance(inst, dt);

                    if (!inst.Root.gameObject.activeSelf)
                        _activeTextCount--;
                }
            }

            // Animate milestone text
            if (_milestoneText != null && _milestoneText.Root.gameObject.activeSelf)
                UpdateTextInstance(_milestoneText, dt);

            // Screen shake decay
            UpdateScreenShake(dt);
        }

        // =================================================================
        // SCREEN SHAKE
        // =================================================================

        private void TriggerShake(float intensity)
        {
            if (intensity <= 0f) return;

            _shakeIntensity = intensity;
            _shakeTimer = _shakeDuration;
        }

        private void UpdateScreenShake(float dt)
        {
            if (_shakeTimer <= 0f || _mainCamera == null)
                return;

            _shakeTimer -= dt;

            if (_shakeTimer > 0f)
            {
                float fade = _shakeTimer / _shakeDuration;
                float intensity = _shakeIntensity * fade;
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

        // =================================================================
        // EVENT HANDLERS
        // =================================================================

        private void HandleJudgment(JudgmentResult result)
        {
            // Judgment text
            ShowJudgmentText(result);

            // Receptor glow via animator
            int lane = Mathf.Clamp(result.Lane, 0, 3);
            if (_receptorAnimator != null)
                _receptorAnimator.TriggerGlow(lane, result.Judgment);

            // Particle burst
            if (_hitParticles != null)
                _hitParticles.Burst(lane, result.Judgment);

            // Per-judgment screen shake
            float shake = result.Judgment switch
            {
                Judgment.Perfect => _perfectShakeIntensity,
                Judgment.Bad     => _badShakeIntensity,
                Judgment.Miss    => _missShakeIntensity,
                _                => 0f  // Good = no shake
            };
            TriggerShake(shake);

            // Hit SFX (not on Miss — silence = failure)
            if (result.Judgment != Judgment.Miss && _hitSound != null)
            {
                float pitch = result.Judgment switch
                {
                    Judgment.Perfect => 1.1f,
                    Judgment.Good    => 1.0f,
                    _                => 0.9f
                };
                _sfxSource.pitch = pitch;
                _sfxSource.PlayOneShot(_hitSound, _hitVolume);
            }
        }

        private void HandleMilestone(int milestone)
        {
            ShowMilestoneText(milestone);

            // Milestone particle burst
            if (_hitParticles != null)
                _hitParticles.MilestoneBurst(milestone);
        }

        // =================================================================
        // JUDGMENT TEXT
        // =================================================================

        private void ShowJudgmentText(JudgmentResult result)
        {
            var inst = GetNextText();

            string label = result.Judgment switch
            {
                Judgment.Perfect => "PERFECT",
                Judgment.Good    => "GOOD",
                Judgment.Bad     => "BAD",
                _                => "MISS"
            };

            Color color = result.Judgment switch
            {
                Judgment.Perfect => PerfectColor,
                Judgment.Good    => GoodColor,
                Judgment.Bad     => BadColor,
                _                => MissColor
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

            // Position at the lane's hit line — read from highway
            int lane = Mathf.Clamp(result.Lane, 0, 3);
            float laneX = GetLaneX(lane);
            float hitY = _highway != null ? _highway.HitLineY : 0f;

            Vector3 worldPos = new Vector3(laneX, hitY + 0.5f, 0f);
            Vector2 screenPos = _mainCamera.WorldToScreenPoint(worldPos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRT, screenPos, null, out Vector2 localPos);
            inst.Root.anchoredPosition = localPos;

            // Start animation
            inst.Timer = 0.5f;
            inst.Root.localScale = Vector3.one * 1.4f;
            inst.Root.gameObject.SetActive(true);
            _activeTextCount++;
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
        // LANE POSITIONS — read from highway (source of truth)
        // =================================================================

        private float GetLaneX(int lane)
        {
            if (_highway != null && _highway.LanePositions != null && lane < _highway.LanePositions.Count)
                return _highway.LanePositions[lane];

            return 0f;
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