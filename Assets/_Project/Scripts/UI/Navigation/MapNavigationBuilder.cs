using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Nav = UnityEngine.UI.Navigation;

namespace RhythmRogue.UI.Navigation
{
    /// <summary>
    /// Builds a keyboard/gamepad navigation graph for the procedural map.
    ///
    /// Map nodes are organized in layers (bottom = first, top = boss).
    /// Navigation rules:
    ///   Up:    nearest accessible node in the next layer (forward)
    ///   Down:  nearest accessible node in the previous layer (back)
    ///   Left:  previous accessible node in the same layer
    ///   Right: next accessible node in the same layer
    ///
    /// Only accessible, non-completed nodes are wired. Disabled/locked
    /// nodes are skipped — the cursor jumps to the nearest valid neighbor.
    ///
    /// Called by MapUI after building the visual node layout.
    /// Returns the first accessible selectable for UIFocusSetter.
    ///
    /// SOLID:
    /// - S: Only wires navigation. No rendering, no game logic.
    /// - O: Works with any node layout — not tied to specific layer configs.
    /// - D: Depends only on Unity Selectable, not on MapNode or MapData.
    /// </summary>
    public static class MapNavigationBuilder
    {
        /// <summary>
        /// Data pair linking a selectable to its layer and column indices.
        /// </summary>
        public struct NodeEntry
        {
            public Selectable Selectable;
            public int Layer;
            public int Column;
            public bool IsAccessible;
        }

        /// <summary>
        /// Build navigation for a set of map node selectables.
        /// Returns the first accessible selectable for default focus.
        /// </summary>
        /// <param name="nodes">All node selectables with their layer/column metadata.</param>
        /// <param name="confirmButton">
        /// Optional confirm button from the info panel. If provided,
        /// the selected node's Down link points to it, and the confirm
        /// button's Up link points back to the selected node.
        /// </param>
        public static Selectable Build(List<NodeEntry> nodes, Selectable confirmButton = null)
        {
            if (nodes == null || nodes.Count == 0) return null;

            // Group accessible nodes by layer
            var layers = new SortedDictionary<int, List<NodeEntry>>();

            foreach (var node in nodes)
            {
                if (!node.IsAccessible || node.Selectable == null) continue;
                if (!node.Selectable.IsInteractable()) continue;

                if (!layers.ContainsKey(node.Layer))
                    layers[node.Layer] = new List<NodeEntry>();

                layers[node.Layer].Add(node);
            }

            if (layers.Count == 0) return null;

            // Sort each layer by column
            foreach (var layer in layers.Values)
                layer.Sort((a, b) => a.Column.CompareTo(b.Column));

            // Get ordered layer indices
            var layerIndices = new List<int>(layers.Keys);
            Selectable firstAccessible = null;

            for (int li = 0; li < layerIndices.Count; li++)
            {
                int layerIdx = layerIndices[li];
                var layer = layers[layerIdx];

                for (int ni = 0; ni < layer.Count; ni++)
                {
                    var entry = layer[ni];
                    Selectable sel = entry.Selectable;

                    if (firstAccessible == null)
                        firstAccessible = sel;

                    Nav nav = new Nav { mode = Nav.Mode.Explicit };

                    // Left/Right within layer
                    if (ni > 0)
                        nav.selectOnLeft = layer[ni - 1].Selectable;
                    if (ni < layer.Count - 1)
                        nav.selectOnRight = layer[ni + 1].Selectable;

                    // Up: nearest node in next layer (forward in the map)
                    if (li < layerIndices.Count - 1)
                    {
                        var nextLayer = layers[layerIndices[li + 1]];
                        nav.selectOnUp = FindNearestInLayer(nextLayer, entry.Column);
                    }

                    // Down: nearest node in previous layer (backward)
                    if (li > 0)
                    {
                        var prevLayer = layers[layerIndices[li - 1]];
                        nav.selectOnDown = FindNearestInLayer(prevLayer, entry.Column);
                    }

                    sel.navigation = nav;
                }
            }

            return firstAccessible;
        }

        /// <summary>
        /// Find the closest selectable in a layer by column proximity.
        /// </summary>
        private static Selectable FindNearestInLayer(List<NodeEntry> layer, int targetColumn)
        {
            if (layer.Count == 0) return null;
            if (layer.Count == 1) return layer[0].Selectable;

            Selectable closest = null;
            int closestDist = int.MaxValue;

            foreach (var entry in layer)
            {
                int dist = Mathf.Abs(entry.Column - targetColumn);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = entry.Selectable;
                }
            }

            return closest;
        }

        /// <summary>
        /// Wire the info panel's confirm button to the currently selected
        /// map node. Call when the info panel opens.
        ///
        ///   - Node's Down → confirm button
        ///   - Confirm's Up → back to the node
        ///   - Escape closes the panel (handled by UICancelHandler)
        /// </summary>
        public static void WireInfoPanel(Selectable selectedNode, Selectable confirmButton)
        {
            if (selectedNode == null || confirmButton == null) return;

            // Add Down link from node to confirm (preserve existing Left/Right/Up)
            UINavigationHelper.AddLink(selectedNode, down: confirmButton);

            // Confirm button goes back Up to the node
            UINavigationHelper.Wire(confirmButton, up: selectedNode);
        }
    }
}