using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;
using RhythmRogue.Battle;

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
    ///   - Enemy/Boss: store in RunState, transition to BattleScene
    ///   - Rest: heal inline, complete node, refresh UI
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
            // Ensure PlayerHealth exists
            var ph = PlayerHealth.Instance;

            if (_runState == null)
            {
                Debug.LogError("[MapScreen] No RunState assigned!");
                return;
            }

            // If no active run, start one
            if (!_runState.IsRunActive || _runState.MapData == null)
            {
                string seed = !string.IsNullOrWhiteSpace(_forcedSeed)
                    ? _forcedSeed
                    : _runState.Seed;

                if (string.IsNullOrWhiteSpace(seed))
                    _runState.StartNewRun();
                else
                    _runState.StartNewRun(seed);

                // Reset player HP for new run
                ph.ResetForNewRun();

                // Generate map
                _runState.MapData = MapGenerator.Generate(_runState.Seed, _slimeData, _bossData);
            }

            // Build UI from map data
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
            // Block if already transitioning
            if (SceneTransitionManager.Instance != null &&
                SceneTransitionManager.Instance.IsTransitioning)
                return;

            Debug.Log($"[MapScreen] Node confirmed: {node}");

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
                    // Stub: complete and move on
                    Debug.Log($"[MapScreen] {node.Type} not implemented — skipping.");
                    _runState.SelectedNode = node;
                    _runState.CompleteSelectedNode();
                    _mapUI.UpdateVisuals();
                    break;
            }
        }

        private void StartBattle(MapNode node)
        {
            _runState.SelectedNode = node;

            // Set BattleConfig for the battle scene to read
            BattleConfig.Enemy = node.EnemyData;
            BattleConfig.ChartAsset = _defaultChart;

            Debug.Log($"[MapScreen] → Battle: {node.EnemyData?.enemyName ?? "Unknown"}");

            // Transition to battle scene
            var tm = SceneTransitionManager.Instance;
            if (tm != null)
            {
                tm.GoToBattle();
            }
            else
            {
                // Fallback: direct load (no fade)
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    SceneTransitionManager.BATTLE_SCENE);
            }
        }

        private void DoRest(MapNode node)
        {
            _runState.SelectedNode = node;

            Debug.Log("[MapScreen] → RestScene");

            var tm = SceneTransitionManager.Instance;
            if (tm != null)
            {
                tm.GoTo("RestScene");
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("RestScene");
            }
        }
    }
}