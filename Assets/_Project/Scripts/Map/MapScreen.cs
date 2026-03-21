using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;
using RhythmRogue.Battle;
using RhythmRogue.Util;

namespace RhythmRogue.Map
{
    /// <summary>
    /// Scene controller for the map screen.
    /// 
    /// On Start:
    ///   - If RunState has a map, rebuild UI from it (returning from battle)
    ///   - If no map, generate a new one (new run)
    /// 
    /// On node confirm:
    ///   - Enemy/Boss: stores node + chart in RunState, transitions to BattleScene
    ///   - Rest: transitions to RestScene
    /// 
    /// No longer sets BattleConfig — all cross-scene data lives in RunState.
    /// </summary>
    public class MapScreen : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MapUI _mapUI;
        [SerializeField] private RunState _runState;

        [Header("Enemy Data (for generation)")]
        [SerializeField] private EnemyData _slimeData;
        [SerializeField] private EnemyData _bossData;

        [Header("Test Settings")]
        [Tooltip("Force a seed for testing. Leave empty for RunState seed.")]
        [SerializeField] private string _forcedSeed = "";

        [Header("Default Chart (for battles)")]
        [SerializeField] private TextAsset _defaultChart;

        private void Start()
        {
            var ph = PlayerHealth.Instance;

            if (_runState == null)
            {
                GameLog.Error("[MapScreen] No RunState assigned!");
                return;
            }

            if (!_runState.IsRunActive || _runState.MapData == null)
            {
                string seed = !string.IsNullOrWhiteSpace(_forcedSeed)
                    ? _forcedSeed
                    : _runState.Seed;

                if (string.IsNullOrWhiteSpace(seed))
                    _runState.StartNewRun();
                else
                    _runState.StartNewRun(seed);

                ph.ResetForNewRun();

                _runState.MapData = MapGenerator.Generate(_runState.Seed, _slimeData, _bossData);
            }

            _mapUI.BuildMap(_runState.MapData);
            _mapUI.OnNodeConfirmed += HandleNodeConfirmed;
        }

        private void OnDestroy()
        {
            if (_mapUI != null)
                _mapUI.OnNodeConfirmed -= HandleNodeConfirmed;
        }

        private void HandleNodeConfirmed(MapNode node)
        {
            if (SceneTransitionManager.Instance != null &&
                SceneTransitionManager.Instance.IsTransitioning)
                return;

            GameLog.Info($"[MapScreen] Node confirmed: {node}");

            switch (node.Type)
            {
                case NodeType.Enemy:
                case NodeType.Elite:
                case NodeType.Boss:
                    StartBattle(node);
                    break;

                case NodeType.Rest:
                    DoRest(node);
                    break;

                case NodeType.Event:
                case NodeType.Shop:
                    GameLog.Info($"[MapScreen] {node.Type} not implemented — skipping.");
                    _runState.SelectedNode = node;
                    _runState.CompleteSelectedNode();
                    _mapUI.UpdateVisuals();
                    break;
            }
        }

        private void StartBattle(MapNode node)
        {
            // Store everything BattleScene needs in RunState
            _runState.SelectedNode = node;
            _runState.SelectedChart = _defaultChart;

            GameLog.Info($"[MapScreen] → Battle: {node.EnemyData?.enemyName ?? "Unknown"}");

            var tm = SceneTransitionManager.Instance;
            if (tm != null)
                tm.GoToBattle();
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(SceneTransitionManager.BATTLE_SCENE);
        }

        private void DoRest(MapNode node)
        {
            _runState.SelectedNode = node;

            GameLog.Info("[MapScreen] → RestScene");

            var tm = SceneTransitionManager.Instance;
            if (tm != null)
                tm.GoTo("RestScene");
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene("RestScene");
        }
    }
}