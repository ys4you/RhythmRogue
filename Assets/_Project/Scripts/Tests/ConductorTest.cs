using UnityEngine;

namespace RhythmRogue.Tests
{
    /// <summary>
    /// Interactive test for the Conductor system.
    /// 
    /// HOW TO USE:
    /// 1. Create an empty scene
    /// 2. Add an empty GameObject, name it "Conductor"
    ///    - Attach the Conductor component
    ///    - Add an AudioSource component
    ///    - Assign a song AudioClip to the AudioSource
    ///    - Uncheck "Play On Awake" on the AudioSource
    /// 3. Add another empty GameObject, name it "ConductorTest"
    ///    - Attach this script
    ///    - Attach ConductorDebugOverlay
    /// 4. Hit Play and use the keyboard controls
    /// 
    /// CONTROLS:
    ///   [Space] — Play / Pause / Resume
    ///   [S]     — Stop
    ///   [Up]    — Increase BPM by 10
    ///   [Down]  — Decrease BPM by 10
    ///   [F1]    — Toggle debug overlay
    /// 
    /// WHAT TO VERIFY:
    ///   - Beat counter in debug overlay increments steadily
    ///   - Beat/half-beat flash indicators pulse in time with the music
    ///   - After pausing and resuming, beats don't skip or stutter
    ///   - After changing BPM, the beat counter adjusts smoothly
    ///   - After 3 minutes, the beat counter is still in sync with audio
    ///   - After tabbing out and back in, sync is maintained
    /// 
    /// DEMONSTRATES:
    ///   - DSP-based timing (no drift)
    ///   - PlayScheduled for precise start
    ///   - Pause/Resume with accurate elapsed tracking
    ///   - Runtime BPM changes with drift-free snapshotting
    ///   - C# event subscriptions (OnBeat, OnHalfBeat, OnBpmChanged)
    /// </summary>
    public class ConductorTest : MonoBehaviour
    {
        [Header("Song Settings")]
        [SerializeField] private float _startingBpm = 120f;
        [SerializeField] private float _songOffset = 0f;

        [Header("Visual Metronome")]
        [SerializeField] private Color _beatColor = Color.green;
        [SerializeField] private Color _offBeatColor = new Color(0.1f, 0.1f, 0.1f);

        private Core.Conductor _conductor;
        private SpriteRenderer _metronomeRenderer;
        private float _flashDecay;

        private void Start()
        {
            _conductor = Core.Conductor.Instance;

            // Create a simple visual metronome sprite
            CreateMetronomeVisual();

            Debug.Log("<color=white>========================================</color>");
            Debug.Log("<color=white>  CONDUCTOR TEST</color>");
            Debug.Log("<color=white>========================================</color>");
            Debug.Log("<color=cyan>  [Space] Play/Pause  [S] Stop</color>");
            Debug.Log("<color=cyan>  [Up] BPM +10  [Down] BPM -10</color>");
            Debug.Log("<color=cyan>  [F1] Toggle Debug Overlay</color>");
            Debug.Log($"<color=white>  Starting BPM: {_startingBpm}</color>");
        }

        private void OnEnable()
        {
            // Defer subscription until Conductor exists
            var conductor = Core.Conductor.Instance;
            if (conductor == null) return;

            conductor.OnBeat += OnBeat;
            conductor.OnBpmChanged += OnBpmChanged;
        }

        private void OnDisable()
        {
            var conductor = Core.Conductor.Instance;
            if (conductor == null) return;

            conductor.OnBeat -= OnBeat;
            conductor.OnBpmChanged -= OnBpmChanged;
        }

        private void Update()
        {
            HandleInput();
            UpdateMetronomeVisual();
        }

        // =================================================================
        // INPUT
        // =================================================================

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (!_conductor.IsPlaying)
                {
                    _conductor.Play(_startingBpm, _songOffset);
                    Debug.Log("<color=green>  ▶ Playing</color>");
                }
                else if (_conductor.IsPaused)
                {
                    _conductor.Resume();
                    Debug.Log("<color=green>  ▶ Resumed</color>");
                }
                else
                {
                    _conductor.Pause();
                    Debug.Log("<color=yellow>  ⏸ Paused</color>");
                }
            }

            if (Input.GetKeyDown(KeyCode.S))
            {
                _conductor.Stop();
                Debug.Log("<color=red>  ⏹ Stopped</color>");
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) && _conductor.IsPlaying)
            {
                _conductor.SetBPM(_conductor.BPM + 10f);
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) && _conductor.IsPlaying)
            {
                float newBpm = Mathf.Max(40f, _conductor.BPM - 10f);
                _conductor.SetBPM(newBpm);
            }
        }

        // =================================================================
        // EVENT HANDLERS
        // =================================================================

        private void OnBeat(int beatNumber)
        {
            _flashDecay = 1f;
        }

        private void OnBpmChanged(float oldBpm, float newBpm)
        {
            Debug.Log($"<color=magenta>  BPM: {oldBpm:F0} → {newBpm:F0}</color>");
        }

        // =================================================================
        // VISUAL METRONOME
        // =================================================================

        private void CreateMetronomeVisual()
        {
            GameObject visual = new GameObject("Metronome");
            visual.transform.SetParent(transform);
            visual.transform.localPosition = Vector3.zero;

            _metronomeRenderer = visual.AddComponent<SpriteRenderer>();
            _metronomeRenderer.sprite = CreateCircleSprite();
            visual.transform.localScale = Vector3.one * 2f;
        }

        private void UpdateMetronomeVisual()
        {
            if (_metronomeRenderer == null) return;

            _flashDecay = Mathf.Max(0f, _flashDecay - Time.unscaledDeltaTime * 4f);
            _metronomeRenderer.color = Color.Lerp(_offBeatColor, _beatColor, _flashDecay);
        }

        /// <summary>
        /// Create a simple circle sprite at runtime for the metronome.
        /// No asset dependency — just a white circle texture.
        /// </summary>
        private static Sprite CreateCircleSprite()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };

            float center = size / 2f;
            float radius = center - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    bool inside = (dx * dx + dy * dy) <= (radius * radius);
                    tex.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
        }
    }
}
