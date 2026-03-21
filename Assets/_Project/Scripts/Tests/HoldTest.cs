using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;
using RhythmRogue.Battle;
using RhythmRogue.Util;

namespace RhythmRogue.Tests
{
    /// <summary>
    /// Interactive test for Hold Note tracking.
    /// Builds on the InputTest setup with HoldTracker added.
    /// 
    /// HOW TO USE:
    /// 1. Use your existing input test scene
    /// 2. Add a "HoldTracker" component — assign InputHandler, NoteMatcher, NoteHighway
    /// 3. Replace InputTest with this script (or add alongside)
    /// 4. Hit Play, press Space, hold arrow keys through hold notes
    /// 
    /// CONTROLS:
    ///   [Space]      — Play / Pause / Resume
    ///   [S]          — Stop and reset
    ///   [Arrow Keys] — Tap notes + hold through hold notes
    /// 
    /// WHAT TO VERIFY:
    ///   - Hold start registers like a tap (gold/green/orange timing)
    ///   - Holding shows tick messages as beats pass
    ///   - Releasing early shows partial credit (no combo reset)
    ///   - Holding to the end shows COMPLETE with full ticks
    ///   - Hold body visually shrinks while held
    ///   - Re-pressing after release does NOT restart the hold
    /// </summary>
    public class HoldTest : MonoBehaviour
    {
        [Header("Chart")]
        [SerializeField] private TextAsset _chartAsset;

        [Header("References")]
        [SerializeField] private NoteHighway _highway;
        [SerializeField] private NoteMatcher _matcher;
        [SerializeField] private InputHandler _inputHandler;
        [SerializeField] private HoldTracker _holdTracker;

        private Conductor _conductor;
        private LoadedChart _loadedChart;
        private int _hitCount;

        private void Start()
        {
            _conductor = Conductor.Instance;

            _loadedChart = ChartLoader.Load(_chartAsset);

            if (_loadedChart == null)
            {
                GameLog.Error("[HoldTest] Failed to load chart.");
                return;
            }

            _highway.LoadChart(_loadedChart);

            GameLog.Info("<color=white>========================================</color>");
            GameLog.Info("<color=white>  HOLD NOTE TEST</color>");
            GameLog.Info($"<color=white>  Chart: {_loadedChart.SongName} — {_loadedChart.NoteCount} notes</color>");
            GameLog.Info("<color=white>========================================</color>");
            GameLog.Info("<color=cyan>  [Space] Play/Pause  [S] Stop</color>");
            GameLog.Info("<color=cyan>  [Arrows] Tap + Hold through hold notes!</color>");
        }

        private void OnEnable()
        {
            if (_matcher != null)
                _matcher.OnNoteHit += HandleNoteHit;

            if (_holdTracker != null)
            {
                _holdTracker.OnHoldTick += HandleHoldTick;
                _holdTracker.OnHoldFinished += HandleHoldFinished;
            }

            if (_inputHandler != null)
            {
                _inputHandler.OnLanePressed += lane => _highway.SetReceptorPressed(lane, true);
                _inputHandler.OnLaneReleased += lane => _highway.SetReceptorPressed(lane, false);
            }
        }

        private void OnDisable()
        {
            if (_matcher != null)
                _matcher.OnNoteHit -= HandleNoteHit;

            if (_holdTracker != null)
            {
                _holdTracker.OnHoldTick -= HandleHoldTick;
                _holdTracker.OnHoldFinished -= HandleHoldFinished;
            }
        }

        private void Update()
        {
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
                _holdTracker.ClearAll();
                _highway.LoadChart(_loadedChart);
                GameLog.Info("<color=red>  ⏹ Stopped and reset</color>");
            }
        }

        // =================================================================
        // TAP / HOLD START FEEDBACK
        // =================================================================

        private void HandleNoteHit(NoteMatchResult result)
        {
            _hitCount++;

            string direction = result.OffsetMs < 0 ? "EARLY" : "LATE";
            string absOffset = $"{Mathf.Abs(result.OffsetMs):F1}ms";

            float abs = Mathf.Abs(result.OffsetMs);
            string color;
            if (abs <= 35f) color = "#FFD700";
            else if (abs <= 70f) color = "green";
            else color = "orange";

            string noteType = result.Note.Data.Type == Data.NoteType.Hold ? "HOLD START" : "TAP";

            GameLog.Info(
                $"<color={color}>  {noteType} #{_hitCount} | Lane {result.Lane} | " +
                $"{direction} {absOffset} | Beat {result.Note.Data.BeatPosition:F2}</color>");
        }

        // =================================================================
        // HOLD TICK FEEDBACK
        // =================================================================

        private void HandleHoldTick(HoldState state)
        {
            GameLog.Info(
                $"<color=cyan>    HOLD TICK | Lane {state.Lane} | " +
                $"Tick {state.TicksHeld}/{state.TotalTicks} | " +
                $"Progress {state.Progress:P0}</color>");
        }

        // =================================================================
        // HOLD FINISH FEEDBACK
        // =================================================================

        private void HandleHoldFinished(HoldResult result)
        {
            if (result.Completed)
            {
                GameLog.Info(
                    $"<color=green>    HOLD COMPLETE | Lane {result.Lane} | " +
                    $"{result.TicksHeld}/{result.TotalTicks} ticks | 100%</color>");
            }
            else
            {
                GameLog.Info(
                    $"<color=yellow>    HOLD EARLY RELEASE | Lane {result.Lane} | " +
                    $"{result.TicksHeld}/{result.TotalTicks} ticks | {result.Progress:P0}</color>");
            }
        }
    }
}
