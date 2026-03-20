using System.Collections.Generic;

namespace RhythmRogue.Map
{
    /// <summary>
    /// Complete map state for a single run.
    /// 
    /// Contains the full node graph organized by layers,
    /// the player's current position, and the seed used
    /// for generation.
    /// 
    /// Layers progress bottom (0) to top (N):
    ///   Layer 0: first encounters (branching starts)
    ///   Layer 1-N: middle encounters, rest stops
    ///   Layer N: boss (all paths converge)
    /// </summary>
    public class MapData
    {
        /// <summary>All nodes in the map, flat list.</summary>
        public List<MapNode> AllNodes { get; } = new();

        /// <summary>
        /// Nodes organized by layer index.
        /// layers[0] = first layer, layers[N] = boss layer.
        /// </summary>
        public List<List<MapNode>> Layers { get; } = new();

        /// <summary>The node the player is currently at (last completed).</summary>
        public MapNode CurrentNode { get; set; }

        /// <summary>Seed string used to generate this map.</summary>
        public string Seed { get; set; }

        /// <summary>Total number of layers including boss.</summary>
        public int LayerCount => Layers.Count;

        /// <summary>The boss node (last layer, single node).</summary>
        public MapNode BossNode => Layers.Count > 0
            ? Layers[Layers.Count - 1][0]
            : null;

        /// <summary>
        /// Get all nodes the player can currently select.
        /// </summary>
        public List<MapNode> GetAccessibleNodes()
        {
            var result = new List<MapNode>();

            foreach (var node in AllNodes)
            {
                if (node.IsAccessible && !node.IsCompleted)
                    result.Add(node);
            }

            return result;
        }

        /// <summary>
        /// Mark a node as completed and update accessibility.
        /// Called after the player finishes a battle or rest.
        /// </summary>
        public void CompleteNode(MapNode node)
        {
            node.IsCompleted = true;
            node.IsAccessible = false;
            CurrentNode = node;

            // Make all forward connections accessible
            foreach (var connection in node.Connections)
            {
                if (!connection.IsCompleted)
                    connection.IsAccessible = true;
            }

            // Make all other nodes in the same layer inaccessible
            // (player chose this path, can't go back)
            foreach (var layer in Layers)
            {
                foreach (var n in layer)
                {
                    if (n.Layer == node.Layer && n.Id != node.Id)
                        n.IsAccessible = false;
                }
            }
        }
    }
}
