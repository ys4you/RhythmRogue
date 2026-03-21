using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Util;

namespace RhythmRogue.Map
{
    /// <summary>
    /// Generates a branching node map for a run, Inscryption / Die in
    /// the Dungeon style.
    /// 
    /// Layout (bottom to top):
    ///   Layer 0: 2 enemy nodes — the first branch choice
    ///   Layer 1: 2-3 nodes — enemy or rest
    ///   Layer 2: 2 nodes — paths narrowing toward boss
    ///   Layer 3: 1 boss node — all paths converge
    /// 
    /// Connections flow upward. Each node connects to 1-2 nodes in
    /// the next layer. Connections can cross lanes but not skip layers.
    /// Every node is reachable and every path reaches the boss.
    /// 
    /// Deterministic: same seed → same map, guaranteed.
    /// Uses System.Random seeded from the seed string's hash.
    /// 
    /// SOLID breakdown:
    /// - S: Only generates map structure. No rendering, no gameplay.
    /// - O: New node types added to NodeType enum, not this class.
    /// - L: Returns MapData usable by any consumer.
    /// - I: One method in, one MapData out.
    /// - D: Depends on EnemyData for assignment, not on UI or scenes.
    /// </summary>
    public static class MapGenerator
    {
        // =================================================================
        // LAYER CONFIGURATION
        // =================================================================

        /// <summary>
        /// Defines the structure of each layer: how many nodes and
        /// what types are allowed.
        /// </summary>
        private struct LayerConfig
        {
            public int MinNodes;
            public int MaxNodes;
            public NodeType[] AllowedTypes;
            public float RestChance; // 0-1, chance a node is Rest instead of Enemy
        }

        private static readonly LayerConfig[] PrototypeLayers =
        {
            new LayerConfig { MinNodes = 2, MaxNodes = 2, AllowedTypes = new[] { NodeType.Enemy }, RestChance = 0f },
            new LayerConfig { MinNodes = 2, MaxNodes = 3, AllowedTypes = new[] { NodeType.Enemy, NodeType.Rest }, RestChance = 0.3f },
            new LayerConfig { MinNodes = 2, MaxNodes = 2, AllowedTypes = new[] { NodeType.Enemy, NodeType.Rest }, RestChance = 0.5f },
            // Boss layer is added separately — always 1 node
        };

        // =================================================================
        // GENERATION
        // =================================================================

        /// <summary>
        /// Generate a complete map from a seed string.
        /// Same seed → same map, always.
        /// </summary>
        /// <param name="seed">Seed string (displayed to player, shareable).</param>
        /// <param name="slimeData">EnemyData for standard enemy nodes.</param>
        /// <param name="bossData">EnemyData for the boss node.</param>
        public static MapData Generate(string seed, EnemyData slimeData, EnemyData bossData)
        {
            int hash = seed.GetHashCode();
            var rng = new System.Random(hash);

            var map = new MapData { Seed = seed };
            int nextId = 0;

            // --- Build layers ---
            foreach (var config in PrototypeLayers)
            {
                int nodeCount = rng.Next(config.MinNodes, config.MaxNodes + 1);
                var layer = new List<MapNode>();

                for (int col = 0; col < nodeCount; col++)
                {
                    NodeType type = PickNodeType(config, rng);

                    var node = new MapNode(nextId++, map.Layers.Count, col, type);

                    // Assign enemy data to battle nodes
                    if (type == NodeType.Enemy)
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

            // --- Assign positions ---
            AssignPositions(map);

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

        private static NodeType PickNodeType(LayerConfig config, System.Random rng)
        {
            if (config.RestChance > 0f && rng.NextDouble() < config.RestChance)
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
        ///   3. Connections don't cross too far (adjacent lanes preferred)
        /// 
        /// This creates the branching/converging path pattern of
        /// Inscryption-style maps.
        /// </summary>
        private static void ConnectLayers(List<MapNode> current, List<MapNode> next, System.Random rng)
        {
            int curCount = current.Count;
            int nextCount = next.Count;

            // Track which next-layer nodes have at least one incoming connection
            bool[] nextReached = new bool[nextCount];

            // Step 1: Each current node connects to at least one next node
            for (int i = 0; i < curCount; i++)
            {
                // Primary connection: closest column in next layer
                int primaryCol = Mathf.Clamp(
                    Mathf.RoundToInt((float)i / (curCount - 1) * (nextCount - 1)),
                    0, nextCount - 1);

                // Handle single-node layers
                if (curCount == 1) primaryCol = rng.Next(0, nextCount);

                current[i].Connections.Add(next[primaryCol]);
                nextReached[primaryCol] = true;

                // Chance for a secondary connection to an adjacent node
                if (rng.NextDouble() < 0.4f)
                {
                    int secondaryCol = primaryCol + (rng.NextDouble() < 0.5 ? -1 : 1);
                    secondaryCol = Mathf.Clamp(secondaryCol, 0, nextCount - 1);

                    if (secondaryCol != primaryCol &&
                        !current[i].Connections.Contains(next[secondaryCol]))
                    {
                        current[i].Connections.Add(next[secondaryCol]);
                        nextReached[secondaryCol] = true;
                    }
                }
            }

            // Step 2: Ensure every next node is reachable
            for (int j = 0; j < nextCount; j++)
            {
                if (nextReached[j]) continue;

                // Find the closest current node and connect it
                int closestCur = Mathf.Clamp(
                    Mathf.RoundToInt((float)j / (nextCount - 1) * (curCount - 1)),
                    0, curCount - 1);

                current[closestCur].Connections.Add(next[j]);
                nextReached[j] = true;
            }
        }

        // =================================================================
        // POSITIONING
        // =================================================================

        /// <summary>
        /// Assign normalized positions (0-1) to all nodes.
        /// Y spreads layers bottom-to-top.
        /// X spreads nodes within a layer evenly.
        /// 
        /// Adds slight random jitter so the map doesn't look
        /// perfectly grid-aligned (more organic, Inscryption-style).
        /// </summary>
        private static void AssignPositions(MapData map)
        {
            int totalLayers = map.Layers.Count;
            var rng = new System.Random(map.Seed.GetHashCode() + 999); // Offset seed for jitter

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
                        // Spread evenly with padding on edges
                        float padding = 0.15f;
                        x = Mathf.Lerp(padding, 1f - padding, (float)col / (nodeCount - 1));
                    }

                    // Small jitter for organic feel (not on boss or first layer)
                    if (layerIdx > 0 && layerIdx < totalLayers - 1)
                    {
                        x += (float)(rng.NextDouble() * 0.06 - 0.03);
                        y += (float)(rng.NextDouble() * 0.02 - 0.01);
                    }

                    layer[col].Position = new Vector2(
                        Mathf.Clamp01(x),
                        Mathf.Clamp01(y));
                }
            }
        }
    }
}
