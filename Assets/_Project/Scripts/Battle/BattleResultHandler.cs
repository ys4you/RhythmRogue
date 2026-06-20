using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Core.Audio;
using RhythmRogue.Data;
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
                // Boss-ness is decided by the NODE TYPE, which the map generator sets
                // authoritatively (NodeType.Boss). Do NOT infer it from EnemyData.IsBoss
                // (maxHP >= 250): that fails whenever the boss asset's HP is under 250 or
                // its EnemyData is null, which silently routes the boss win to the reward
                // screen and never grants victory. The EnemyData.IsBoss check stays only as
                // a secondary fallback for safety.
                bool wasBoss = _runState.SelectedNode?.Type == NodeType.Boss
                               || (_runState.SelectedNode?.EnemyData?.IsBoss ?? false);
                bool wasElite = _runState.SelectedNode?.Type == NodeType.Elite;
                _runState.LastBattleWasElite = wasElite;

                AwardCurrency(wasBoss, wasElite, accuracy);

                // Per-node reward relic (onboarding): completing a tagged node hands the player a
                // relic. Read before CompleteSelectedNode, which clears SelectedNode.
                GrantNodeRewardRelic();

                bool practice = _runState.CurrentArea != null && _runState.CurrentArea.IsPracticeRun;

                _runState.CompleteSelectedNode();

                if (wasBoss)
                {
                    _runState.EndRun(true);
                    // The onboarding finale is a tutorial, not a scored run: skip the run-summary
                    // ("you conquered the dungeon") and drop the player at the main menu, ready to
                    // start a real run. Normal boss wins still show the summary.
                    GoTo(practice ? SceneTransitionManager.MAIN_MENU_SCENE : "SummaryScene");
                }
                // Onboarding (practice) skips the reward pick so the node reward above is the only
                // relic source; go straight back to the map.
                else if (practice) GoTo(SceneTransitionManager.MAP_SCENE);
                else GoTo("RewardScene");
            }
            else
            {
                _runState.EndRun(false);
                GoTo("SummaryScene");
            }
        }

        /// <summary>
        /// Award run currency for a battle win. Reads the encounter kind, the player's
        /// accuracy, and any CurrencyMultiplier relics, then asks EconomyService to
        /// compute the amount from the EconomyConfig (model C: flat base + capped
        /// accuracy bonus). Called before CompleteSelectedNode so SelectedNode is still
        /// valid for reading the encounter type.
        /// </summary>
        private void AwardCurrency(bool wasBoss, bool wasElite, float accuracy)
        {
            var config = _runState.Economy;
            if (config == null) return;

            var kind = wasBoss ? EconomyService.EncounterKind.Boss
                     : wasElite ? EconomyService.EncounterKind.Elite
                     : EconomyService.EncounterKind.Normal;

            // CurrencyMultiplier relics (e.g. Coin Magnet) stack multiplicatively via the aggregator.
            float currencyMult = RelicEffectAggregator.Aggregate(_runState.ActiveRelics).CurrencyMultiplier;

            int award = EconomyService.ComputeAward(config, kind, accuracy, currencyMult);
            _runState.AddCurrency(award);
        }

        private void GoTo(string scene)
        {
            var tm = SceneTransitionManager.Instance;
            if (tm != null) tm.GoTo(scene);
            else UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
        }

        /// <summary>
        /// Grant the relic tagged on the just-cleared node, if any (onboarding: the shield node
        /// hands the player their first relic). Routed through AcquireRelic so any on-pickup effect
        /// fires, exactly as a reward-screen pick would. No-op when the node has no reward relic or
        /// the player already holds it.
        /// </summary>
        private void GrantNodeRewardRelic()
        {
            RelicData relic = _runState.SelectedNode != null ? _runState.SelectedNode.RewardRelic : null;
            if (relic == null || _runState.ActiveRelics.Contains(relic)) return;
            _runState.AcquireRelic(relic, PlayerHealthAcquireContext.Default);
            GameLog.Info($"[BattleResultHandler] Node reward relic granted: {relic.relicName}");
        }
    }
}
