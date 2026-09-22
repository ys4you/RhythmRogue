using UnityEngine;
using RhythmRogue.UI;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Builds a dark focus panel behind a highway's lanes so the notes read clearly against the
    /// scene, a lane "track" like the darkened note columns in osu!mania or Fortnite Festival. The
    /// panel's width comes from the highway's own receptor lane positions and its height from the
    /// battle camera, both read at runtime, so it fits whatever highway it is attached to with no
    /// manual sizing. Sorted just behind the performers and well behind the receptors and notes, so
    /// nothing on the lanes is ever covered. Purely visual, built in code to match the project's
    /// no-prefab convention.
    /// </summary>
    public static class LaneBackdropFactory
    {
        // Default look: the darkest palette tone, semi-transparent so it reads as a calm track over
        // whatever sits behind it without becoming a hard black box. Low-contrast on purpose, per
        // the GDD's "keep the lane area uncluttered" note.
        private static readonly Color DefaultColor =
            new Color(UIHelpers.BgDeep.r, UIHelpers.BgDeep.g, UIHelpers.BgDeep.b, 0.62f);

        private static Sprite _panelSprite;

        /// <summary>
        /// Create a lane backdrop sized to <paramref name="highway"/>'s lanes, returned so a caller
        /// can keep or destroy it. Null if the highway has no lane data yet. The optional overrides
        /// are the single swap point: pass a tint to recolour, or a sprite to drop in a custom
        /// track later in place of the generated flat panel.
        /// </summary>
        public static GameObject Attach(HighwayBase highway, Color? color = null, Sprite spriteOverride = null,
            float sidePadLanes = 0.65f, float heightOverscan = 1.06f)
        {
            if (highway == null) return null;
            var lanes = highway.LanePositions;
            if (lanes == null || lanes.Count == 0) return null;

            // Horizontal span from the lane positions: the full lane spread plus a little beyond the
            // outer lanes so edge notes are not flush against the border.
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < lanes.Count; i++)
            {
                if (lanes[i] < minX) minX = lanes[i];
                if (lanes[i] > maxX) maxX = lanes[i];
            }
            float spacing = lanes.Count > 1 ? (maxX - minX) / (lanes.Count - 1) : 2f;
            float sidePad = spacing * sidePadLanes;
            float width = (maxX - minX) + sidePad * 2f;
            float centerX = (minX + maxX) * 0.5f;

            // Vertical span from the battle camera so the track runs the height of the view; fall
            // back to a tall strip around the receptor line if there is no camera.
            float centerY, height;
            Camera cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                centerY = cam.transform.position.y;
                height = cam.orthographicSize * 2f * heightOverscan;
            }
            else
            {
                centerY = highway.ReceptorY + 4f;
                height = 16f;
            }

            var go = new GameObject($"LaneBackdrop_{highway.name}");
            go.transform.position = new Vector3(centerX, centerY, 0f);
            // 1x1 sprite at 1 px-per-unit, so transform scale is a literal world size.
            go.transform.localScale = new Vector3(width, height, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spriteOverride != null ? spriteOverride : GetPanelSprite();
            sr.color = color ?? DefaultColor;

            // Same sorting layer as the receptors, two orders below them: below the receptors and
            // notes (which sit at or above receptor order, so they always draw on top), and below
            // the performers at receptor order - 1, so the panel never tints a character. A scene
            // backdrop, if added later, should sit further back still (a large negative order).
            var refReceptor = FirstReceptor(highway);
            if (refReceptor != null)
            {
                sr.sortingLayerID = refReceptor.sortingLayerID;
                sr.sortingOrder = refReceptor.sortingOrder - 2;
            }
            else
            {
                sr.sortingOrder = -2;
            }

            return go;
        }

        private static SpriteRenderer FirstReceptor(HighwayBase highway)
        {
            var receptors = highway.Receptors;
            if (receptors == null) return null;
            for (int i = 0; i < receptors.Count; i++)
                if (receptors[i] != null) return receptors[i];
            return null;
        }

        // A 1x1 white sprite at 1 pixel-per-unit, generated once and reused. Tinting is done through
        // the SpriteRenderer colour; scaling the transform gives the exact world-unit size.
        private static Sprite GetPanelSprite()
        {
            if (_panelSprite != null) return _panelSprite;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _panelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _panelSprite;
        }
    }
}
