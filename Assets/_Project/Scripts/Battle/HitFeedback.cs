using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.UI;

namespace RhythmRogue.Battle
{
    public class HitFeedback : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private JudgmentSystem _judgmentSystem;
        [SerializeField] private ComboSystem _comboSystem;
        [SerializeField] private NoteHighway _highway;
        [SerializeField] private Camera _mainCamera;

        [Header("Receptor Animator (optional)")]
        [SerializeField] private ReceptorAnimator _receptorAnimator;

        [Header("Hit Particles (optional)")]
        [SerializeField] private HitParticles _hitParticles;

        [Header("Screen Shake")]
        [SerializeField] private float _missShakeIntensity = 2f;
        [SerializeField] private float _badShakeIntensity = 0f;
        [SerializeField] private float _perfectShakeIntensity = 0.5f;
        [SerializeField] private float _shakeDuration = 0.1f;

        [Header("Hit SFX")]
        [SerializeField] private AudioClip _hitSound;
        [SerializeField] [Range(0f, 1f)] private float _hitVolume = 0.3f;

        [Header("Pool Size")]
        [SerializeField] private int _textPoolSize = 8;

        private Canvas _canvas;
        private RectTransform _canvasRT;
        private AudioSource _sfxSource;
        private readonly List<JudgmentTextInstance> _textPool = new();
        private int _nextTextIndex;
        private JudgmentTextInstance _milestoneText;
        private Vector3 _cameraOriginalPos;
        private float _shakeTimer, _shakeIntensity;
        private int _activeTextCount;

        private static readonly Color PerfectColor = new(1f, 0.85f, 0f);
        private static readonly Color GoodColor = new(0.3f, 1f, 0.3f);
        private static readonly Color BadColor = new(1f, 0.6f, 0.2f);
        private static readonly Color MissColor = new(1f, 0.25f, 0.25f);

        private void Awake()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            _cameraOriginalPos = _mainCamera != null ? _mainCamera.transform.localPosition : Vector3.zero;
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;

            if (_receptorAnimator == null && _highway != null) _receptorAnimator = _highway.GetComponent<ReceptorAnimator>();
            if (_hitParticles == null) _hitParticles = GetComponent<HitParticles>();
            if (_hitParticles == null && _highway != null) _hitParticles = _highway.GetComponent<HitParticles>();

            CreateCanvas();
            CreateTextPool();
        }

        private void OnEnable()
        {
            if (_judgmentSystem != null) _judgmentSystem.OnJudgment += HandleJudgment;
            if (_comboSystem != null) _comboSystem.OnComboMilestone += HandleMilestone;
        }

        private void OnDisable()
        {
            if (_judgmentSystem != null) _judgmentSystem.OnJudgment -= HandleJudgment;
            if (_comboSystem != null) _comboSystem.OnComboMilestone -= HandleMilestone;
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;

            if (_activeTextCount > 0)
            {
                foreach (var inst in _textPool)
                {
                    if (!inst.Root.gameObject.activeSelf) continue;
                    UpdateTextInstance(inst, dt);
                    if (!inst.Root.gameObject.activeSelf) _activeTextCount--;
                }
            }

            if (_milestoneText != null && _milestoneText.Root.gameObject.activeSelf)
                UpdateTextInstance(_milestoneText, dt);

            UpdateScreenShake(dt);
        }

        private void TriggerShake(float intensity)
        {
            if (intensity <= 0f) return;
            _shakeIntensity = intensity;
            _shakeTimer = _shakeDuration;
        }

        private void UpdateScreenShake(float dt)
        {
            if (_shakeTimer <= 0f || _mainCamera == null) return;
            _shakeTimer -= dt;

            if (_shakeTimer > 0f)
            {
                float fade = _shakeTimer / _shakeDuration;
                float i = _shakeIntensity * fade;
                _mainCamera.transform.localPosition = _cameraOriginalPos +
                    new Vector3(Random.Range(-i, i) / 32f, Random.Range(-i, i) / 32f, 0f);
            }
            else
            {
                _mainCamera.transform.localPosition = _cameraOriginalPos;
            }
        }

        private void HandleJudgment(JudgmentResult result)
        {
            ShowJudgmentText(result);

            int lane = Mathf.Clamp(result.Lane, 0, 3);
            if (_receptorAnimator != null) _receptorAnimator.TriggerGlow(lane, result.Judgment);
            if (_hitParticles != null) _hitParticles.Burst(lane, result.Judgment);

            TriggerShake(result.Judgment switch
            {
                Judgment.Perfect => _perfectShakeIntensity,
                Judgment.Bad => _badShakeIntensity,
                Judgment.Miss => _missShakeIntensity,
                _ => 0f
            });

            if (result.Judgment != Judgment.Miss && _hitSound != null)
            {
                _sfxSource.pitch = result.Judgment switch { Judgment.Perfect => 1.1f, Judgment.Good => 1.0f, _ => 0.9f };
                _sfxSource.PlayOneShot(_hitSound, _hitVolume);
            }
        }

        private void HandleMilestone(int milestone)
        {
            ShowMilestoneText(milestone);
            if (_hitParticles != null) _hitParticles.MilestoneBurst(milestone);
        }

        private void ShowJudgmentText(JudgmentResult result)
        {
            var inst = GetNextText();

            inst.MainText.text = result.Judgment switch
            {
                Judgment.Perfect => "PERFECT", Judgment.Good => "GOOD",
                Judgment.Bad => "BAD", _ => "MISS"
            };

            Color color = result.Judgment switch
            {
                Judgment.Perfect => PerfectColor, Judgment.Good => GoodColor,
                Judgment.Bad => BadColor, _ => MissColor
            };
            inst.MainText.color = color;

            if (!result.IsAutoMiss && result.Judgment != Judgment.Miss)
            {
                inst.SubText.text = result.AdjustedOffsetMs < 0 ? "EARLY" : "LATE";
                inst.SubText.color = new Color(color.r, color.g, color.b, 0.7f);
                inst.SubText.gameObject.SetActive(true);
            }
            else
            {
                inst.SubText.gameObject.SetActive(false);
            }

            int lane = Mathf.Clamp(result.Lane, 0, 3);
            float laneX = _highway != null && _highway.LanePositions != null && lane < _highway.LanePositions.Count
                ? _highway.LanePositions[lane] : 0f;
            float hitY = _highway != null ? _highway.HitLineY : 0f;

            Vector3 worldPos = new Vector3(laneX, hitY + 0.5f, 0f);
            Vector2 screenPos = _mainCamera.WorldToScreenPoint(worldPos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRT, screenPos, null, out Vector2 localPos);
            inst.Root.anchoredPosition = localPos;

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
            _milestoneText.Root.anchoredPosition = new Vector2(0, 100);
            _milestoneText.Root.localScale = Vector3.one * 1.6f;
            _milestoneText.Timer = 0.8f;
            _milestoneText.Root.gameObject.SetActive(true);
        }

        private void UpdateTextInstance(JudgmentTextInstance inst, float dt)
        {
            if (!inst.Root.gameObject.activeSelf) return;
            inst.Timer -= dt;

            if (inst.Timer <= 0f) { inst.Root.gameObject.SetActive(false); return; }

            float t = 1f - (inst.Timer / 0.5f);
            inst.Root.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.Min(t * 3f, 1f));

            float alpha = inst.Timer < 0.2f ? inst.Timer / 0.2f : 1f;
            Color mc = inst.MainText.color; mc.a = alpha; inst.MainText.color = mc;

            if (inst.SubText.gameObject.activeSelf)
            {
                Color sc = inst.SubText.color; sc.a = alpha * 0.7f; inst.SubText.color = sc;
            }

            // Drift upward (scaled for 1920x1080)
            inst.Root.anchoredPosition += new Vector2(0, dt * 100f);
        }

        // Canvas at 1920x1080 for crisp text
        private void CreateCanvas()
        {
            var canvasGO = new GameObject("FeedbackCanvas");
            canvasGO.transform.SetParent(transform);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 110;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
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
            _milestoneText.MainText.fontSize = 48;
            _milestoneText.Root.gameObject.SetActive(false);
        }

        private JudgmentTextInstance CreateTextInstance(string name)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(_canvasRT, false);
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(300, 100);

            var mainGO = new GameObject("Main", typeof(RectTransform), typeof(Text));
            mainGO.transform.SetParent(root.transform, false);
            var mainRT = mainGO.GetComponent<RectTransform>();
            mainRT.anchorMin = mainRT.anchorMax = mainRT.pivot = new Vector2(0.5f, 0.5f);
            mainRT.anchoredPosition = Vector2.zero;
            mainRT.sizeDelta = new Vector2(300, 60);
            var mainText = mainGO.GetComponent<Text>();
            mainText.font = UIHelpers.GetDefaultFont(36);
            mainText.fontSize = 36;
            mainText.fontStyle = FontStyle.Bold;
            mainText.alignment = TextAnchor.MiddleCenter;
            mainText.color = Color.white;
            mainText.horizontalOverflow = HorizontalWrapMode.Overflow;
            mainText.verticalOverflow = VerticalWrapMode.Overflow;

            var subGO = new GameObject("Sub", typeof(RectTransform), typeof(Text));
            subGO.transform.SetParent(root.transform, false);
            var subRT = subGO.GetComponent<RectTransform>();
            subRT.anchorMin = subRT.anchorMax = subRT.pivot = new Vector2(0.5f, 0.5f);
            subRT.anchoredPosition = new Vector2(0, -35);
            subRT.sizeDelta = new Vector2(200, 40);
            var subText = subGO.GetComponent<Text>();
            subText.font = UIHelpers.GetDefaultFont(22);
            subText.fontSize = 22;
            subText.alignment = TextAnchor.MiddleCenter;
            subText.color = new Color(1, 1, 1, 0.6f);
            subText.horizontalOverflow = HorizontalWrapMode.Overflow;
            subText.verticalOverflow = VerticalWrapMode.Overflow;

            return new JudgmentTextInstance { Root = rt, MainText = mainText, SubText = subText, Timer = 0f };
        }

        private JudgmentTextInstance GetNextText()
        {
            var inst = _textPool[_nextTextIndex];
            _nextTextIndex = (_nextTextIndex + 1) % _textPool.Count;
            return inst;
        }

        private class JudgmentTextInstance
        {
            public RectTransform Root;
            public Text MainText;
            public Text SubText;
            public float Timer;
        }
    }
}