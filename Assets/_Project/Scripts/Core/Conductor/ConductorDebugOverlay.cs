using RhythmRogue.Util;
using UnityEngine;

namespace RhythmRogue.Core
{
    /// <summary>
    /// Debug overlay for the Conductor.
    /// 
    /// Displays real-time timing data in the top-left corner:
    ///   - Song position (beats and seconds)
    ///   - Current BPM and sec/beat
    ///   - Playback state
    ///   - Beat event flash (visual confirmation events are firing)
    /// 
    /// Toggle with F1. Uses OnGUI — no Canvas or UI prefab needed.
    /// Remove or disable in release builds.
    /// 
    /// Also logs beat events to the console with timestamps so you
    /// can verify sync by comparing against the audio.
    /// </summary>
    public class ConductorDebugOverlay : MonoBehaviour
    {
        [Header("Toggle")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.F1;

        [Header("Display")]
        [SerializeField] private int _fontSize = 14;

        private bool _visible = true;
        private float _beatFlash;
        private float _halfBeatFlash;
        private int _lastBeatLogged = -1;

        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _beatFlashStyle;

        private void OnEnable()
        {
            var conductor = Conductor.Instance;
            if (conductor == null) return;

            conductor.OnBeat += HandleBeat;
            conductor.OnHalfBeat += HandleHalfBeat;
        }

        private void OnDisable()
        {
            var conductor = Conductor.Instance;
            if (conductor == null) return;

            conductor.OnBeat -= HandleBeat;
            conductor.OnHalfBeat -= HandleHalfBeat;
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
                _visible = !_visible;

            // Decay the beat flash indicators
            _beatFlash = Mathf.Max(0f, _beatFlash - Time.unscaledDeltaTime * 4f);
            _halfBeatFlash = Mathf.Max(0f, _halfBeatFlash - Time.unscaledDeltaTime * 6f);
        }

        private void HandleBeat(int beatNumber)
        {
            _beatFlash = 1f;

            // Log every 4th beat to avoid console spam
            if (beatNumber % 4 == 0 && beatNumber != _lastBeatLogged)
            {
                _lastBeatLogged = beatNumber;
                GameLog.Info($"[Conductor] Beat {beatNumber} | " +
                          $"DSP: {AudioSettings.dspTime:F4} | " +
                          $"Pos: {Conductor.Instance.SongPositionInSeconds:F3}s");
            }
        }

        private void HandleHalfBeat(int halfBeatNumber)
        {
            _halfBeatFlash = 1f;
        }

        private void OnGUI()
        {
            if (!_visible) return;

            InitStyles();

            var conductor = Conductor.Instance;
            if (conductor == null)
            {
                GUI.Box(new Rect(10, 10, 260, 30), "Conductor: not found", _boxStyle);
                return;
            }

            float boxWidth = 280f;
            float boxHeight = 170f;
            Rect boxRect = new Rect(10, 10, boxWidth, boxHeight);

            GUI.Box(boxRect, "", _boxStyle);

            float x = 18f;
            float y = 16f;
            float lineHeight = 20f;

            // Title
            GUI.Label(new Rect(x, y, boxWidth, lineHeight),
                "CONDUCTOR DEBUG [F1]", _labelStyle);
            y += lineHeight + 4f;

            // State
            string state = conductor.IsPlaying
                ? (conductor.IsPaused ? "<color=yellow>PAUSED</color>" : "<color=lime>PLAYING</color>")
                : "<color=grey>STOPPED</color>";
            GUI.Label(new Rect(x, y, boxWidth, lineHeight),
                $"State: {state}", _labelStyle);
            y += lineHeight;

            // BPM
            GUI.Label(new Rect(x, y, boxWidth, lineHeight),
                $"BPM: {conductor.BPM:F1}  ({conductor.SecPerBeat * 1000f:F1} ms/beat)", _labelStyle);
            y += lineHeight;

            // Beat position
            GUI.Label(new Rect(x, y, boxWidth, lineHeight),
                $"Beat: {conductor.SongPositionInBeats:F3}  (#{conductor.CurrentBeat})", _labelStyle);
            y += lineHeight;

            // Song time
            GUI.Label(new Rect(x, y, boxWidth, lineHeight),
                $"Time: {conductor.SongPositionInSeconds:F3}s", _labelStyle);
            y += lineHeight;

            // DSP time (raw)
            GUI.Label(new Rect(x, y, boxWidth, lineHeight),
                $"DSP:  {AudioSettings.dspTime:F4}", _labelStyle);
            y += lineHeight + 4f;

            // Beat flash indicators
            Color beatColor = Color.Lerp(Color.grey, Color.green, _beatFlash);
            Color halfColor = Color.Lerp(Color.grey, Color.cyan, _halfBeatFlash);

            _beatFlashStyle.normal.textColor = beatColor;
            GUI.Label(new Rect(x, y, 80, lineHeight), "● BEAT", _beatFlashStyle);

            _beatFlashStyle.normal.textColor = halfColor;
            GUI.Label(new Rect(x + 90, y, 100, lineHeight), "● HALF", _beatFlashStyle);
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
                fontSize = _fontSize,
                richText = true,
                normal = { textColor = Color.white }
            };

            _beatFlashStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = _fontSize + 2,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.grey }
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
