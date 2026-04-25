using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Map;
using RhythmRogue.Util;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Tests
{
    /// <summary>
    /// Generates a map and logs the full structure.
    /// Verifies determinism, connectivity, and accessibility.
    /// 
    /// CONTROLS:
    ///   [Space] - Generate with random seed
    ///   [S]     - Generate same seed again (verify determinism)
    ///   [1-4]   - Select an accessible node
    /// </summary>
    public class MapTest : MonoBehaviour
    {
        [Header("Enemy Data")]
        [SerializeField] private EnemyData _slimeData;
        [SerializeField] private EnemyData _bossData;

        private MapData _map;
        private string _lastSeed;

        private void Start()
        {
            GameLog.Info("<color=white>========================================</color>");
            GameLog.Info("<color=white>  MAP GENERATOR TEST</color>");
            GameLog.Info("<color=white>========================================</color>");
            GameLog.Info("<color=cyan>  [Space] Random seed  [S] Same seed</color>");
            GameLog.Info("<color=cyan>  [1-4] Select accessible node</color>");

            GenerateRandom();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
                GenerateRandom();

            if (Input.GetKeyDown(KeyCode.S))
                GenerateSame();

            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectNode(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectNode(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectNode(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SelectNode(3);
        }

        private void GenerateRandom()
        {
            _lastSeed = System.DateTime.Now.Ticks.ToString().Substring(10);
            Generate(_lastSeed);
        }

        private void GenerateSame()
        {
            if (_lastSeed != null)
            {
                GameLog.Info($"<color=yellow>  Regenerating with same seed: {_lastSeed}</color>");
                Generate(_lastSeed);
            }
        }

        private void Generate(string seed)
        {
            ISeededRandom rng = new SeededRandom(seed.GetHashCode());
            _map = MapGenerator.Generate(rng, seed, _slimeData, _bossData);
            PrintMap();
        }

        private void SelectNode(int index)
        {
            if (_map == null) return;

            var accessible = _map.GetAccessibleNodes();

            if (index >= accessible.Count)
            {
                GameLog.Info("<color=red>  No node at that index</color>");
                return;
            }

            var node = accessible[index];
            GameLog.Info($"<color=green>  Selected: {node}</color>");

            _map.CompleteNode(node);
            PrintAccessible();

            if (node.Type == NodeType.Boss)
            {
                GameLog.Info("<color=magenta>  BOSS REACHED - run would end here!</color>");
            }
        }

        private void PrintMap()
        {
            GameLog.Info($"<color=white>  Seed: {_map.Seed}</color>");
            GameLog.Info($"<color=white>  Layers: {_map.LayerCount}, Nodes: {_map.AllNodes.Count}</color>");

            for (int i = _map.Layers.Count - 1; i >= 0; i--)
            {
                var layer = _map.Layers[i];
                string layerStr = $"  Layer {i}: ";

                foreach (var node in layer)
                {
                    string conns = "";
                    foreach (var c in node.Connections)
                        conns += $">{c.Id} ";

                    layerStr += $"[{node.Id}:{node.Type}" +
                                (node.EnemyData != null ? $"({node.EnemyData.enemyName})" : "") +
                                $" pos({node.Position.x:F2},{node.Position.y:F2})" +
                                $" {conns.Trim()}] ";
                }

                GameLog.Info($"<color=white>{layerStr}</color>");
            }

            PrintAccessible();
        }

        private void PrintAccessible()
        {
            var accessible = _map.GetAccessibleNodes();
            string accStr = "  Accessible: ";

            for (int i = 0; i < accessible.Count; i++)
            {
                accStr += $"[{i + 1}] {accessible[i]} ";
            }

            GameLog.Info($"<color=cyan>{accStr}</color>");
        }
    }
}
