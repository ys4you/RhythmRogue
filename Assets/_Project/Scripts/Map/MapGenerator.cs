using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Util;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Map
{
    /// <summary>
    /// Generates a branching node map. Deterministic via ISeededRandom.
    /// Shape: 1 start node -> opener layer (3-4 nodes) -> 3-5 mid layers (2-4 nodes) -> 1 boss.
    /// Total layer count varies between MinTotalLayers and MaxTotalLayers inclusive.
    ///
    /// Enemies are selected from the supplied Area's pools, weighted and seeded.
    /// </summary>
    public static class MapGenerator
    {
        private struct LayerConfig
        {
            public int MinNodes, MaxNodes;
            public float RestChance, EliteChance;
        }

        // Total layer count (start + opener + mid + boss). 6 means: 1 start + 1 opener + 3 mid + 1 boss.
        private const int MinTotalLayers = 6;
        private const int MaxTotalLayers = 8;

        // Start layer: a single entry node.
        private static readonly LayerConfig StartLayer =
            new() { MinNodes = 1, MaxNodes = 1, RestChance = 0f, EliteChance = 0f };

        // Opener layer (layer 2): wider 3-4 node fan-out from the start.
        // No rest, no elite this early - just basic enemies to ease the player in.
        private static readonly LayerConfig OpenerLayer =
            new() { MinNodes = 3, MaxNodes = 4, RestChance = 0f, EliteChance = 0f };

        // Mid layer (layer 3+): 2-4 nodes, mixed node types.
        // Rest and elite chances kick in here so the run gets choice and tension.
        private static readonly LayerConfig MidLayer =
            new() { MinNodes = 2, MaxNodes = 4, RestChance = 0.25f, EliteChance = 0.2f };

        /// <summary>
        /// Generate a map for the given area. Enemies are picked from area pools
        /// using a forked RNG so map structure and enemy selection are independent
        /// (changing one doesn't reshuffle the other when sharing seeds).
        /// </summary>
        public static MapData Generate(ISeededRandom rng, string seed, Area area)
        {
            if (area == null)
            {
                GameLog.Error("[MapGenerator] Area is null. Cannot generate map.");
                return new MapData { Seed = seed };
            }

            var map = new MapData { Seed = seed };
            int nextId = 0;

            // Separate RNG streams keep enemy selection stable when layer RNG changes
            ISeededRandom enemyRng = rng.Fork("enemies");

            // Decide the total length of this map: 6-8 layers inclusive.
            int totalLayers = rng.Range(MinTotalLayers, MaxTotalLayers + 1);
            int midLayerCount = totalLayers - 3; // minus start, opener, boss

            // Start layer (always 1 node, entry point)
            AddLayer(map, StartLayer, area, rng, enemyRng, ref nextId);

            // Opener layer (3-4 nodes, basic enemies only)
            AddLayer(map, OpenerLayer, area, rng, enemyRng, ref nextId);

            // Mid layers (2-4 nodes, mixed types)
            for (int i = 0; i < midLayerCount; i++)
                AddLayer(map, MidLayer, area, rng, enemyRng, ref nextId);

            // Boss layer (always 1 node, end point)
            var boss = area.bosses != null ? area.bosses.Pick(enemyRng) : null;
            if (boss == null)
                GameLog.Warn($"[MapGenerator] Area '{area.areaName}' has no boss configured.");

            var bossNode = new MapNode(nextId, map.Layers.Count, 0, NodeType.Boss) { EnemyData = boss };
            map.Layers.Add(new List<MapNode> { bossNode });
            map.AllNodes.Add(bossNode);

            // IMPORTANT: positions first, connections second.
            // ConnectLayers uses column indices and enforces a no-cross rule based on
            // sibling order, so the visual layout has to be settled before edges are drawn.
            AssignPositions(map, rng.Fork("jitter"));

            for (int i = 0; i < map.Layers.Count - 1; i++)
                ConnectLayers(map.Layers[i], map.Layers[i + 1], rng);

            foreach (var node in map.Layers[0]) node.IsAccessible = true;

            GameLog.Info($"[MapGenerator] Generated map for '{area.areaName}': {map.AllNodes.Count} nodes, {map.LayerCount} layers");
            return map;
        }

        /// <summary>
        /// Build one layer according to <paramref name="config"/> and append it to the map.
        /// Centralises the per-slot loop so the main Generate() reads as the overall shape only.
        /// </summary>
        private static void AddLayer(MapData map, LayerConfig config, Area area,
                                     ISeededRandom rng, ISeededRandom enemyRng, ref int nextId)
        {
            int nodeCount = rng.Range(config.MinNodes, config.MaxNodes + 1);
            var layer = new List<MapNode>();

            for (int col = 0; col < nodeCount; col++)
            {
                NodeType type = PickNodeType(config, rng);
                var node = new MapNode(nextId++, map.Layers.Count, col, type);
                node.EnemyData = ResolveEnemyForNode(type, area, enemyRng);
                layer.Add(node);
                map.AllNodes.Add(node);
            }

            map.Layers.Add(layer);
        }

        private static EnemyData ResolveEnemyForNode(NodeType type, Area area, ISeededRandom rng) => type switch
        {
            NodeType.Enemy => area.basicEnemies != null ? area.basicEnemies.Pick(rng) : null,
            NodeType.Elite => (area.eliteEnemies != null && !area.eliteEnemies.IsEmpty)
                                ? area.eliteEnemies.Pick(rng)
                                : (area.basicEnemies != null ? area.basicEnemies.Pick(rng) : null),
            _ => null
        };

        private static NodeType PickNodeType(LayerConfig config, ISeededRandom rng)
        {
            if (config.EliteChance > 0f && rng.Chance(config.EliteChance)) return NodeType.Elite;
            if (config.RestChance > 0f && rng.Chance(config.RestChance)) return NodeType.Rest;
            return NodeType.Enemy;
        }

        /// <summary>
        /// Connect two adjacent layers without producing crossing edges.
        ///
        /// Rules:
        ///   1. A node at column i can only connect to next-layer columns in the range
        ///      [i-1, i+1] (after normalising to the next layer's column space).
        ///   2. If node A (column i) connects to next[j], then for any node B at column
        ///      i+1 the connection must go to next[k] where k >= j. This prevents the
        ///      X-shaped crossings visible in the original output.
        ///   3. Every next-layer node ends up with at least one inbound connection.
        ///
        /// The algorithm walks current nodes left-to-right and tracks the highest
        /// next-layer column used so far. Each node picks a primary target in its local
        /// window that is >= the last used column, then optionally adds an in-range
        /// secondary that doesn't violate the rule.
        /// </summary>
        private static void ConnectLayers(List<MapNode> current, List<MapNode> next, ISeededRandom rng)
        {
            int curCount = current.Count;
            int nextCount = next.Count;
            bool[] nextReached = new bool[nextCount];

            // Special case: a 1-node layer (start, boss) is centred. Connect it to a small
            // group of central next-layer nodes rather than fanning to all of them.
            // For a 3-4 wide next layer this picks the middle 2 nodes; the rest are
            // wired by the orphan-cleanup pass below.
            if (curCount == 1)
            {
                int center = nextCount / 2;
                int primary = nextCount % 2 == 0 ? center - 1 : center;
                current[0].Connections.Add(next[primary]);
                nextReached[primary] = true;

                if (nextCount >= 2 && primary + 1 < nextCount)
                {
                    current[0].Connections.Add(next[primary + 1]);
                    nextReached[primary + 1] = true;
                }
            }
            else
            {
                // Map current column i to its "natural" column on the next layer.
                // With curCount=3, nextCount=4 this maps 0->0, 1->2, 2->3.
                // The mapping is monotonic, which is what keeps edges from crossing.
                int lastPrimary = -1;
                for (int i = 0; i < curCount; i++)
                {
                    int natural = Mathf.RoundToInt((float)i / (curCount - 1) * (nextCount - 1));
                    // Stay monotonic: never pick a column earlier than the previous node's primary.
                    int primary = Mathf.Max(natural, lastPrimary);
                    primary = Mathf.Clamp(primary, 0, nextCount - 1);

                    current[i].Connections.Add(next[primary]);
                    nextReached[primary] = true;
                    lastPrimary = primary;

                    // Optional secondary: only allowed within +/-1 of primary AND only
                    // if it doesn't cross the next node's primary range.
                    if (rng.Chance(0.35f))
                    {
                        bool canBranchRight = primary + 1 < nextCount;
                        bool canBranchLeft = primary - 1 >= 0
                            && primary - 1 >= GetPrevPrimary(current, i);

                        int secondary = -1;
                        if (canBranchRight && canBranchLeft)
                            secondary = primary + (rng.Chance(0.5f) ? 1 : -1);
                        else if (canBranchRight)
                            secondary = primary + 1;
                        else if (canBranchLeft)
                            secondary = primary - 1;

                        if (secondary >= 0 && !current[i].Connections.Contains(next[secondary]))
                        {
                            current[i].Connections.Add(next[secondary]);
                            nextReached[secondary] = true;
                            if (secondary > lastPrimary) lastPrimary = secondary;
                        }
                    }
                }
            }

            // Orphan pass: any unreached next-layer node gets wired to the nearest current
            // node (by column ratio). Since the primary pass is monotonic and the orphan's
            // pick uses the same ratio mapping, this can't introduce crossings.
            for (int j = 0; j < nextCount; j++)
            {
                if (nextReached[j]) continue;
                int closest = curCount == 1
                    ? 0
                    : Mathf.Clamp(Mathf.RoundToInt((float)j / (nextCount - 1) * (curCount - 1)), 0, curCount - 1);
                if (!current[closest].Connections.Contains(next[j]))
                    current[closest].Connections.Add(next[j]);
            }
        }

        /// <summary>
        /// Helper for ConnectLayers: returns the smallest primary column used by
        /// any earlier current-layer node. Used to enforce monotonicity for secondary edges.
        /// </summary>
        private static int GetPrevPrimary(List<MapNode> current, int upToIndex)
        {
            if (upToIndex == 0) return -1;
            // The primary is always the first connection added in the main loop,
            // so the earliest connection of the previous node tells us its primary column.
            var prev = current[upToIndex - 1];
            if (prev.Connections.Count == 0) return -1;
            return prev.Connections[0].Column;
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
                    float x = layer.Count == 1 ? 0.5f : Mathf.Lerp(0.2f, 0.8f, (float)col / (layer.Count - 1));

                    // Reduced jitter: enough to feel hand-placed, not enough to obscure the
                    // monotonic column order that ConnectLayers relies on for non-crossing edges.
                    if (layerIdx > 0 && layerIdx < totalLayers - 1)
                    {
                        x += rng.Range(-0.015f, 0.015f);
                        y += rng.Range(-0.005f, 0.005f);
                    }

                    layer[col].Position = new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
                }
            }
        }
    }
}
