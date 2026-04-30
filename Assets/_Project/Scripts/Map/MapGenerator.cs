using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Util;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Map
{
    /// <summary>
    /// Generates a branching node map. Deterministic via ISeededRandom.
    /// Layout: Layer 0 (2 enemies) -> Layer 1 (elite) -> Layers 2-3 (mixed) -> Boss.
    /// </summary>
    public static class MapGenerator
    {
        private struct LayerConfig
        {
            public int MinNodes, MaxNodes;
            public float RestChance, EliteChance;
        }

        private static readonly LayerConfig[] PrototypeLayers =
        {
            new() { MinNodes = 2, MaxNodes = 2, RestChance = 0f,  EliteChance = 0f },
            new() { MinNodes = 1, MaxNodes = 1, RestChance = 0f,  EliteChance = 1f },
            new() { MinNodes = 2, MaxNodes = 3, RestChance = 0.3f, EliteChance = 0.2f },
            new() { MinNodes = 2, MaxNodes = 2, RestChance = 0.4f, EliteChance = 0.35f },
        };

        public static MapData Generate(ISeededRandom rng, string seed, EnemyData slimeData, EnemyData bossData)
        {
            var map = new MapData { Seed = seed };
            int nextId = 0;

            foreach (var config in PrototypeLayers)
            {
                int nodeCount = rng.Range(config.MinNodes, config.MaxNodes + 1);
                var layer = new List<MapNode>();

                for (int col = 0; col < nodeCount; col++)
                {
                    NodeType type = PickNodeType(config, rng);
                    var node = new MapNode(nextId++, map.Layers.Count, col, type);
                    if (type == NodeType.Enemy || type == NodeType.Elite) node.EnemyData = slimeData;
                    layer.Add(node);
                    map.AllNodes.Add(node);
                }
                map.Layers.Add(layer);
            }

            var bossNode = new MapNode(nextId, map.Layers.Count, 0, NodeType.Boss) { EnemyData = bossData };
            map.Layers.Add(new List<MapNode> { bossNode });
            map.AllNodes.Add(bossNode);

            for (int i = 0; i < map.Layers.Count - 1; i++)
                ConnectLayers(map.Layers[i], map.Layers[i + 1], rng);

            AssignPositions(map, rng.Fork("jitter"));

            foreach (var node in map.Layers[0]) node.IsAccessible = true;

            GameLog.Info($"[MapGenerator] Generated map: {map.AllNodes.Count} nodes, {map.LayerCount} layers");
            return map;
        }

        private static NodeType PickNodeType(LayerConfig config, ISeededRandom rng)
        {
            if (config.EliteChance > 0f && rng.Chance(config.EliteChance)) return NodeType.Elite;
            if (config.RestChance > 0f && rng.Chance(config.RestChance)) return NodeType.Rest;
            return NodeType.Enemy;
        }

        /// <summary>
        /// Guarantees: every current-layer node connects forward, every next-layer
        /// node is reachable, connections prefer adjacent columns.
        /// </summary>
        private static void ConnectLayers(List<MapNode> current, List<MapNode> next, ISeededRandom rng)
        {
            int curCount = current.Count;
            int nextCount = next.Count;
            bool[] nextReached = new bool[nextCount];

            for (int i = 0; i < curCount; i++)
            {
                int primary = curCount == 1
                    ? rng.Range(0, nextCount)
                    : Mathf.Clamp(Mathf.RoundToInt((float)i / (curCount - 1) * (nextCount - 1)), 0, nextCount - 1);

                current[i].Connections.Add(next[primary]);
                nextReached[primary] = true;

                if (rng.Chance(0.4f))
                {
                    int secondary = Mathf.Clamp(primary + (rng.Chance(0.5f) ? -1 : 1), 0, nextCount - 1);
                    if (secondary != primary && !current[i].Connections.Contains(next[secondary]))
                    {
                        current[i].Connections.Add(next[secondary]);
                        nextReached[secondary] = true;
                    }
                }
            }

            for (int j = 0; j < nextCount; j++)
            {
                if (nextReached[j]) continue;
                int closest = Mathf.Clamp(Mathf.RoundToInt((float)j / (nextCount - 1) * (curCount - 1)), 0, curCount - 1);
                if (!current[closest].Connections.Contains(next[j]))
                    current[closest].Connections.Add(next[j]);
            }
        }

        private static void AssignPositions(MapData map, ISeededRandom rng)
        {
            int totalLayers = map.Layers.Count;
            for (int layerIdx = 0; layerIdx < totalLayers; layerIdx++)
            {
                var layer = map.Layers[layerIdx];
                float y = (float)layerIdx / (totalLayers - 1);

                for (int col = 0; col < layer.Count; col++)
                {
                    float x = layer.Count == 1 ? 0.5f : Mathf.Lerp(0.15f, 0.85f, (float)col / (layer.Count - 1));

                    // Jitter for organic feel (skip first and boss layers)
                    if (layerIdx > 0 && layerIdx < totalLayers - 1)
                    {
                        x += rng.Range(-0.03f, 0.03f);
                        y += rng.Range(-0.01f, 0.01f);
                    }

                    layer[col].Position = new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
                }
            }
        }
    }
}
