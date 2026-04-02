using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Nav = UnityEngine.UI.Navigation;

namespace RhythmRogue.UI.Navigation
{
    /// <summary>
    /// Static utility for wiring explicit navigation between Selectables.
    ///
    /// Unity's Automatic navigation mode guesses connections based on
    /// proximity, which breaks on procedural layouts (map nodes, dynamic
    /// button lists). Explicit mode with manual wiring is predictable
    /// and debuggable.
    ///
    /// All methods set Navigation.Mode.Explicit and wire only the
    /// directions specified — unspecified directions remain null
    /// (which keeps the cursor on the current element).
    ///
    /// SOLID: pure static utility, no state, no dependencies.
    /// </summary>
    public static class UINavigationHelper
    {
        /// <summary>
        /// Wire a vertical chain of selectables (top to bottom).
        /// Up from first → wraps to last. Down from last → wraps to first.
        ///
        ///   UINavigationHelper.WireVertical(newRunBtn, settingsBtn, quitBtn);
        /// </summary>
        public static void WireVertical(params Selectable[] items)
        {
            if (items == null || items.Length < 2) return;

            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;

                Nav nav = new Nav { mode = Nav.Mode.Explicit };

                // Up: previous item (wrap to last)
                int upIdx = (i - 1 + items.Length) % items.Length;
                nav.selectOnUp = items[upIdx];

                // Down: next item (wrap to first)
                int downIdx = (i + 1) % items.Length;
                nav.selectOnDown = items[downIdx];

                items[i].navigation = nav;
            }
        }

        /// <summary>
        /// Wire a vertical chain without wrapping.
        /// Up from first = null (stays). Down from last = null (stays).
        /// </summary>
        public static void WireVerticalNoWrap(params Selectable[] items)
        {
            if (items == null || items.Length < 2) return;

            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;

                Nav nav = new Nav { mode = Nav.Mode.Explicit };

                if (i > 0)
                    nav.selectOnUp = items[i - 1];

                if (i < items.Length - 1)
                    nav.selectOnDown = items[i + 1];

                items[i].navigation = nav;
            }
        }

        /// <summary>
        /// Wire a horizontal chain of selectables (left to right).
        /// Left from first → wraps to last. Right from last → wraps to first.
        ///
        ///   UINavigationHelper.WireHorizontal(newRunBtn, retryBtn, menuBtn);
        /// </summary>
        public static void WireHorizontal(params Selectable[] items)
        {
            if (items == null || items.Length < 2) return;

            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;

                Nav nav = new Nav { mode = Nav.Mode.Explicit };

                int leftIdx = (i - 1 + items.Length) % items.Length;
                nav.selectOnLeft = items[leftIdx];

                int rightIdx = (i + 1) % items.Length;
                nav.selectOnRight = items[rightIdx];

                items[i].navigation = nav;
            }
        }

        /// <summary>
        /// Wire a horizontal chain without wrapping.
        /// </summary>
        public static void WireHorizontalNoWrap(params Selectable[] items)
        {
            if (items == null || items.Length < 2) return;

            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;

                Nav nav = new Nav { mode = Nav.Mode.Explicit };

                if (i > 0)
                    nav.selectOnLeft = items[i - 1];

                if (i < items.Length - 1)
                    nav.selectOnRight = items[i + 1];

                items[i].navigation = nav;
            }
        }

        /// <summary>
        /// Wire a single selectable with fully explicit directions.
        /// Pass null for directions that should dead-end (cursor stays).
        /// </summary>
        public static void Wire(Selectable target,
            Selectable up = null, Selectable down = null,
            Selectable left = null, Selectable right = null)
        {
            if (target == null) return;

            Nav nav = new Nav
            {
                mode = Nav.Mode.Explicit,
                selectOnUp = up,
                selectOnDown = down,
                selectOnLeft = left,
                selectOnRight = right
            };

            target.navigation = nav;
        }

        /// <summary>
        /// Merge additional directions into an existing explicit navigation
        /// without overwriting directions already set.
        /// Useful for connecting separate chains (e.g. vertical menu ↔ horizontal footer).
        /// </summary>
        public static void AddLink(Selectable target,
            Selectable up = null, Selectable down = null,
            Selectable left = null, Selectable right = null)
        {
            if (target == null) return;

            Nav nav = target.navigation;
            nav.mode = Nav.Mode.Explicit;

            if (up != null) nav.selectOnUp = up;
            if (down != null) nav.selectOnDown = down;
            if (left != null) nav.selectOnLeft = left;
            if (right != null) nav.selectOnRight = right;

            target.navigation = nav;
        }

        /// <summary>
        /// Set a selectable to Explicit mode with no links (dead-ends everywhere).
        /// Useful as a starting point before calling AddLink.
        /// </summary>
        public static void SetExplicit(Selectable target)
        {
            if (target == null) return;
            target.navigation = new Nav { mode = Nav.Mode.Explicit };
        }

        /// <summary>
        /// Wire a dynamic list of selectables vertically, filtering out
        /// null and non-interactable entries. Returns the first valid
        /// selectable (for use as default focus).
        /// </summary>
        public static Selectable WireVerticalDynamic(IList<Selectable> items)
        {
            var valid = new List<Selectable>();

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].IsInteractable()
                    && items[i].gameObject.activeInHierarchy)
                {
                    valid.Add(items[i]);
                }
            }

            if (valid.Count == 0) return null;

            WireVerticalNoWrap(valid.ToArray());
            return valid[0];
        }

        /// <summary>
        /// Build a navigation grid from a 2D array of selectables.
        /// Rows map to Up/Down, columns to Left/Right.
        /// Null entries are skipped — navigation jumps to the nearest
        /// non-null neighbor.
        ///
        /// Designed for map node layouts where nodes form a grid-like
        /// pattern with irregular gaps.
        /// </summary>
        public static void WireGrid(Selectable[,] grid)
        {
            int rows = grid.GetLength(0);
            int cols = grid.GetLength(1);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (grid[r, c] == null) continue;

                    Nav nav = new Nav { mode = Nav.Mode.Explicit };

                    // Up: search upward in same column, then nearby columns
                    nav.selectOnUp = FindNearest(grid, r, c, -1, 0, rows, cols);

                    // Down: search downward
                    nav.selectOnDown = FindNearest(grid, r, c, 1, 0, rows, cols);

                    // Left: search left in same row
                    nav.selectOnLeft = FindNearest(grid, r, c, 0, -1, rows, cols);

                    // Right: search right in same row
                    nav.selectOnRight = FindNearest(grid, r, c, 0, 1, rows, cols);

                    grid[r, c].navigation = nav;
                }
            }
        }

        /// <summary>
        /// Search for the nearest non-null selectable in a direction.
        /// If direct neighbor is null, checks adjacent columns/rows.
        /// </summary>
        private static Selectable FindNearest(Selectable[,] grid,
            int startRow, int startCol, int rowDir, int colDir,
            int rows, int cols)
        {
            int r = startRow + rowDir;
            int c = startCol + colDir;

            // Primary direction search
            while (r >= 0 && r < rows && c >= 0 && c < cols)
            {
                if (grid[r, c] != null)
                    return grid[r, c];

                // If moving vertically, also check adjacent columns
                if (rowDir != 0)
                {
                    for (int offset = 1; offset < cols; offset++)
                    {
                        if (c + offset < cols && grid[r, c + offset] != null)
                            return grid[r, c + offset];
                        if (c - offset >= 0 && grid[r, c - offset] != null)
                            return grid[r, c - offset];
                    }
                }

                // If moving horizontally, also check adjacent rows
                if (colDir != 0)
                {
                    for (int offset = 1; offset < rows; offset++)
                    {
                        if (r + offset < rows && grid[r + offset, c] != null)
                            return grid[r + offset, c];
                        if (r - offset >= 0 && grid[r - offset, c] != null)
                            return grid[r - offset, c];
                    }
                }

                r += rowDir;
                c += colDir;
            }

            return null;
        }
    }
}