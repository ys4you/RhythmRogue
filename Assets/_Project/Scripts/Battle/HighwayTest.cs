using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;
using RhythmRogue.Battle;

namespace RhythmRogue.Tests
{
    /// <summary>
    /// Interactive test for the Note Highway + Chart Loader + Conductor.
    /// 
    /// HOW TO USE:
    /// 1. Create a scene with:
    ///    - A "Conductor" GameObject with the Conductor component
    ///      and an AudioClip assigned to the AudioSource
    ///    - A "NotePool" GameObject with the NotePool component
    ///      and a NoteView prefab assigned + prewarm count of 40
    ///    - A "NoteHighway" GameObject with:
    ///      - NoteHighway component (assign the NotePool reference)
    ///      - Optionally add 4 receptor sprites as children
    ///    - A "HighwayTest" GameObject with this script
    ///      (assign the chart JSON TextAsset in the inspector)
    /// 2. Hit Play
    /// 
    /// CONTROLS:
    ///   [Space]     — Play / Pause / Resume
    ///   [S]         — Stop and reset
    ///   [Arrow Keys]— Flash receptors (visual only, no hit detection yet)
    ///   [Up/Down]   — BPM +/- 10 (hold Shift)
    ///   [F1]        — Toggle Conductor debug overlay
    /// 
    /// WHAT TO VERIFY:
    ///   - Notes scroll smoothly from top toward the hit line
    ///   - Notes arrive at the hit line exactly on their beat
    ///   - Hold notes display head, body, and tail correctly
    ///   - Notes despawn after passing the miss window
    ///   - Pausing freezes all note movement
    ///   - No jitter or stuttering at any BPM
    /// </summary>
    public class HighwayTest : MonoBehaviour
    {
        [Header("Chart")]
        [Tooltip("Drag the test chart JSON file here.")]
        [SerializeField] private TextAsset _chartAsset;

        [Header("References")]
        [SerializeField] private NoteHighway _highway;

        private Conductor _conductor;
        private LoadedChart _loadedChart;

        private void Start()
        {
            _conductor = Conductor.Instance;

            if (_chartAsset == null)
            {
                Debug.LogError("[HighwayTest] No chart assigned! Drag a chart JSON into the inspector.");
                return;
            }

            if (_highway == null)
            {
                Debug.LogError("[HighwayTest] No NoteHighway assigned!");
                return;
            }

            // Load the chart
            _loadedChart = ChartLoader.Load(_chartAsset);

            if (_loadedChart == null)
            {
                Debug.LogError("[HighwayTest] Failed to load chart.");
                return;
            }

            // Prepare the highway
            _highway.LoadChart(_loadedChart);

            Debug.Log("<color=white>========================================</color>");
            Debug.Log("<color=white>  HIGHWAY TEST</color>");
            Debug.Log($"<color=white>  Chart: {_loadedChart.SongName}</color>");
            Debug.Log($"<color=white>  Notes: {_loadedChart.NoteCount}</color>");
            Debug.Log($"<color=white>  BPM:   {_loadedChart.BPM}</color>");
            Debug.Log("<color=white>========================================</color>");
            Debug.Log("<color=cyan>  [Space] Play/Pause  [S] Stop</color>");
            Debug.Log("<color=cyan>  [Arrow Keys] Flash Receptors</color>");
            Debug.Log("<color=cyan>  [Shift+Up/Down] BPM ±10</color>");
        }

        private void Update()
        {
            HandlePlaybackInput();
            HandleReceptorInput();
        }

        private void HandlePlaybackInput()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (!_conductor.IsPlaying)
                {
                    _conductor.Play(_loadedChart.BPM, _loadedChart.Offset);
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
                _highway.ClearAllNotes();
                _highway.LoadChart(_loadedChart);
                Debug.Log("<color=red>  ⏹ Stopped and reset</color>");
            }

            // BPM changes with Shift held
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                if (Input.GetKeyDown(KeyCode.UpArrow) && _conductor.IsPlaying)
                    _conductor.SetBPM(_conductor.BPM + 10f);

                if (Input.GetKeyDown(KeyCode.DownArrow) && _conductor.IsPlaying)
                    _conductor.SetBPM(Mathf.Max(40f, _conductor.BPM - 10f));
            }
        }

        /// <summary>
        /// Flash receptors on arrow key presses.
        /// This is just visual feedback — no hit detection yet (PROTO-005).
        /// </summary>
        private void HandleReceptorInput()
        {
            // Press
            if (Input.GetKeyDown(KeyCode.LeftArrow))  _highway.SetReceptorPressed(0, true);
            if (Input.GetKeyDown(KeyCode.DownArrow) && !Input.GetKey(KeyCode.LeftShift))
                _highway.SetReceptorPressed(1, true);
            if (Input.GetKeyDown(KeyCode.UpArrow) && !Input.GetKey(KeyCode.LeftShift))
                _highway.SetReceptorPressed(2, true);
            if (Input.GetKeyDown(KeyCode.RightArrow)) _highway.SetReceptorPressed(3, true);

            // Release
            if (Input.GetKeyUp(KeyCode.LeftArrow))  _highway.SetReceptorPressed(0, false);
            if (Input.GetKeyUp(KeyCode.DownArrow))  _highway.SetReceptorPressed(1, false);
            if (Input.GetKeyUp(KeyCode.UpArrow))    _highway.SetReceptorPressed(2, false);
            if (Input.GetKeyUp(KeyCode.RightArrow)) _highway.SetReceptorPressed(3, false);
        }
    }
}
