using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Util;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Map
{
    /// <summary>
    /// Generates a branching node map for a run, Inscryption / Die in
    /// the Dungeon style.
    /// 
    /// Layout (bottom to top):
    ///   Layer 0: 2 enemy nodes (safe, no elites)
    ///   Layer 1: 1 elite node
    ///   Layer 2: 2-3 nodes (enemy, elite, or rest)
    ///   Layer 3: 2 nodes (higher elite/rest chance)
    ///   Layer 4: 1 boss node (all paths converge)
    /// 
    /// Deterministic: same seed produces the same map.
    /// Uses ISeededRandom forked from the run seed, consistent
    /// with all other procedural systems in the project.
    /// </summary>
    public static class MapGenerator
    {
        // =================================================================
        // LAYER CONFIGURATION
        // =================================================================

        private struct LayerConfig
        {
            public int MinNodes;
            public int MaxNodes;
            public float RestChance;
            public float EliteChance;
        }

        private static readonly LayerConfig[] PrototypeLayers =
        {
            new() { MinNodes = 2, MaxNodes = 2, RestChance = 0f,  EliteChance = 0f },
            new() { MinNodes = 1, MaxNodes = 1, RestChance = 0f,  EliteChance = 1f },
            new() { MinNodes = 2, MaxNodes = 3, RestChance = 0.3f, EliteChance = 0.2f },
            new() { MinNodes = 2, MaxNodes = 2, RestChance = 0.4f, EliteChance = 0.35f },
        };

        // =================================================================
        // GENERATION
        // =================================================================

        /// <summary>
        /// Generate a complete map from a seeded random source.
        /// Same seed produces the same map.
        /// </summary>
        /// <param name="rng">
        /// Seeded random forked from the run seed via RandomDomain.Map.
        /// </param>
        /// <param name="seed">
        /// Seed display string (stored on MapData for UI/sharing).
        /// </param>
        /// <param name="slimeData">EnemyData for standard enemy nodes.</param>
        /// <param name="bossData">EnemyData for the boss node.</param>
        public static MapData Generate(ISeededRandom rng, string seed,
            EnemyData slimeData, EnemyData bossData)
        {
            var map = new MapData { Seed = seed };
            int nextId = 0;

            // --- Build layers ---
            foreach (var config in PrototypeLayers)
            {
                int nodeCount = rng.Range(config.MinNodes, config.MaxNodes + 1);
                var layer = new List<MapNode>();

                for (int col = 0; col < nodeCount; col++)
                {
                    NodeType type = PickNodeType(config, rng);

                    var node = new MapNode(nextId++, map.Layers.Count, col, type);

                    if (type == NodeType.Enemy || type == NodeType.Elite)
                        node.EnemyData = slimeData;

                    layer.Add(node);
                    map.AllNodes.Add(node);
                }

                map.Layers.Add(layer);
            }

            // --- Boss layer (always 1 node) ---
            var bossNode = new MapNode(nextId, map.Layers.Count, 0, NodeType.Boss);
            bossNode.EnemyData = bossData;

            var bossLayer = new List<MapNode> { bossNode };
            map.Layers.Add(bossLayer);
            map.AllNodes.Add(bossNode);

            // --- Connect layers ---
            for (int layerIdx = 0; layerIdx < map.Layers.Count - 1; layerIdx++)
            {
                ConnectLayers(map.Layers[layerIdx], map.Layers[layerIdx + 1], rng);
            }

            // --- Assign positions (uses a separate fork to avoid shifting main sequence) ---
            ISeededRandom jitterRng = rng.Fork("jitter");
            AssignPositions(map, jitterRng);

            // --- Set initial accessibility ---
            foreach (var node in map.Layers[0])
            {
                node.IsAccessible = true;
            }

            GameLog.Info($"[MapGenerator] Generated map from seed '{seed}': " +
                      $"{map.AllNodes.Count} nodes, {map.LayerCount} layers");

            return map;
        }

        // =================================================================
        // NODE TYPE SELECTION
        // =================================================================

        private static NodeType PickNodeType(LayerConfig config, ISeededRandom rng)
        {
            if (config.EliteChance > 0f && rng.Chance(config.EliteChance))
                return NodeType.Elite;

            if (config.RestChance > 0f && rng.Chance(config.RestChance))
                return NodeType.Rest;

            return NodeType.Enemy;
        }

        // =================================================================
        // CONNECTION LOGIC
        // =================================================================

        /// <summary>
        /// Connect two adjacent layers. Ensures:
        ///   1. Every node in the current layer connects to at least 1 node ahead
        ///   2. Every node in the next layer is reachable from at least 1 node behind
        ///   3. Connections prefer adjacent lanes (Inscryption-style branching)
        /// </summary>
        private static void ConnectLayers(List<MapNode> current, List<MapNode> next, ISeededRandom rng)
        {
            int curCount = current.Count;
            int nextCount = next.Count;

            bool[] nextReached = new bool[nextCount];

            for (int i = 0; i < curCount; i++)
            {
                int primaryCol = Mathf.Clamp(
                    Mathf.RoundToInt((float)i / (curCount - 1) * (nextCount - 1)),
                    0, nextCount - 1);

                if (curCount == 1)
                    primaryCol = rng.Range(0, nextCount);

                current[i].Connections.Add(next[primaryCol]);
                nextReached[primaryCol] = true;

                if (rng.Chance(0.4f))
                {
                    int secondaryCol = primaryCol + (rng.Chance(0.5f) ? -1 : 1);
                    secondaryCol = Mathf.Clamp(secondaryCol, 0, nextCount - 1);

                    if (secondaryCol != primaryCol &&
                        !current[i].Connections.Contains(next[secondaryCol]))
                    {
                        current[i].Connections.Add(next[secondaryCol]);
                        nextReached[secondaryCol] = true;
                    }
                }
            }

            for (int j = 0; j < nextCount; j++)
            {
                if (nextReached[j]) continue;

                int closestCur = Mathf.Clamp(
                    Mathf.RoundToInt((float)j / (nextCount - 1) * (curCount - 1)),
                    0, curCount - 1);

                if (!current[closestCur].Connections.Contains(next[j]))
                    current[closestCur].Connections.Add(next[j]);
            }
        }

        // =================================================================
        // POSITIONING
        // =================================================================

        /// <summary>
        /// Assign normalized positions (0-1) to all nodes.
        /// Adds slight jitter for an organic, non-grid look.
        /// </summary>
        private static void AssignPositions(MapData map, ISeededRandom rng)
        {
            int totalLayers = map.Layers.Count;

            for (int layerIdx = 0; layerIdx < totalLayers; layerIdx++)
            {
                var layer = map.Layers[layerIdx];
                int nodeCount = layer.Count;

                float y = (float)layerIdx / (totalLayers - 1);

                for (int col = 0; col < nodeCount; col++)
                {
                    float x;

                    if (nodeCount == 1)
                    {
                        x = 0.5f;
                    }
                    else
                    {
                        float padding = 0.15f;
                        x = Mathf.Lerp(padding, 1f - padding, (float)col / (nodeCount - 1));
                    }

                    // Small jitter for organic feel (not on boss or first layer)
                    if (layerIdx > 0 && layerIdx < totalLayers - 1)
                    {
                        x += rng.Range(-0.03f, 0.03f);
                        y += rng.Range(-0.01f, 0.01f);
                    }

                    layer[col].Position = new Vector2(
                        Mathf.Clamp01(x),
                        Mathf.Clamp01(y));
                }
            }
        }
    }
}
