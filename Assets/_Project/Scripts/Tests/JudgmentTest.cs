using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;
using RhythmRogue.Battle;
using RhythmRogue.Util;

namespace RhythmRogue.Tests
{
    /// <summary>
    /// Interactive test for the full Judgment pipeline.
    /// 
    /// HOW TO USE:
    /// 1. Use your existing scene with Conductor, NoteHighway, NotePool
    /// 2. Add components: InputHandler, NoteMatcher, HoldTracker,
    ///    JudgmentSystem, AccuracyTracker, JudgmentDebugOverlay
    /// 3. Add this script — assign all references
    /// 4. Wire JudgmentSystem refs: NoteMatcher, NoteHighway
    /// 5. Wire AccuracyTracker ref: JudgmentSystem
    /// 6. Wire JudgmentDebugOverlay refs: JudgmentSystem, AccuracyTracker
    /// 7. Hit Play, Space to start, arrow keys to play
    /// 
    /// CONTROLS:
    ///   [Space]      — Play / Pause / Resume
    ///   [S]          — Stop and reset
    ///   [Arrow Keys] — Hit notes
    ///   [F1]         — Conductor debug overlay
    ///   [F2]         — Judgment debug overlay
    /// 
    /// WHAT TO VERIFY:
    ///   - Hits show colored judgment: Perfect (gold), Good (green), Bad (orange)
    ///   - Missed notes fire auto-miss with combo reset
    ///   - F2 overlay shows running counts and accuracy
    ///   - Average offset shows early/late bias
    ///   - Hold notes: start judgment + tick feedback + complete/early
    /// </summary>
    public class JudgmentTest : MonoBehaviour
    {
        [Header("Chart")]
        [SerializeField] private TextAsset _chartAsset;

        [Header("References")]
        [SerializeField] private NoteHighway _highway;
        [SerializeField] private InputHandler _inputHandler;
        [SerializeField] private NoteMatcher _matcher;
        [SerializeField] private HoldTracker _holdTracker;
        [SerializeField] private JudgmentSystem _judgmentSystem;
        [SerializeField] private AccuracyTracker _accuracyTracker;

        private Conductor _conductor;
        private LoadedChart _loadedChart;

        private void Start()
        {
            _conductor = Conductor.Instance;

            _loadedChart = ChartLoader.Load(_chartAsset);
            if (_loadedChart == null)
            {
                GameLog.Error("[JudgmentTest] Failed to load chart.");
                return;
            }

            _highway.LoadChart(_loadedChart);

            GameLog.Info("<color=white>========================================</color>");
            GameLog.Info("<color=white>  JUDGMENT TEST</color>");
            GameLog.Info($"<color=white>  Chart: {_loadedChart.SongName} — {_loadedChart.NoteCount} notes</color>");
            GameLog.Info("<color=white>========================================</color>");
            GameLog.Info("<color=cyan>  [Space] Play  [S] Stop  [F1] Conductor  [F2] Judgment</color>");
            GameLog.Info("<color=cyan>  [Arrows] Hit notes!</color>");
        }

        private void OnEnable()
        {
            // Judgment logging
            if (_judgmentSystem != null)
                _judgmentSystem.OnJudgment += HandleJudgment;

            // Hold events
            if (_holdTracker != null)
            {
                _holdTracker.OnHoldTick += HandleHoldTick;
                _holdTracker.OnHoldFinished += HandleHoldFinished;
            }

            // Receptor visual feedback
            if (_inputHandler != null)
            {
                _inputHandler.OnLanePressed += HandleReceptorPress;
                _inputHandler.OnLaneReleased += HandleReceptorRelease;
            }
        }

        private void OnDisable()
        {
            if (_judgmentSystem != null)
                _judgmentSystem.OnJudgment -= HandleJudgment;

            if (_holdTracker != null)
            {
                _holdTracker.OnHoldTick -= HandleHoldTick;
                _holdTracker.OnHoldFinished -= HandleHoldFinished;
            }

            if (_inputHandler != null)
            {
                _inputHandler.OnLanePressed -= HandleReceptorPress;
                _inputHandler.OnLaneReleased -= HandleReceptorRelease;
            }
        }

        private void Update()
        {
            HandlePlayback();
        }

        // =================================================================
        // PLAYBACK
        // =================================================================

        private void HandlePlayback()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (!_conductor.IsPlaying)
                {
                    _accuracyTracker?.Reset();
                    _conductor.Play(_loadedChart.BPM, _loadedChart.Offset);
                    GameLog.Info("<color=green>  ▶ Playing</color>");
                }
                else if (_conductor.IsPaused)
                {
                    _conductor.Resume();
                }
                else
                {
                    _conductor.Pause();
                    GameLog.Info("<color=yellow>  ⏸ Paused</color>");
                }
            }

            if (Input.GetKeyDown(KeyCode.S))
            {
                _conductor.Stop();
                _highway.ClearAllNotes();
                _holdTracker?.ClearAll();
                _highway.LoadChart(_loadedChart);
                _accuracyTracker?.Reset();
                GameLog.Info("<color=red>  ⏹ Stopped and reset</color>");
            }
        }

        // =================================================================
        // RECEPTOR FEEDBACK
        // =================================================================

        private void HandleReceptorPress(int lane) => _highway.SetReceptorPressed(lane, true);
        private void HandleReceptorRelease(int lane) => _highway.SetReceptorPressed(lane, false);

        // =================================================================
        // JUDGMENT FEEDBACK
        // =================================================================

        private void HandleJudgment(JudgmentResult result)
        {
            string color;
            string label;

            switch (result.Judgment)
            {
                case Judgment.Perfect:
                    color = "#FFD700"; label = "PERFECT"; break;
                case Judgment.Good:
                    color = "green"; label = "GOOD"; break;
                case Judgment.Bad:
                    color = "orange"; label = "BAD"; break;
                default:
                    color = "red"; label = result.IsAutoMiss ? "MISS (auto)" : "MISS"; break;
            }

            string offsetStr = result.IsAutoMiss
                ? ""
                : $" | {result.AdjustedOffsetMs:+0.0;-0.0}ms";

            GameLog.Info($"<color={color}>  {label} | Lane {result.Lane}{offsetStr}</color>");
        }

        // =================================================================
        // HOLD FEEDBACK
        // =================================================================

        private void HandleHoldTick(HoldState state)
        {
            GameLog.Info(
                $"<color=cyan>    TICK | Lane {state.Lane} | " +
                $"{state.TicksHeld}/{state.TotalTicks} ({state.Progress:P0})</color>");
        }

        private void HandleHoldFinished(HoldResult result)
        {
            string status = result.Completed ? "COMPLETE" : $"EARLY ({result.Progress:P0})";
            string color = result.Completed ? "green" : "yellow";

            GameLog.Info($"<color={color}>    HOLD {status} | Lane {result.Lane}</color>");
        }
    }
}
