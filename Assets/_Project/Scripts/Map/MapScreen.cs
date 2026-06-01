using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Core.Audio;
using RhythmRogue.Data;
using RhythmRogue.Battle;
using RhythmRogue.Util;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Map
{
    /// <summary>
    /// Scene controller for the map screen.
    /// Builds and displays a procedural map for the current Area.
    /// </summary>
    public class MapScreen : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MapUI _mapUI;
        [SerializeField] private RunState _runState;

        [Header("Content")]
        [Tooltip("Area data driving map generation. Holds enemy pools, boss pool, and difficulty knobs.")]
        [SerializeField] private Area _area;

        [Header("Test Settings")]
        [Tooltip("Force a seed for testing. Leave empty for RunState seed.")]
        [SerializeField] private string _forcedSeed = "";

        [Header("Default Chart (for battles)")]
        [SerializeField] private TextAsset _defaultChart;

        private RhythmRogue.UI.RelicBar _relicBar;

        private void Start()
        {
            var ph = PlayerHealth.Instance;

            if (_runState == null)
            {
                GameLog.Error("[MapScreen] No RunState assigned!");
                return;
            }

            if (_area == null)
            {
                GameLog.Error("[MapScreen] No Area assigned. Cannot generate map.");
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

                if (ph != null) ph.ResetForNewRun();

                ISeededRandom mapRng = _runState.RunSeed.GetRandom(RandomDomain.Map);
                _runState.MapData = MapGenerator.Generate(mapRng, _runState.Seed, _area);
            }

            _mapUI.SetRunState(_runState);
            _mapUI.BuildMap(_runState.MapData);
            _mapUI.OnNodeConfirmed += HandleNodeConfirmed;

            // Relic bar: icon strip across the top, hover for name, click for full detail.
            // Self-contained on its own canvas, so it overlays the map HUD without touching
            // MapUI's layout. Built fresh each time the map loads (it reads current relics).
            _relicBar = RhythmRogue.UI.RelicBar.Create(_runState);

            // Crossfade from whatever was playing (menu drone, or silence after a battle)
            // into the map's shamanic ambient. Idempotent across re-entries to the map.
            MusicManager.Instance.Play(MusicTrack.MapShamanic);
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

                case NodeType.Shop:
                    DoShop(node);
                    break;

                case NodeType.Event:
                    DoEvent(node);
                    break;
            }
        }

        private void StartBattle(MapNode node)
        {
            _runState.SelectedNode = node;
            _runState.SelectedChart = _defaultChart;

            GameLog.Info($"[MapScreen] Battle: {node.EnemyData?.enemyName ?? "Unknown"}");

            var tm = SceneTransitionManager.Instance;
            if (tm != null)
                tm.GoToBattle();
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(SceneTransitionManager.BATTLE_SCENE);
        }

        private void DoRest(MapNode node)
        {
            _runState.SelectedNode = node;

            GameLog.Info("[MapScreen] RestScene");

            var tm = SceneTransitionManager.Instance;
            if (tm != null)
                tm.GoTo("RestScene");
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene("RestScene");
        }

        private void DoShop(MapNode node)
        {
            _runState.SelectedNode = node;

            GameLog.Info("[MapScreen] ShopScene");

            var tm = SceneTransitionManager.Instance;
            if (tm != null)
                tm.GoToShop();
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(SceneTransitionManager.SHOP_SCENE);
        }

        private void DoEvent(MapNode node)
        {
            _runState.SelectedNode = node;

            GameLog.Info("[MapScreen] EventScene");

            var tm = SceneTransitionManager.Instance;
            if (tm != null)
                tm.GoToEvent();
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(SceneTransitionManager.EVENT_SCENE);
        }
    }
}
