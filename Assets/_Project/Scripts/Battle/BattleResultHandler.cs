using UnityEngine;
using RhythmRogue.Core;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Handles the battle → map/summary transition.
    /// 
    /// Sits in the battle scene alongside BattleManager.
    /// Listens for battle completion, records results in RunState,
    /// and triggers the appropriate scene transition.
    /// 
    /// Flow:
    ///   Win (normal enemy) → complete node → go to map
    ///   Win (boss)         → complete node → end run (victory) → go to summary
    ///   Lose               → end run (defeat) → go to summary
    /// </summary>
    public class BattleResultHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BattleManager _battleManager;
        [SerializeField] private RunState _runState;
        [SerializeField] private AccuracyTracker _accuracyTracker;
        [SerializeField] private ComboSystem _comboSystem;
        [SerializeField] private DamagePipeline _damagePipeline;

        private int _battleScore;
        private bool _handled;

        private void OnEnable()
        {
            if (_battleManager != null)
                _battleManager.OnBattleCompleted += HandleBattleComplete;

            if (_damagePipeline != null)
                _damagePipeline.OnDamageDealt += TrackScore;
        }

        private void OnDisable()
        {
            if (_battleManager != null)
                _battleManager.OnBattleCompleted -= HandleBattleComplete;

            if (_damagePipeline != null)
                _damagePipeline.OnDamageDealt -= TrackScore;
        }

        private void TrackScore(DamageResult result)
        {
            if (!result.IsPlayerDamage)
                _battleScore += result.Amount;
        }

        /// <summary>
        /// Called by BattleManager when the battle is fully complete
        /// (after the result overlay delay).
        /// Wire this to BattleManager.OnBattleComplete or call manually.
        /// </summary>
        public void HandleBattleComplete(bool victory)
        {
            if (_handled) return;
            _handled = true;

            float accuracy = _accuracyTracker != null ? _accuracyTracker.Accuracy : 0f;
            int maxCombo = _comboSystem != null ? _comboSystem.MaxCombo : 0;

            if (_runState != null)
            {
                _runState.RecordBattleResult(victory, _battleScore, accuracy, maxCombo);

                if (victory)
                {
                    _runState.CompleteSelectedNode();

                    // Check if this was the boss
                    bool wasBoss = BattleConfig.Enemy != null && BattleConfig.Enemy.IsBoss;

                    if (wasBoss)
                    {
                        _runState.EndRun(true);
                        TransitionToSummary();
                    }
                    else
                    {
                        TransitionToMap();
                    }
                }
                else
                {
                    _runState.EndRun(false);
                    TransitionToSummary();
                }
            }
            else
            {
                // No RunState — just log (testing battle scene standalone)
                Debug.Log($"[BattleResultHandler] No RunState. Result: {(victory ? "WIN" : "LOSS")}");
            }
        }

        private void TransitionToMap()
        {
            Debug.Log("[BattleResultHandler] → MapScene");

            var tm = SceneTransitionManager.Instance;
            if (tm != null)
            {
                tm.GoToMap();
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    SceneTransitionManager.MAP_SCENE);
            }
        }

        private void TransitionToSummary()
        {
            Debug.Log("[BattleResultHandler] → SummaryScene");

            var tm = SceneTransitionManager.Instance;
            if (tm != null)
            {
                tm.GoToSummary();
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    SceneTransitionManager.SUMMARY_SCENE);
            }
        }
    }
}