using UnityEngine;
using RhythmRogue.Core;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Animates receptor sprites with three layered effects:
    /// 
    ///   1. Beat pulse — subtle scale throb synced to the Conductor's beat.
    ///      Keeps receptors feeling alive even during silence.
    /// 
    ///   2. Press pop — receptor scales up on key press, springs back on release.
    ///      Gives immediate tactile feedback.
    /// 
    ///   3. Hit glow — brief color flash on successful hit.
    ///      Gold for Perfect, green for Good, orange for Bad.
    ///      Miss gets a red flash + slight offset shake.
    /// 
    /// Attach to the same GameObject as NoteHighway or EnemyHighway.
    /// Reads receptors from the highway's public Receptors property.
    /// 
    /// Wiring:
    ///   - HitFeedback calls TriggerGlow() on each judgment
    ///   - InputHandler calls SetPressed() on press/release
    ///   - Beat pulse is automatic (synced to Conductor)
    /// </summary>
    public class ReceptorAnimator : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("References")]
        [Tooltip("Highway to animate. If empty, auto-detects on same GameObject.")]
        [SerializeField] private HighwayBase _highway;

        [Header("Beat Pulse")]
        [SerializeField] private bool _beatPulseEnabled = true;
        [Tooltip("Additional scale added on the beat (e.g. 0.08 = 8% bigger).")]
        [SerializeField] private float _beatPulseAmount = 0.08f;
        [Tooltip("How fast the pulse returns to normal. Higher = snappier.")]
        [SerializeField] private float _beatPulseDecay = 10f;

        [Header("Press Pop")]
        [Tooltip("Scale multiplier when pressed (e.g. 1.25 = 25% bigger).")]
        [SerializeField] private float _pressScale = 1.25f;
        [Tooltip("How fast the pop springs back on release.")]
        [SerializeField] private float _pressDecay = 15f;

        [Header("Hit Glow")]
        [Tooltip("Duration of the color flash on hit.")]
        [SerializeField] private float _glowDuration = 0.12f;
        [Tooltip("Brightness multiplier during glow.")]
        [SerializeField] private float _glowBrightness = 2f;

        [Header("Miss Shake")]
        [Tooltip("Pixel offset for receptor shake on Miss.")]
        [SerializeField] private float _missShakeAmount = 1.5f;
        [Tooltip("Duration of the miss shake.")]
        [SerializeField] private float _missShakeDuration = 0.08f;

        // =================================================================
        // STATE
        // =================================================================

        private SpriteRenderer[] _receptors;
        private Color[] _baseColors;
        private Vector3[] _basePositions;
        private float[] _baseScales;
        private Conductor _conductor;

        private float[] _pressTarget;
        private float[] _glowTimer;
        private Color[] _glowColor;
        private float[] _pulseScale;
        private float[] _missShakeTimer;

        private float _lastBeat;
        private bool _initialized;

        // =================================================================
        // PUBLIC — called by HitFeedback and InputHandler
        // =================================================================

        /// <summary>
        /// Set press state for a lane. Call on key press (true) and release (false).
        /// </summary>
        public void SetPressed(int lane, bool pressed)
        {
            if (!IsValidLane(lane)) return;
            _pressTarget[lane] = pressed ? 1f : 0f;
        }

        /// <summary>
        /// Flash a receptor with a judgment-specific color.
        /// Call from HitFeedback on every judgment.
        /// </summary>
        public void TriggerGlow(int lane, Judgment judgment)
        {
            if (!IsValidLane(lane)) return;

            _glowTimer[lane] = _glowDuration;
            _glowColor[lane] = judgment switch
            {
                Judgment.Perfect => new Color(1f, 0.85f, 0f),
                Judgment.Good    => new Color(0.3f, 1f, 0.3f),
                Judgment.Bad     => new Color(1f, 0.6f, 0.2f),
                _                => new Color(1f, 0.25f, 0.25f)
            };

            if (judgment == Judgment.Miss)
                _missShakeTimer[lane] = _missShakeDuration;
        }

        /// <summary>
        /// Flash a receptor with a raw color (used by enemy highway auto-hit).
        /// </summary>
        public void TriggerGlow(int lane, Color color)
        {
            if (!IsValidLane(lane)) return;
            _glowTimer[lane] = _glowDuration;
            _glowColor[lane] = color;
        }

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Start()
        {
            _conductor = Conductor.Instance;

            if (_highway == null)
                _highway = GetComponent<HighwayBase>();

            if (_highway == null)
            {
                Debug.LogWarning("[ReceptorAnimator] No highway found.");
                return;
            }

            // Delay one frame so highway has run Awake and created receptors
            StartCoroutine(InitNextFrame());
        }

        private System.Collections.IEnumerator InitNextFrame()
        {
            yield return null;

            var src = _highway.Receptors;
            if (src == null || src.Count < 4)
            {
                Debug.LogWarning("[ReceptorAnimator] Highway has fewer than 4 receptors.");
                yield break;
            }

            _receptors = new SpriteRenderer[4];
            _baseColors = new Color[4];
            _basePositions = new Vector3[4];
            _baseScales = new float[4];
            _pressTarget = new float[4];
            _glowTimer = new float[4];
            _glowColor = new Color[4];
            _pulseScale = new float[4];
            _missShakeTimer = new float[4];

            for (int i = 0; i < 4; i++)
            {
                _receptors[i] = src[i];
                _baseColors[i] = _receptors[i].color;
                _basePositions[i] = _receptors[i].transform.localPosition;
                _baseScales[i] = _receptors[i].transform.localScale.x;
            }

            _lastBeat = -1f;
            _initialized = true;
        }

        private void LateUpdate()
        {
            if (!_initialized) return;

            float dt = Time.deltaTime;

            UpdateBeatPulse(dt);
            UpdateScale(dt);
            UpdateGlow(dt);
            UpdateMissShake(dt);
        }

        // =================================================================
        // BEAT PULSE
        // =================================================================

        private void UpdateBeatPulse(float dt)
        {
            if (!_beatPulseEnabled || _conductor == null || !_conductor.IsPlaying)
                return;

            float currentBeat = _conductor.SongPositionInBeats;

            int intBeat = Mathf.FloorToInt(currentBeat);
            if (intBeat > Mathf.FloorToInt(_lastBeat) && _lastBeat >= 0f)
            {
                for (int i = 0; i < 4; i++)
                    _pulseScale[i] = _beatPulseAmount;
            }

            _lastBeat = currentBeat;

            for (int i = 0; i < 4; i++)
            {
                if (_pulseScale[i] > 0f)
                {
                    _pulseScale[i] -= dt * _beatPulseDecay * _beatPulseAmount;
                    if (_pulseScale[i] < 0f)
                        _pulseScale[i] = 0f;
                }
            }
        }

        // =================================================================
        // COMBINED SCALE (base + pulse + press pop)
        // =================================================================

        private void UpdateScale(float dt)
        {
            for (int i = 0; i < 4; i++)
            {
                if (_receptors[i] == null) continue;

                float target;

                if (_pressTarget[i] > 0.5f)
                {
                    target = _baseScales[i] * _pressScale;
                }
                else
                {
                    target = _baseScales[i] + _pulseScale[i];
                }

                float current = _receptors[i].transform.localScale.x;
                float next = Mathf.Lerp(current, target, dt * _pressDecay);
                _receptors[i].transform.localScale = Vector3.one * next;
            }
        }

        // =================================================================
        // HIT GLOW
        // =================================================================

        private void UpdateGlow(float dt)
        {
            for (int i = 0; i < 4; i++)
            {
                if (_receptors[i] == null) continue;

                if (_glowTimer[i] > 0f)
                {
                    _glowTimer[i] -= dt;
                    float t = Mathf.Clamp01(_glowTimer[i] / _glowDuration);

                    Color bright = _glowColor[i] * _glowBrightness;
                    bright.a = 1f;
                    _receptors[i].color = Color.Lerp(_baseColors[i], bright, t);
                }
                else
                {
                    _receptors[i].color = _baseColors[i];
                }
            }
        }

        // =================================================================
        // MISS SHAKE
        // =================================================================

        private void UpdateMissShake(float dt)
        {
            for (int i = 0; i < 4; i++)
            {
                if (_receptors[i] == null) continue;

                if (_missShakeTimer[i] > 0f)
                {
                    _missShakeTimer[i] -= dt;

                    float intensity = _missShakeAmount * (_missShakeTimer[i] / _missShakeDuration);
                    float ox = Random.Range(-intensity, intensity) / 32f;
                    float oy = Random.Range(-intensity, intensity) / 32f;

                    _receptors[i].transform.localPosition = _basePositions[i] + new Vector3(ox, oy, 0f);
                }
                else
                {
                    _receptors[i].transform.localPosition = _basePositions[i];
                }
            }
        }

        // =================================================================
        // HELPERS
        // =================================================================

        private bool IsValidLane(int lane)
        {
            return _initialized && lane >= 0 && lane < 4;
        }
    }
}
