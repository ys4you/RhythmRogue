using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Core.Audio;
using RhythmRogue.Map;
using RhythmRogue.Util;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Handles battle -> next scene transition. Records results, routes to
    /// reward (normal/elite win), summary (boss win or loss).
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
            if (_battleManager != null) _battleManager.OnBattleCompleted += HandleBattleComplete;
            if (_damagePipeline != null) _damagePipeline.OnDamageDealt += TrackScore;
        }

        private void OnDisable()
        {
            if (_battleManager != null) _battleManager.OnBattleCompleted -= HandleBattleComplete;
            if (_damagePipeline != null) _damagePipeline.OnDamageDealt -= TrackScore;
        }

        private void TrackScore(DamageResult result) { if (!result.IsPlayerDamage) _battleScore += result.Amount; }

        public void HandleBattleComplete(bool victory)
        {
            if (_handled) return;
            _handled = true;

            float accuracy = _accuracyTracker != null ? _accuracyTracker.Accuracy : 0f;
            int maxCombo = _comboSystem != null ? _comboSystem.MaxCombo : 0;

            if (_runState == null) return;

            _runState.RecordBattleResult(victory, _battleScore, accuracy, maxCombo);

            var mgr = AudioManager.Instance;
            if (mgr != null) mgr.Play(victory ? SfxId.Victory : SfxId.Defeat);

            if (victory)
            {
                bool wasBoss = _runState.SelectedNode?.EnemyData?.IsBoss ?? false;
                _runState.LastBattleWasElite = _runState.SelectedNode?.Type == NodeType.Elite;
                _runState.CompleteSelectedNode();

                if (wasBoss) { _runState.EndRun(true); GoTo("SummaryScene"); }
                else GoTo("RewardScene");
            }
            else
            {
                _runState.EndRun(false);
                GoTo("SummaryScene");
            }
        }

        private void GoTo(string scene)
        {
            var tm = SceneTransitionManager.Instance;
            if (tm != null) tm.GoTo(scene);
            else UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
        }
    }
}
