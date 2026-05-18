using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.UI.Navigation;

namespace RhythmRogue.UI
{
    /// <summary>
    /// Shared UI utilities. Warm 8-color palette + 4 lane colors.
    /// Fonts: m5x7 (body, size &lt;36) and m6x11plus (titles, size 36+).
    /// </summary>
    public static class UIHelpers
    {
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;

        // Warm 8-color palette
        public static readonly Color BgDeep      = new(0.082f, 0.047f, 0.129f); // #150c21
        public static readonly Color BgSurface   = new(0.200f, 0.133f, 0.196f); // #332232
        public static readonly Color BgLight     = new(0.439f, 0.235f, 0.404f); // #703c67
        public static readonly Color Shadow      = new(0.353f, 0.176f, 0.165f); // #5a2d2a
        public static readonly Color RustOrange  = new(0.694f, 0.357f, 0.208f); // #b15b35
        public static readonly Color AmberOrange = new(0.918f, 0.643f, 0.290f); // #eaa44a
        public static readonly Color WarmGold    = new(0.941f, 0.808f, 0.431f); // #f0ce6e
        public static readonly Color OffWhite    = new(0.988f, 0.973f, 0.957f); // #fcf8f4

        // Lane colors (exempt from palette - need maximum mutual contrast)
        public static readonly Color LaneLeft  = new(1f, 0.3f, 0.3f);
        public static readonly Color LaneDown  = new(0.3f, 0.85f, 1f);
        public static readonly Color LaneUp    = new(0.3f, 1f, 0.3f);
        public static readonly Color LaneRight = new(1f, 1f, 0.3f);

        /// <summary>
        /// HP-by-percent: warm gradient gold to amber to rust as health drops.
        /// </summary>
        public static Color HPColor(float pct)
        {
            if (pct > 0.5f) return Color.Lerp(AmberOrange, WarmGold, (pct - 0.5f) * 2f);
            if (pct > 0.25f) return Color.Lerp(RustOrange, AmberOrange, (pct - 0.25f) * 4f);
            return RustOrange;
        }

        public static RectTransform CreateCanvas(Transform parent, string name, int sortingOrder = 50, bool ensureEventSystem = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            if (ensureEventSystem) UIEventSystemProvider.EnsureEventSystem();
            return go.GetComponent<RectTransform>();
        }

        public static GameObject MakePanel(RectTransform parent, string name, Vector2 ancMin, Vector2 ancMax, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = ancMin; rt.anchorMax = ancMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            obj.GetComponent<Image>().color = color;
            return obj;
        }

        public static Text MakeText(RectTransform parent, string name, Vector2 ancMin, Vector2 ancMax, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize, TextAnchor alignment, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = ancMin; rt.anchorMax = ancMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = obj.GetComponent<Text>();
            t.fontSize = fontSize; t.font = GetDefaultFont(fontSize);
            t.alignment = alignment; t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            return t;
        }

        public static Button MakeButton(RectTransform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color bgColor, int fontSize = 28)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            obj.GetComponent<Image>().color = bgColor;

            var txt = MakeText(rt, "Text", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, fontSize, TextAnchor.MiddleCenter, OffWhite);
            txt.rectTransform.offsetMin = Vector2.zero;
            txt.rectTransform.offsetMax = Vector2.zero;
            txt.fontStyle = FontStyle.Bold;
            txt.text = label;
            return obj.GetComponent<Button>();
        }

        public static Slider MakeSlider(RectTransform parent, string name, Vector2 ancMin, Vector2 ancMax, Vector2 pivot, Vector2 pos, Vector2 size, float min = 0f, float max = 1f, float value = 0.5f)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            var rootRT = root.GetComponent<RectTransform>();
            rootRT.anchorMin = ancMin; rootRT.anchorMax = ancMax; rootRT.pivot = pivot;
            rootRT.anchoredPosition = pos; rootRT.sizeDelta = size;

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image)); bg.transform.SetParent(root.transform, false);
            var bgRT = bg.GetComponent<RectTransform>(); bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one; bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = BgSurface;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform)); fillArea.transform.SetParent(root.transform, false);
            var faRT = fillArea.GetComponent<RectTransform>(); faRT.anchorMin = Vector2.zero; faRT.anchorMax = Vector2.one; faRT.offsetMin = faRT.offsetMax = Vector2.zero;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)); fill.transform.SetParent(fillArea.transform, false);
            var fillRT = fill.GetComponent<RectTransform>(); fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one; fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = AmberOrange;

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)); handleArea.transform.SetParent(root.transform, false);
            var haRT = handleArea.GetComponent<RectTransform>(); haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one; haRT.offsetMin = haRT.offsetMax = Vector2.zero;

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)); handle.transform.SetParent(handleArea.transform, false);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(30, size.y + 10);
            handle.GetComponent<Image>().color = WarmGold;

            var slider = root.GetComponent<Slider>();
            slider.fillRect = fillRT; slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = min; slider.maxValue = max; slider.value = value;
            return slider;
        }

        private static Font _bodyFont, _titleFont, _fallback;
        public static Font GetDefaultFont(int size = 24) => size >= 36 ? GetTitleFont() : GetBodyFont();
        public static Font GetBodyFont()
        {
            if (_bodyFont == null) _bodyFont = Resources.Load<Font>("Fonts/m5x7");
            return _bodyFont != null ? _bodyFont : GetFallbackFont();
        }
        public static Font GetTitleFont()
        {
            if (_titleFont == null) _titleFont = Resources.Load<Font>("Fonts/m6x11plus");
            return _titleFont != null ? _titleFont : GetFallbackFont();
        }
        private static Font GetFallbackFont()
        {
            if (_fallback == null) _fallback = Font.CreateDynamicFontFromOSFont("Arial", 24);
            return _fallback;
        }
    }
}