using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Map;

namespace RhythmRogue.Tests
{
    /// <summary>
    /// Generates a map and logs the full structure.
    /// Verifies determinism, connectivity, and accessibility.
    /// 
    /// CONTROLS:
    ///   [Space] — Generate with random seed
    ///   [S]     — Generate same seed again (verify determinism)
    ///   [1-4]   — Select an accessible node
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
            Debug.Log("<color=white>========================================</color>");
            Debug.Log("<color=white>  MAP GENERATOR TEST</color>");
            Debug.Log("<color=white>========================================</color>");
            Debug.Log("<color=cyan>  [Space] Random seed  [S] Same seed</color>");
            Debug.Log("<color=cyan>  [1-4] Select accessible node</color>");

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
                Debug.Log($"<color=yellow>  Regenerating with same seed: {_lastSeed}</color>");
                Generate(_lastSeed);
            }
        }

        private void Generate(string seed)
        {
            _map = MapGenerator.Generate(seed, _slimeData, _bossData);
            PrintMap();
        }

        private void SelectNode(int index)
        {
            if (_map == null) return;

            var accessible = _map.GetAccessibleNodes();

            if (index >= accessible.Count)
            {
                Debug.Log("<color=red>  No node at that index</color>");
                return;
            }

            var node = accessible[index];
            Debug.Log($"<color=green>  Selected: {node}</color>");

            _map.CompleteNode(node);
            PrintAccessible();

            // Check for boss reached
            if (node.Type == NodeType.Boss)
            {
                Debug.Log("<color=magenta>  BOSS REACHED — run would end here!</color>");
            }
        }

        private void PrintMap()
        {
            Debug.Log($"<color=white>  Seed: {_map.Seed}</color>");
            Debug.Log($"<color=white>  Layers: {_map.LayerCount}, Nodes: {_map.AllNodes.Count}</color>");

            for (int i = _map.Layers.Count - 1; i >= 0; i--)
            {
                var layer = _map.Layers[i];
                string layerStr = $"  Layer {i}: ";

                foreach (var node in layer)
                {
                    string conns = "";
                    foreach (var c in node.Connections)
                        conns += $"→{c.Id} ";

                    layerStr += $"[{node.Id}:{node.Type}" +
                                (node.EnemyData != null ? $"({node.EnemyData.enemyName})" : "") +
                                $" pos({node.Position.x:F2},{node.Position.y:F2})" +
                                $" {conns.Trim()}] ";
                }

                Debug.Log($"<color=white>{layerStr}</color>");
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

            Debug.Log($"<color=cyan>{accStr}</color>");
        }
    }
}
