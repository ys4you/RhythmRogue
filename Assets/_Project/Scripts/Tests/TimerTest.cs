using UnityEngine;
using RhythmRogue.Util.Timing;

namespace RhythmRogue.Tests
{
    /// <summary>
    /// Interactive test for the Timer / Cooldown system.
    /// A bomb defusal mini-game!
    /// 
    /// HOW TO USE:
    /// 1. Create an empty scene
    /// 2. Add an empty GameObject, attach this script
    ///    (TimerService singleton auto-creates itself)
    /// 3. Hit Play and use the keyboard controls
    /// 
    /// GAME RULES:
    ///   A bomb is ticking down from 15 seconds.
    ///   You have wire cutters (2 second cooldown).
    ///   Cut wires [1-4] to defuse — but one wire speeds up the bomb!
    ///   Cut all 4 correct wires before time runs out.
    ///
    /// CONTROLS:
    ///   [Space] — Start a new bomb
    ///   [1-4]   — Cut a wire (has cooldown)
    ///   [P]     — Pause / unpause all timers
    ///   [R]     — Restart
    /// 
    /// DEMONSTRATES:
    ///   - One-shot timer (bomb countdown)
    ///   - Progress callback (countdown bar in console)
    ///   - Looping timer (ticking sound effect log)
    ///   - Cooldown (wire cutter tool)
    ///   - Global pause (freezes everything)
    ///   - Timer cancellation (defuse stops the bomb)
    /// </summary>
    public class TimerTest : MonoBehaviour
    {
        // Game state
        private bool _gameActive;
        private bool[] _wiresCut;
        private int _dangerWire; // cutting this wire speeds things up
        private int _wiresCutCount;
        private float _bombDuration = 15f;

        // Timer references
        private ITimer _bombTimer;
        private ITimer _tickTimer;
        private Cooldown _cutterCooldown;

        // For progress display
        private float _lastProgress;

        private void Start()
        {
            _wiresCut = new bool[4];
            _cutterCooldown = new Cooldown(2f);

            Debug.Log("<color=white>========================================</color>");
            Debug.Log("<color=white>  BOMB DEFUSAL — Timer/Cooldown Test</color>");
            Debug.Log("<color=white>========================================</color>");
            Debug.Log("<color=cyan>  [Space] Start Bomb</color>");
        }

        private void Update()
        {
            // Tick the cooldown manually (timers are ticked by TimerService)
            _cutterCooldown.Update(Time.deltaTime);

            if (Input.GetKeyDown(KeyCode.Space)) StartBomb();
            if (Input.GetKeyDown(KeyCode.Alpha1)) CutWire(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) CutWire(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) CutWire(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) CutWire(3);
            if (Input.GetKeyDown(KeyCode.P)) TogglePause();
            if (Input.GetKeyDown(KeyCode.R)) Restart();

            // Print progress bar periodically
            if (_gameActive && _bombTimer != null && !_bombTimer.IsPaused)
            {
                float progress = _bombTimer.Progress;
                // Update every 5%
                if (Mathf.FloorToInt(progress * 20) != Mathf.FloorToInt(_lastProgress * 20))
                {
                    _lastProgress = progress;
                    PrintBombStatus();
                }
            }
        }

        // ===================================================================
        // GAME ACTIONS
        // ===================================================================

        private void StartBomb()
        {
            if (_gameActive) return;

            _gameActive = true;
            _wiresCutCount = 0;
            _bombDuration = 15f;
            _lastProgress = 0f;

            for (int i = 0; i < 4; i++)
                _wiresCut[i] = false;

            // Randomize which wire is the danger wire
            _dangerWire = Random.Range(0, 4);

            Debug.Log("<color=red>========================================</color>");
            Debug.Log("<color=red>  BOMB ARMED! 15 seconds!</color>");
            Debug.Log($"<color=yellow>  (Hint: wire {_dangerWire + 1} is dangerous...)</color>");
            Debug.Log("<color=red>========================================</color>");
            PrintWireStatus();
            Debug.Log("<color=white>  [1-4] Cut Wire  [P] Pause  [R] Restart</color>");

            // --- ONE-SHOT TIMER: bomb countdown ---
            _bombTimer = TimerService.Lerp(_bombDuration,
                // Per-frame progress callback (we use it in Update for display)
                onTick: _ => { },
                // Completion callback: BOOM
                onCompleted: () =>
                {
                    if (_gameActive)
                    {
                        _gameActive = false;
                        _tickTimer?.Cancel();
                        Debug.Log("<color=red>========================================</color>");
                        Debug.Log("<color=red>  BOOM! The bomb exploded!</color>");
                        Debug.Log("<color=red>========================================</color>");
                        Debug.Log("<color=cyan>  [Space] Try Again</color>");
                    }
                }
            );

            // --- LOOPING TIMER: tick sound every second ---
            _tickTimer = TimerService.Every(1f, () =>
            {
                if (_gameActive)
                {
                    float remaining = _bombTimer.Remaining;
                    string urgency = remaining < 5f ? "<color=red>  TICK!</color>" : "<color=grey>  tick</color>";
                    Debug.Log($"{urgency} <color=grey>({remaining:F0}s remaining)</color>");
                }
            });

            _cutterCooldown.ForceReady();
        }

        private void CutWire(int wireIndex)
        {
            if (!_gameActive) return;

            if (_wiresCut[wireIndex])
            {
                Debug.Log($"<color=grey>  Wire {wireIndex + 1} already cut.</color>");
                return;
            }

            // --- COOLDOWN: wire cutter tool ---
            if (!_cutterCooldown.TryUse())
            {
                Debug.Log($"<color=yellow>  Cutter on cooldown! {_cutterCooldown.Remaining:F1}s</color>");
                return;
            }

            _wiresCut[wireIndex] = true;
            _wiresCutCount++;

            if (wireIndex == _dangerWire)
            {
                // Danger wire! Speed up the bomb
                Debug.Log($"<color=red>  WRONG WIRE! Bomb speeds up!</color>");

                // Cancel old bomb timer and create a faster one
                float remaining = _bombTimer.Remaining;
                _bombTimer.Cancel();

                float newDuration = remaining * 0.5f; // half the remaining time
                _bombTimer = TimerService.Lerp(newDuration,
                    onTick: _ => { },
                    onCompleted: () =>
                    {
                        if (_gameActive)
                        {
                            _gameActive = false;
                            _tickTimer?.Cancel();
                            Debug.Log("<color=red>  BOOM! The bomb exploded!</color>");
                            Debug.Log("<color=cyan>  [Space] Try Again</color>");
                        }
                    }
                );
            }
            else
            {
                Debug.Log($"<color=green>  Wire {wireIndex + 1} cut! ({_wiresCutCount}/4)</color>");
            }

            PrintWireStatus();

            // Check for defusal (need all 3 safe wires)
            int safeCuts = 0;
            for (int i = 0; i < 4; i++)
            {
                if (_wiresCut[i] && i != _dangerWire)
                    safeCuts++;
            }

            if (safeCuts >= 3)
            {
                // --- TIMER CANCELLATION: defuse stops everything ---
                _gameActive = false;
                _bombTimer.Cancel();
                _tickTimer.Cancel();

                Debug.Log("<color=green>========================================</color>");
                Debug.Log("<color=green>  DEFUSED! You saved the day!</color>");
                Debug.Log($"<color=green>  Time remaining: {_bombTimer.Remaining:F1}s</color>");
                Debug.Log("<color=green>========================================</color>");
                Debug.Log("<color=cyan>  [Space] Play Again</color>");
            }
        }

        private void TogglePause()
        {
            if (!_gameActive) return;

            var timers = TimerService.Instance.Scaled;

            if (timers.IsGloballyPaused)
            {
                // --- GLOBAL RESUME ---
                timers.ResumeAll();
                Debug.Log("<color=yellow>  RESUMED — all timers running</color>");
            }
            else
            {
                // --- GLOBAL PAUSE ---
                timers.PauseAll();
                Debug.Log("<color=yellow>  PAUSED — all timers frozen</color>");
                Debug.Log($"<color=grey>  Bomb at {_bombTimer.Remaining:F1}s | Cutter at {_cutterCooldown.Remaining:F1}s</color>");
            }
        }

        private void Restart()
        {
            _gameActive = false;
            _bombTimer?.Cancel();
            _tickTimer?.Cancel();

            var timers = TimerService.Instance.Scaled;
            if (timers.IsGloballyPaused)
                timers.ResumeAll();

            Debug.Log("<color=yellow>  --- RESET ---</color>");
            Debug.Log("<color=cyan>  [Space] Start Bomb</color>");
        }

        // ===================================================================
        // DISPLAY HELPERS
        // ===================================================================

        private void PrintBombStatus()
        {
            float remaining = _bombTimer.Remaining;
            float progress = _bombTimer.Progress;

            int barLength = 20;
            int filled = Mathf.RoundToInt((1f - progress) * barLength);
            string bar = new string('█', filled) + new string('░', barLength - filled);

            string color = remaining < 5f ? "red" : remaining < 10f ? "yellow" : "green";
            Debug.Log($"<color={color}>  [{bar}] {remaining:F1}s</color>");
        }

        private void PrintWireStatus()
        {
            string w1 = _wiresCut[0] ? (0 == _dangerWire ? "XX" : "OK") : "--";
            string w2 = _wiresCut[1] ? (1 == _dangerWire ? "XX" : "OK") : "--";
            string w3 = _wiresCut[2] ? (2 == _dangerWire ? "XX" : "OK") : "--";
            string w4 = _wiresCut[3] ? (3 == _dangerWire ? "XX" : "OK") : "--";

            Debug.Log($"<color=white>  Wires: [1:{w1}] [2:{w2}] [3:{w3}] [4:{w4}]</color>");
        }
    }
}
