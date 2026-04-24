using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Battle;
using RhythmRogue.Core;
using RhythmRogue.Data;
using RhythmRogue.Util;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Tests
{
    /// <summary>
    /// Chart Gym: a rapid prototyping controller for testing the
    /// new shape-based chart generation system.
    /// 
    /// Controls (in-game):
    ///   Space  = Play / Restart with current settings
    ///   S      = Next seed
    ///   A      = Previous seed  
    ///   1-4    = Set difficulty (0.25, 0.5, 0.75, 1.0)
    ///   +/-    = Adjust scroll speed
    ///   P      = Pause / Resume
    ///   R      = Regenerate chart (same seed, same audio)
    ///   G      = Toggle debug overlay
    /// </summary>
    public class ChartGymController : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("Audio")]
        [Tooltip("The song to test. Assign any AudioClip.")]
        [SerializeField] private AudioClip _testClip;

        [Tooltip("BPM of the test clip. Required for both paths.")]
        [SerializeField] private float _bpm = 120f;

        [Header("Chart Source")]
        [Tooltip("Hand-authored beat map. If assigned, uses this for timing. " +
                 "If null, uses RuntimeBeatAnalyzer on the audio.")]
        [SerializeField] private SongBeatMap _beatMap;

        [Header("Shape System")]
        [Tooltip("Library of lane shapes for the new assembler.")]
        [SerializeField] private ShapeLibrary _shapeLibrary;

        [Header("Highways")]
        [SerializeField] private NoteHighway _playerHighway;
        [SerializeField] private EnemyHighway _enemyHighway;

        [Header("Settings")]
        [SerializeField] private int _seed = 42;

        [Range(0f, 1f)]
        [SerializeField] private float _difficulty = 0.5f;

        [Tooltip("Audio analysis sensitivity for auto-detect path.")]
        [Range(0f, 1f)]
        [SerializeField] private float _analysisSensitivity = 0.5f;

        [Range(0.5f, 6.0f)]
        [Tooltip("Note scroll speed multiplier. Drag to adjust in real time. Persists in PlayerPrefs.")]
        [SerializeField] private float _scrollSpeed = 1.0f;

        [Header("Debug")]
        [SerializeField] private bool _showDebugOverlay = true;

        // =================================================================
        // STATE
        // =================================================================

        private Conductor _conductor;
        private BattleChart _currentChart;

        // Debug info
        private string _lastLog = "";
        private int _markerCount;
        private float _analysisTimeMs;

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Awake()
        {
            _conductor = Conductor.Instance;

            if (_conductor == null)
                GameLog.Error("[ChartGym] No Conductor found in scene!");
        }

        private void Start()
        {
            // Pull saved scroll speed so inspector slider reflects PlayerPrefs
            _scrollSpeed = ScrollSpeedSetting.Multiplier;

            if (_testClip != null && _conductor != null)
            {
                AudioSource source = _conductor.GetComponent<AudioSource>();
                if (source != null)
                    source.clip = _testClip;
            }

            GenerateAndPlay();
        }

        private void Update()
        {
            HandleInput();
        }

        /// <summary>
        /// Sync inspector slider to ScrollSpeedSetting when changed in editor.
        /// Works both in edit mode and during play mode dragging.
        /// </summary>
        private void OnValidate()
        {
            ScrollSpeedSetting.Multiplier = _scrollSpeed;
        }

        // =================================================================
        // INPUT
        // =================================================================

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Space))
                GenerateAndPlay();

            if (Input.GetKeyDown(KeyCode.S))
            {
                _seed++;
                GenerateAndPlay();
            }

            if (Input.GetKeyDown(KeyCode.A))
            {
                _seed = Mathf.Max(1, _seed - 1);
                GenerateAndPlay();
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                _difficulty = 0.25f;
                GenerateAndPlay();
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                _difficulty = 0.5f;
                GenerateAndPlay();
            }

            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                _difficulty = 0.75f;
                GenerateAndPlay();
            }

            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                _difficulty = 1.0f;
                GenerateAndPlay();
            }

            // Scroll speed adjustment
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
            {
                ScrollSpeedSetting.Increase();
                _scrollSpeed = ScrollSpeedSetting.Multiplier;
            }

            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                ScrollSpeedSetting.Decrease();
                _scrollSpeed = ScrollSpeedSetting.Multiplier;
            }

            if (Input.GetKeyDown(KeyCode.P))
            {
                if (_conductor.IsPaused)
                    _conductor.Resume();
                else if (_conductor.IsPlaying)
                    _conductor.Pause();
            }

            if (Input.GetKeyDown(KeyCode.R))
                GenerateChart();

            if (Input.GetKeyDown(KeyCode.G))
                _showDebugOverlay = !_showDebugOverlay;
        }

        // =================================================================
        // CHART GENERATION
        // =================================================================

        private void GenerateAndPlay()
        {
            if (_conductor.IsPlaying)
                _conductor.Stop();

            GenerateChart();
            LoadAndPlay();
        }

        private void GenerateChart()
        {
            if (_shapeLibrary == null || _shapeLibrary.shapes.Count == 0)
            {
                _lastLog = "ERROR: No ShapeLibrary assigned!";
                GameLog.Error("[ChartGym] No ShapeLibrary assigned.");
                return;
            }

            ISeededRandom rng = new SeededRandom(_seed);

            float startTime = Time.realtimeSinceStartup;

            List<BeatMarker> markers;
            List<SongSection> sections;
            float totalBeats;

            if (_beatMap != null)
            {
                markers = new List<BeatMarker>(_beatMap.markers);
                sections = _beatMap.sections != null
                    ? new List<SongSection>(_beatMap.sections)
                    : null;

                totalBeats = markers.Count > 0
                    ? markers[markers.Count - 1].beat + 4f
                    : 0f;

                _markerCount = markers.Count;
            }
            else if (_testClip != null)
            {
                float sensitivity = Mathf.Lerp(0.3f, 0.8f, _analysisSensitivity);
                var analysis = RuntimeBeatAnalyzer.Analyze(_testClip, _bpm, sensitivity);

                if (!analysis.Success || analysis.Markers.Count == 0)
                {
                    _lastLog = "ERROR: Audio analysis found no markers!";
                    GameLog.Error("[ChartGym] Analysis failed.");
                    return;
                }

                markers = analysis.Markers;
                sections = analysis.Sections;
                totalBeats = analysis.TotalBeats;
                _markerCount = markers.Count;
            }
            else
            {
                _lastLog = "ERROR: No AudioClip or SongBeatMap assigned!";
                return;
            }

            _currentChart = ShapeAssembler.Assemble(
                markers, sections, _shapeLibrary, rng,
                _difficulty, _bpm, totalBeats);

            _analysisTimeMs = (Time.realtimeSinceStartup - startTime) * 1000f;

            if (_currentChart != null)
            {
                _lastLog = $"Seed {_seed} | Diff {_difficulty:F2} | " +
                           $"{_currentChart.PlayerNoteCount}P + {_currentChart.EnemyNoteCount}E notes | " +
                           $"{_analysisTimeMs:F1}ms";
            }
            else
            {
                _lastLog = "ERROR: ShapeAssembler returned null!";
            }
        }

        private void LoadAndPlay()
        {
            if (_currentChart == null) return;

            if (_playerHighway != null)
            {
                _playerHighway.ClearAllNotes();
                _playerHighway.LoadNotes(_currentChart.AllPlayerNotes);
            }

            if (_enemyHighway != null)
            {
                _enemyHighway.Clear();
                _enemyHighway.LoadNotes(_currentChart.AllEnemyNotes);
            }

            _conductor.Play(_bpm, 0f);
        }

        // =================================================================
        // DEBUG OVERLAY
        // =================================================================

        private void OnGUI()
        {
            if (!_showDebugOverlay) return;

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(8, 8, 8, 8),
            };

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                richText = true,
            };

            float w = 400;
            float x = Screen.width - w - 10;
            float y = 10;

            GUI.Box(new Rect(x, y, w, 260), "CHART GYM", boxStyle);
            y += 24;

            GUI.Label(new Rect(x + 8, y, w - 16, 20), $"<b>Seed:</b> {_seed}  (S/A to change)", labelStyle);
            y += 18;

            GUI.Label(new Rect(x + 8, y, w - 16, 20), $"<b>Difficulty:</b> {_difficulty:F2}  (1-4 to set)", labelStyle);
            y += 18;

            GUI.Label(new Rect(x + 8, y, w - 16, 20), $"<b>Scroll:</b> {ScrollSpeedSetting.DisplayString}  (+/- to adjust)", labelStyle);
            y += 18;

            GUI.Label(new Rect(x + 8, y, w - 16, 20), $"<b>Source:</b> {(_beatMap != null ? "SongBeatMap" : "Auto-detect")}", labelStyle);
            y += 18;

            GUI.Label(new Rect(x + 8, y, w - 16, 20), $"<b>Markers:</b> {_markerCount}", labelStyle);
            y += 18;

            if (_currentChart != null)
            {
                GUI.Label(new Rect(x + 8, y, w - 16, 20),
                    $"<b>Player notes:</b> {_currentChart.PlayerNoteCount}  " +
                    $"<b>Enemy:</b> {_currentChart.EnemyNoteCount}", labelStyle);
                y += 18;

                GUI.Label(new Rect(x + 8, y, w - 16, 20),
                    $"<b>Sections:</b> {_currentChart.Sections.Count}  " +
                    $"<b>Shapes:</b> {_shapeLibrary.shapes.Count} in library", labelStyle);
                y += 18;
            }

            GUI.Label(new Rect(x + 8, y, w - 16, 20),
                $"<b>Gen time:</b> {_analysisTimeMs:F1}ms", labelStyle);
            y += 18;

            y += 8;
            GUI.Label(new Rect(x + 8, y, w - 16, 20), $"<color=yellow>{_lastLog}</color>", labelStyle);
            y += 24;

            GUIStyle smallStyle = new GUIStyle(labelStyle) { fontSize = 11 };
            GUI.Label(new Rect(x + 8, y, w - 16, 20), "Space=Play  S/A=Seed  1-4=Diff  +/-=Scroll  P=Pause  G=Hide", smallStyle);
        }
    }
}