using UnityEngine;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Debug overlay showing real-time judgment data.
    /// 
    /// Displays:
    ///   - Last judgment result with timing offset
    ///   - Running counts: Perfect / Good / Bad / Miss
    ///   - Accuracy percentage
    ///   - Average timing offset and early/late bias
    /// 
    /// Toggle with F2. Uses OnGUI — no Canvas needed.
    /// Complements the Conductor debug overlay (F1).
    /// </summary>
    public class JudgmentDebugOverlay : MonoBehaviour
    {
        [Header("Toggle")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.F2;

        [Header("References")]
        [SerializeField] private JudgmentSystem _judgmentSystem;
        [SerializeField] private AccuracyTracker _accuracyTracker;

        private bool _visible = true;
        private JudgmentResult _lastResult;
        private bool _hasResult;
        private float _lastResultTime;

        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;

        private void OnEnable()
        {
            if (_judgmentSystem != null)
                _judgmentSystem.OnJudgment += HandleJudgment;
        }

        private void OnDisable()
        {
            if (_judgmentSystem != null)
                _judgmentSystem.OnJudgment -= HandleJudgment;
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
                _visible = !_visible;
        }

        private void HandleJudgment(JudgmentResult result)
        {
            _lastResult = result;
            _hasResult = true;
            _lastResultTime = Time.unscaledTime;
        }

        private void OnGUI()
        {
            if (!_visible) return;

            InitStyles();

            float boxWidth = 280f;
            float boxHeight = 200f;

            // Position below the Conductor overlay
            Rect boxRect = new Rect(10, 190, boxWidth, boxHeight);
            GUI.Box(boxRect, "", _boxStyle);

            float x = 18f;
            float y = 196f;
            float lineHeight = 20f;

            // Title
            GUI.Label(new Rect(x, y, boxWidth, lineHeight),
                "JUDGMENT DEBUG [F2]", _labelStyle);
            y += lineHeight + 4f;

            // Last judgment
            if (_hasResult)
            {
                float age = Time.unscaledTime - _lastResultTime;
                float alpha = Mathf.Clamp01(1f - (age / 2f));

                string judgmentText;
                Color judgmentColor;

                switch (_lastResult.Judgment)
                {
                    case Judgment.Perfect:
                        judgmentText = "PERFECT";
                        judgmentColor = new Color(1f, 0.84f, 0f, alpha); // Gold
                        break;
                    case Judgment.Good:
                        judgmentText = "GOOD";
                        judgmentColor = new Color(0.3f, 1f, 0.3f, alpha); // Green
                        break;
                    case Judgment.Bad:
                        judgmentText = "BAD";
                        judgmentColor = new Color(1f, 0.6f, 0.2f, alpha); // Orange
                        break;
                    default:
                        judgmentText = _lastResult.IsAutoMiss ? "MISS (auto)" : "MISS";
                        judgmentColor = new Color(1f, 0.2f, 0.2f, alpha); // Red
                        break;
                }

                string offsetStr = _lastResult.IsAutoMiss
                    ? ""
                    : $" ({_lastResult.AdjustedOffsetMs:+0.0;-0.0}ms)";

                _labelStyle.normal.textColor = judgmentColor;
                GUI.Label(new Rect(x, y, boxWidth, lineHeight),
                    $"Last: {judgmentText}{offsetStr}", _labelStyle);
                _labelStyle.normal.textColor = Color.white;
            }
            else
            {
                GUI.Label(new Rect(x, y, boxWidth, lineHeight),
                    "Last: —", _labelStyle);
            }
            y += lineHeight + 2f;

            // Accuracy stats
            if (_accuracyTracker == null)
            {
                GUI.Label(new Rect(x, y, boxWidth, lineHeight),
                    "No AccuracyTracker assigned", _labelStyle);
                return;
            }

            // Counts
            _labelStyle.normal.textColor = new Color(1f, 0.84f, 0f);
            GUI.Label(new Rect(x, y, boxWidth, lineHeight),
                $"Perfect: {_accuracyTracker.PerfectCount}", _labelStyle);
            y += lineHeight;

            _labelStyle.normal.textColor = Color.green;
            GUI.Label(new Rect(x, y, boxWidth, lineHeight),
                $"Good:    {_accuracyTracker.GoodCount}", _labelStyle);
            y += lineHeight;

            _labelStyle.normal.textColor = new Color(1f, 0.6f, 0.2f);
            GUI.Label(new Rect(x, y, boxWidth, lineHeight),
                $"Bad:     {_accuracyTracker.BadCount}", _labelStyle);
            y += lineHeight;

            _labelStyle.normal.textColor = Color.red;
            GUI.Label(new Rect(x, y, boxWidth, lineHeight),
                $"Miss:    {_accuracyTracker.MissCount}", _labelStyle);
            y += lineHeight + 4f;

            // Accuracy and offset
            _labelStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y, boxWidth, lineHeight),
                $"Accuracy: {_accuracyTracker.Accuracy:P1}  " +
                $"({_accuracyTracker.TotalNotes} notes)", _labelStyle);
            y += lineHeight;

            GUI.Label(new Rect(x, y, boxWidth, lineHeight),
                $"Avg offset: {_accuracyTracker.AverageAbsOffsetMs:F1}ms  " +
                $"Bias: {_accuracyTracker.BiasDirection} " +
                $"({_accuracyTracker.AverageSignedOffsetMs:+0.0;-0.0}ms)", _labelStyle);
        }

        private void InitStyles()
        {
            if (_boxStyle != null) return;

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(2, 2, new Color(0f, 0f, 0f, 0.8f)) }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                richText = true,
                normal = { textColor = Color.white }
            };
        }

        private static Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;

            Texture2D tex = new Texture2D(width, height);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
