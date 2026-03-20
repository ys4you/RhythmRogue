using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Data;

namespace RhythmRogue.Map
{
    /// <summary>
    /// A single node on the run map.
    /// 
    /// Nodes form a directed graph — each node connects forward
    /// to one or more nodes in the next layer. The player picks
    /// a path through the graph, Inscryption/Die in the Dungeon style.
    /// 
    /// Layout: layers progress bottom-to-top. Layer 0 is the start,
    /// the final layer is the boss. Paths branch and converge.
    /// </summary>
    public class MapNode
    {
        /// <summary>Unique identifier.</summary>
        public int Id { get; }

        /// <summary>Which layer this node is on (0 = first, N = boss).</summary>
        public int Layer { get; }

        /// <summary>Index within the layer (0 = leftmost).</summary>
        public int Column { get; }

        /// <summary>What happens at this node.</summary>
        public NodeType Type { get; }

        /// <summary>
        /// Normalized position for UI layout (0-1 range).
        /// X = horizontal spread within layer.
        /// Y = vertical layer position (0 = bottom, 1 = top).
        /// </summary>
        public Vector2 Position { get; set; }

        /// <summary>Nodes this connects forward to (next layer).</summary>
        public List<MapNode> Connections { get; } = new();

        /// <summary>Whether the player has completed this node.</summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// Whether the player can currently select this node.
        /// True if: not completed AND connected from a completed node
        /// (or is in the first layer).
        /// </summary>
        public bool IsAccessible { get; set; }

        /// <summary>Enemy data for battle nodes. Null for non-battle nodes.</summary>
        public EnemyData EnemyData { get; set; }

        public MapNode(int id, int layer, int column, NodeType type)
        {
            Id = id;
            Layer = layer;
            Column = column;
            Type = type;
        }

        public override string ToString()
        {
            return $"Node[{Id}] L{Layer}C{Column} {Type}" +
                   (IsCompleted ? " ✓" : "") +
                   (IsAccessible ? " ●" : "");
        }
    }
}
