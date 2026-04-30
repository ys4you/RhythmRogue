using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.UI.Navigation;

namespace RhythmRogue.UI
{
    /// <summary>
    /// Shared UI utilities. Fonts: m5x7 (body, size &lt;36) and m6x11plus (titles, size 36+).
    /// Reference resolution: 1920x1080. Pixel Perfect Camera at 384x216 for gameplay.
    /// </summary>
    public static class UIHelpers
    {
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;

        public static Color HPColor(float pct)
        {
            if (pct > 0.5f) return Color.Lerp(Color.yellow, Color.green, (pct - 0.5f) * 2f);
            if (pct > 0.25f) return Color.Lerp(Color.red, Color.yellow, (pct - 0.25f) * 4f);
            return Color.red;
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

            var txt = MakeText(rt, "Text", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, fontSize, TextAnchor.MiddleCenter, Color.white);
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
            bg.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform)); fillArea.transform.SetParent(root.transform, false);
            var faRT = fillArea.GetComponent<RectTransform>(); faRT.anchorMin = Vector2.zero; faRT.anchorMax = Vector2.one; faRT.offsetMin = faRT.offsetMax = Vector2.zero;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)); fill.transform.SetParent(fillArea.transform, false);
            var fillRT = fill.GetComponent<RectTransform>(); fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one; fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = new Color(0.4f, 0.5f, 0.7f);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)); handleArea.transform.SetParent(root.transform, false);
            var haRT = handleArea.GetComponent<RectTransform>(); haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one; haRT.offsetMin = haRT.offsetMax = Vector2.zero;

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)); handle.transform.SetParent(handleArea.transform, false);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(30, size.y + 10);
            handle.GetComponent<Image>().color = Color.white;

            var slider = root.GetComponent<Slider>();
            slider.fillRect = fillRT; slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = min; slider.maxValue = max; slider.value = value;
            return slider;
        }

        // Font system: sizes 36+ get m6x11plus (titles), smaller get m5x7 (body)
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
