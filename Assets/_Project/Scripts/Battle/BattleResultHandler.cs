using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Map;
using RhythmRogue.Util;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Handles the battle → next scene transition.
    /// 
    /// Listens for battle completion, records results in RunState,
    /// and triggers the appropriate scene transition.
    /// 
    /// Flow:
    ///   Win (normal enemy) → complete node → reward pick → map
    ///   Win (elite enemy)  → complete node → reward pick (better options) → map
    ///   Win (boss)         → complete node → end run (victory) → summary
    ///   Lose               → end run (defeat) → summary
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
                    // Read node info before completing (which nulls SelectedNode)
                    bool wasBoss = _runState.SelectedNode?.EnemyData?.IsBoss ?? false;
                    bool wasElite = _runState.SelectedNode?.Type == NodeType.Elite;

                    _runState.LastBattleWasElite = wasElite;
                    _runState.CompleteSelectedNode();

                    if (wasBoss)
                    {
                        _runState.EndRun(true);
                        TransitionToSummary();
                    }
                    else
                    {
                        TransitionToReward();
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
                GameLog.Info($"[BattleResultHandler] No RunState. Result: {(victory ? "WIN" : "LOSS")}");
            }
        }

        private void TransitionToReward()
        {
            GameLog.Info("[BattleResultHandler] → RewardScene");

            var tm = SceneTransitionManager.Instance;
            if (tm != null)
                tm.GoTo("RewardScene");
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene("RewardScene");
        }

        private void TransitionToMap()
        {
            GameLog.Info("[BattleResultHandler] → MapScene");

            var tm = SceneTransitionManager.Instance;
            if (tm != null)
                tm.GoToMap();
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(SceneTransitionManager.MAP_SCENE);
        }

        private void TransitionToSummary()
        {
            GameLog.Info("[BattleResultHandler] → SummaryScene");

            var tm = SceneTransitionManager.Instance;
            if (tm != null)
                tm.GoToSummary();
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(SceneTransitionManager.SUMMARY_SCENE);
        }
    }
}