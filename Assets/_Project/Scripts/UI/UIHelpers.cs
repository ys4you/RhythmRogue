using UnityEngine;
using UnityEngine.UI;

namespace RhythmRogue.UI
{
    /// <summary>
    /// Shared UI utility methods used across all screens.
    /// 
    /// Consolidates code that was previously copy-pasted into
    /// BattleUI, RestScreen, MapUI, MainMenuScreen, and SummaryScreen.
    /// 
    /// All methods are static — no instance needed. This is a pure
    /// utility class, not a MonoBehaviour.
    /// </summary>
    public static class UIHelpers
    {
        // =================================================================
        // REFERENCE RESOLUTION — consistent across all screens
        // =================================================================

        public const float ReferenceWidth = 384f;
        public const float ReferenceHeight = 216f;

        // =================================================================
        // COLORS
        // =================================================================

        /// <summary>
        /// HP bar color based on percentage. Used by BattleUI, RestScreen, MapUI.
        /// Green (>50%) → Yellow (>25%) → Red (≤25%).
        /// </summary>
        public static Color HPColor(float pct)
        {
            if (pct > 0.5f) return Color.Lerp(Color.yellow, Color.green, (pct - 0.5f) * 2f);
            if (pct > 0.25f) return Color.Lerp(Color.red, Color.yellow, (pct - 0.25f) * 4f);
            return Color.red;
        }

        // =================================================================
        // CANVAS CREATION
        // =================================================================

        /// <summary>
        /// Create a standard screen-space overlay Canvas with 384×216 scaler.
        /// Includes GraphicRaycaster. Optionally creates an EventSystem if none exists.
        /// </summary>
        /// <param name="parent">Parent transform for the Canvas GameObject.</param>
        /// <param name="name">Canvas GameObject name.</param>
        /// <param name="sortingOrder">Canvas sorting order.</param>
        /// <param name="ensureEventSystem">Create EventSystem if missing.</param>
        /// <returns>The Canvas's RectTransform.</returns>
        public static RectTransform CreateCanvas(Transform parent, string name,
            int sortingOrder = 50, bool ensureEventSystem = true)
        {
            GameObject canvasGO = new GameObject(name);
            canvasGO.transform.SetParent(parent);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            if (ensureEventSystem && UnityEngine.EventSystems.EventSystem.current == null)
            {
                GameObject esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            return canvasGO.GetComponent<RectTransform>();
        }

        // =================================================================
        // ELEMENT CREATION
        // =================================================================

        /// <summary>
        /// Create a colored panel (Image on a RectTransform).
        /// </summary>
        public static GameObject MakePanel(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 pos, Vector2 size, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            obj.GetComponent<Image>().color = color;
            return obj;
        }

        /// <summary>
        /// Create a Text element with standard configuration.
        /// Uses system Arial font. Post-prototype: replace with cached TMP font.
        /// </summary>
        public static Text MakeText(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 pos, Vector2 size,
            int fontSize, TextAnchor alignment, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Text t = obj.GetComponent<Text>();
            t.text = "";
            t.fontSize = fontSize;
            t.font = GetDefaultFont(fontSize);
            t.alignment = alignment;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;

            return t;
        }

        /// <summary>
        /// Create a Button with a colored background and centered label.
        /// </summary>
        public static Button MakeButton(RectTransform parent, string name, string label,
            Vector2 anchor, Vector2 pos, Vector2 size, Color bgColor,
            int fontSize = 6)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            obj.GetComponent<Image>().color = bgColor;

            // Label
            Text txt = MakeText(rt, "Text",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                fontSize, TextAnchor.MiddleCenter, Color.white);
            txt.rectTransform.offsetMin = Vector2.zero;
            txt.rectTransform.offsetMax = Vector2.zero;
            txt.fontStyle = FontStyle.Bold;
            txt.text = label;

            return obj.GetComponent<Button>();
        }

        // =================================================================
        // SLIDER CREATION
        // =================================================================

        /// <summary>
        /// Create a minimal slider using Unity UI primitives. No prefab needed.
        /// </summary>
        public static Slider MakeSlider(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 pos, Vector2 size,
            float min = 0f, float max = 1f, float value = 0.5f)
        {
            // Root
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);

            RectTransform rootRT = root.GetComponent<RectTransform>();
            rootRT.anchorMin = anchorMin;
            rootRT.anchorMax = anchorMax;
            rootRT.pivot = pivot;
            rootRT.anchoredPosition = pos;
            rootRT.sizeDelta = size;

            // Background
            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(root.transform, false);
            RectTransform bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);

            // Fill area
            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            RectTransform fillAreaRT = fillArea.GetComponent<RectTransform>();
            fillAreaRT.anchorMin = Vector2.zero;
            fillAreaRT.anchorMax = Vector2.one;
            fillAreaRT.offsetMin = Vector2.zero;
            fillAreaRT.offsetMax = Vector2.zero;

            // Fill
            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = new Color(0.4f, 0.5f, 0.7f);

            // Handle area
            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(root.transform, false);
            RectTransform handleAreaRT = handleArea.GetComponent<RectTransform>();
            handleAreaRT.anchorMin = Vector2.zero;
            handleAreaRT.anchorMax = Vector2.one;
            handleAreaRT.offsetMin = Vector2.zero;
            handleAreaRT.offsetMax = Vector2.zero;

            // Handle
            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            RectTransform handleRT = handle.GetComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(6, size.y + 2);
            handle.GetComponent<Image>().color = Color.white;

            // Wire slider
            Slider slider = root.GetComponent<Slider>();
            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;

            return slider;
        }

        // =================================================================
        // FONT — cached to avoid per-element allocation
        // =================================================================

        private static Font _cachedFont;

        /// <summary>
        /// Get (or create once) the default system font.
        /// Post-prototype: replace with a TMP font asset reference.
        /// </summary>
        public static Font GetDefaultFont(int size = 12)
        {
            if (_cachedFont == null)
                _cachedFont = Font.CreateDynamicFontFromOSFont("Arial", size);

            return _cachedFont;
        }
    }
}
