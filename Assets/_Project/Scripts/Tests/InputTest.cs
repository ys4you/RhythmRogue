using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;
using RhythmRogue.Battle;

namespace RhythmRogue.Tests
{
    /// <summary>
    /// Interactive test for Input + Note Matching using the new Input System.
    /// 
    /// HOW TO USE:
    /// 1. Use your existing highway test scene
    /// 2. Add an "InputHandler" component — assign the RhythmActions asset
    /// 3. Add a "NoteMatcher" component — assign InputHandler and NoteHighway
    /// 4. Add this script — assign all references
    /// 5. Remove HighwayTest (this replaces it)
    /// 6. Hit Play, press Space to start, arrow keys / gamepad to hit notes
    /// 
    /// CONTROLS:
    ///   [Space]      — Play / Pause / Resume (debug, legacy Input)
    ///   [S]          — Stop and reset (debug, legacy Input)
    ///   [Arrow Keys] — Hit notes via Input System
    ///   [D-pad]      — Hit notes via Input System (gamepad)
    ///   [Face btns]  — Hit notes via Input System (X/A/Y/B)
    ///   [F1]         — Toggle Conductor debug overlay
    /// 
    /// WHAT TO VERIFY:
    ///   - Arrow keys and gamepad both match notes
    ///   - Console shows timing offset (e.g. "+12.3ms" or "-5.1ms")
    ///   - Same note can't be hit twice
    ///   - Pressing when no note is near does nothing
    ///   - Receptors flash on press and release
    ///   - Notes that scroll past without being hit log as missed
    /// </summary>
    public class InputTest : MonoBehaviour
    {
        [Header("Chart")]
        [SerializeField] private TextAsset _chartAsset;

        [Header("References")]
        [SerializeField] private NoteHighway _highway;
        [SerializeField] private NoteMatcher _matcher;
        [SerializeField] private InputHandler _inputHandler;

        private Conductor _conductor;
        private LoadedChart _loadedChart;

        private int _hitCount;

        private void Start()
        {
            _conductor = Conductor.Instance;

            _loadedChart = ChartLoader.Load(_chartAsset);

            if (_loadedChart == null)
            {
                Debug.LogError("[InputTest] Failed to load chart.");
                return;
            }

            _highway.LoadChart(_loadedChart);

            Debug.Log("<color=white>========================================</color>");
            Debug.Log("<color=white>  INPUT + MATCHING TEST (New Input System)</color>");
            Debug.Log($"<color=white>  Chart: {_loadedChart.SongName} — {_loadedChart.NoteCount} notes</color>");
            Debug.Log("<color=white>========================================</color>");
            Debug.Log("<color=cyan>  [Space] Play/Pause  [S] Stop</color>");
            Debug.Log("<color=cyan>  [Arrows / D-pad / Face buttons] Hit notes!</color>");
        }

        private void OnEnable()
        {
            // Note match results
            if (_matcher != null)
                _matcher.OnNoteHit += HandleNoteHit;

            // Receptor visual feedback driven by InputHandler events
            if (_inputHandler != null)
            {
                _inputHandler.OnLanePressed += HandleReceptorPress;
                _inputHandler.OnLaneReleased += HandleReceptorRelease;
            }
        }

        private void OnDisable()
        {
            if (_matcher != null)
                _matcher.OnNoteHit -= HandleNoteHit;

            if (_inputHandler != null)
            {
                _inputHandler.OnLanePressed -= HandleReceptorPress;
                _inputHandler.OnLaneReleased -= HandleReceptorRelease;
            }
        }

        private void Update()
        {
            // Playback controls use legacy Input — these are debug-only,
            // not rhythm gameplay, so precision doesn't matter.
            HandlePlayback();
        }

        private void HandlePlayback()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (!_conductor.IsPlaying)
                {
                    _conductor.Play(_loadedChart.BPM, _loadedChart.Offset);
                    _hitCount = 0;
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
        }

        // =================================================================
        // RECEPTOR FEEDBACK
        // =================================================================

        private void HandleReceptorPress(int lane)
        {
            _highway.SetReceptorPressed(lane, true);
        }

        private void HandleReceptorRelease(int lane)
        {
            _highway.SetReceptorPressed(lane, false);
        }

        // =================================================================
        // NOTE HIT FEEDBACK
        // =================================================================

        private void HandleNoteHit(NoteMatchResult result)
        {
            _hitCount++;

            string direction = result.OffsetMs < 0 ? "EARLY" : "LATE";
            string absOffset = $"{Mathf.Abs(result.OffsetMs):F1}ms";

            float abs = Mathf.Abs(result.OffsetMs);
            string color;

            if (abs <= 35f) color = "#FFD700";      // Perfect — gold
            else if (abs <= 70f) color = "green";    // Good
            else color = "orange";                    // Bad

            Debug.Log(
                $"<color={color}>  HIT #{_hitCount} | Lane {result.Lane} | " +
                $"{direction} {absOffset} | Beat {result.Note.Data.BeatPosition:F2}</color>");
        }
    }
}
