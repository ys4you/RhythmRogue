using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.Core.Display;

namespace RhythmRogue.Core
{
    /// <summary>
    /// Persistent CRT arcade cabinet overlay. Sits on top of all scenes.
    /// Creates bezel frame, scanlines, and vignette for retro feel.
    /// </summary>
    public class CRTOverlay : Util.Singleton<CRTOverlay>
    {
        [Header("Bezel")]
        [SerializeField] private float _bezelThickness = 32f;
        [SerializeField] private Color _bezelColor = new Color(0.04f, 0.04f, 0.04f, 1f);
        [SerializeField] private Color _bezelInner = new Color(0.08f, 0.08f, 0.08f, 1f);
        [SerializeField] private float _innerBorderWidth = 3f;

        [Header("Scanlines")]
        [SerializeField] private float _scanlineAlpha = 0.06f;
        [SerializeField] private int _scanlineSpacing = 3;

        [Header("Vignette")]
        [SerializeField] private float _vignetteStrength = 0.4f;

        [Header("Screen Tint")]
        [SerializeField] private Color _screenTint = new Color(0.95f, 1f, 0.92f, 0.02f);

        private Canvas _canvas;

        protected override void Awake()
        {
            base.Awake();
            CreateOverlay();
            ApplyVisibility();
        }

        /// <summary>
        /// Re-reads the CRT setting and shows/hides the overlay canvas.
        /// Called by the settings UI toggle.
        /// </summary>
        public void ApplyVisibility()
        {
            if (_canvas != null)
                _canvas.gameObject.SetActive(DisplaySettings.CRTEffect);
        }

        private void CreateOverlay()
        {
            var canvasGO = new GameObject("CRT_Canvas");
            canvasGO.transform.SetParent(transform);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10000;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>().blockingObjects = GraphicRaycaster.BlockingObjects.None;

            RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();

            // Disable raycasting on everything so the overlay doesn't block input
            CreateBezel(canvasRT);
            CreateScanlines(canvasRT);
            CreateVignette(canvasRT);
            CreateScreenTint(canvasRT);
        }

        private void CreateBezel(RectTransform parent)
        {
            float b = _bezelThickness;
            float ib = _innerBorderWidth;

            // Top
            CreateBar(parent, "Bezel_Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0, b), _bezelColor);
            CreateBar(parent, "BezelInner_Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0, -b), new Vector2(0, ib), _bezelInner);

            // Bottom
            CreateBar(parent, "Bezel_Bottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0, b), _bezelColor);
            CreateBar(parent, "BezelInner_Bottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0f), new Vector2(0, b), new Vector2(0, ib), _bezelInner);

            // Left
            CreateBar(parent, "Bezel_Left", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(b, 0), _bezelColor);
            CreateBar(parent, "BezelInner_Left", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0f, 0.5f), new Vector2(b, 0), new Vector2(ib, 0), _bezelInner);

            // Right
            CreateBar(parent, "Bezel_Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(b, 0), _bezelColor);
            CreateBar(parent, "BezelInner_Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1f, 0.5f), new Vector2(-b, 0), new Vector2(ib, 0), _bezelInner);
        }

        private void CreateBar(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 offset, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = offset;
            rt.sizeDelta = sizeDelta;

            // Stretch: for bars, offset from anchors
            if (anchorMin.x == 0 && anchorMax.x == 1) // horizontal bar
            {
                rt.offsetMin = new Vector2(0, rt.offsetMin.y);
                rt.offsetMax = new Vector2(0, rt.offsetMax.y);
            }
            if (anchorMin.y == 0 && anchorMax.y == 1) // vertical bar
            {
                rt.offsetMin = new Vector2(rt.offsetMin.x, 0);
                rt.offsetMax = new Vector2(rt.offsetMax.x, 0);
            }

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private void CreateScanlines(RectTransform parent)
        {
            // Generate a small tiling texture: alternating clear/dark rows
            int texHeight = _scanlineSpacing * 2;
            var tex = new Texture2D(1, texHeight, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;

            for (int y = 0; y < texHeight; y++)
            {
                // Dark line every N pixels
                bool isDark = y < _scanlineSpacing;
                Color c = isDark ? new Color(0, 0, 0, _scanlineAlpha) : new Color(0, 0, 0, 0);
                tex.SetPixel(0, y, c);
            }
            tex.Apply();

            var go = new GameObject("Scanlines", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var raw = go.GetComponent<RawImage>();
            raw.texture = tex;
            raw.color = Color.white;
            raw.raycastTarget = false;

            // Tile the texture across the screen
            // UV rect: width=1 (no horizontal tiling), height = screen pixels / texture height
            float screenH = 1080f;
            float tileCount = screenH / texHeight;
            raw.uvRect = new Rect(0, 0, 1, tileCount);
        }

        private void CreateVignette(RectTransform parent)
        {
            // Generate radial gradient texture for vignette
            int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxDist = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center.x) / maxDist;
                    float dy = (y - center.y) / maxDist;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    // Smooth falloff from center to edges
                    float vignette = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dist - 0.4f) / 0.6f));
                    float alpha = vignette * _vignetteStrength;

                    tex.SetPixel(x, y, new Color(0, 0, 0, alpha));
                }
            }
            tex.Apply();

            var go = new GameObject("Vignette", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var raw = go.GetComponent<RawImage>();
            raw.texture = tex;
            raw.color = Color.white;
            raw.raycastTarget = false;
        }

        private void CreateScreenTint(RectTransform parent)
        {
            if (_screenTint.a <= 0f) return;

            var go = new GameObject("ScreenTint", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = _screenTint;
            img.raycastTarget = false;
        }
    }
}
