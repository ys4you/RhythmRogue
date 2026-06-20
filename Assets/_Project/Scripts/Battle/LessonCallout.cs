using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.UI;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// A coach-mark for the onboarding: dims the screen, leaves a bright hole over one HUD element
    /// (the guard badge, the relic bar), rings it, and shows the lesson text in a card below it, so
    /// the player is shown WHERE to look, not just told. Used only for lessons that teach a specific
    /// on-screen thing; the plain text lessons keep using BattleUI's lesson card.
    ///
    /// The hole works by sitting on a canvas ABOVE the HUD and leaving a gap over the target's
    /// screen rect, so the real element underneath shows through at full brightness while everything
    /// else is covered. Raw-pixel overlay canvas (no scaler) so the gap lines up with the target's
    /// actual screen position; text is scaled by resolution so it stays a sensible size. Only shows
    /// while the fight is frozen on the lesson, so it is not the during-play dim that read as too
    /// much.
    /// </summary>
    public class LessonCallout : MonoBehaviour
    {
        private const float HolePad = 14f;
        private const float RingWidth = 4f;

        private GameObject _canvasGO;
        private RectTransform _left, _right, _top, _bottom;
        private RectTransform _ringL, _ringR, _ringT, _ringB;
        private RectTransform _card;
        private Text _text, _prompt;
        private RectTransform _target;
        private bool _shown;
        private readonly Vector3[] _corners = new Vector3[4];

        public static LessonCallout Create()
        {
            var go = new GameObject("LessonCallout");
            return go.AddComponent<LessonCallout>();
        }

        /// <summary>Dim the screen, spotlight <paramref name="target"/>, and show the lesson text.</summary>
        public void Show(string text, RectTransform target)
        {
            if (_canvasGO == null) Build();
            _target = target;
            if (_text != null) _text.text = text;
            _canvasGO.SetActive(true);
            _shown = true;
            Layout();
        }

        public void Hide()
        {
            _shown = false;
            if (_canvasGO != null) _canvasGO.SetActive(false);
        }

        private void Build()
        {
            _canvasGO = new GameObject("LessonCalloutCanvas", typeof(RectTransform), typeof(Canvas));
            _canvasGO.transform.SetParent(transform, false);
            var canvas = _canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the battle HUD (100) and relic bar (120) so the hole reveals them; below pause.
            canvas.sortingOrder = 130;

            Color dim = new Color(UIHelpers.BgDeep.r, UIHelpers.BgDeep.g, UIHelpers.BgDeep.b, 0.9f);
            _left = MakeRect("DimLeft", dim);
            _right = MakeRect("DimRight", dim);
            _top = MakeRect("DimTop", dim);
            _bottom = MakeRect("DimBottom", dim);

            Color ring = new Color(UIHelpers.WarmGold.r, UIHelpers.WarmGold.g, UIHelpers.WarmGold.b, 1f);
            _ringL = MakeRect("RingL", ring);
            _ringR = MakeRect("RingR", ring);
            _ringT = MakeRect("RingT", ring);
            _ringB = MakeRect("RingB", ring);

            float s = ScaleFactor();

            // Text card: bright surface panel anchored bottom-center (targets live at the top).
            var cardGO = new GameObject("Card", typeof(RectTransform), typeof(Image));
            cardGO.transform.SetParent(_canvasGO.transform, false);
            cardGO.GetComponent<Image>().color = new Color(UIHelpers.BgSurface.r, UIHelpers.BgSurface.g, UIHelpers.BgSurface.b, 0.98f);
            _card = cardGO.GetComponent<RectTransform>();
            _card.anchorMin = _card.anchorMax = new Vector2(0.5f, 0f);
            _card.pivot = new Vector2(0.5f, 0f);

            float pad = 40f * s;
            _text = MakeText(_card, "Text", 30, TextAnchor.MiddleCenter, UIHelpers.OffWhite, true);
            _text.rectTransform.anchorMin = new Vector2(0f, 0f);
            _text.rectTransform.anchorMax = new Vector2(1f, 1f);
            _text.rectTransform.offsetMin = new Vector2(pad, pad + 36f * s);
            _text.rectTransform.offsetMax = new Vector2(-pad, -pad);

            _prompt = MakeText(_card, "Prompt", 22, TextAnchor.MiddleCenter, UIHelpers.AmberOrange, false);
            _prompt.fontStyle = FontStyle.Italic;
            _prompt.text = "Press any key to begin";
            _prompt.rectTransform.anchorMin = new Vector2(0f, 0f);
            _prompt.rectTransform.anchorMax = new Vector2(1f, 0f);
            _prompt.rectTransform.pivot = new Vector2(0.5f, 0f);
            _prompt.rectTransform.sizeDelta = new Vector2(-2f * pad, 40f * s);
            _prompt.rectTransform.anchoredPosition = new Vector2(0f, 16f * s);
        }

        private RectTransform MakeRect(string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_canvasGO.transform, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            return rt;
        }

        private Text MakeText(RectTransform parent, string name, int nominalSize, TextAnchor align, Color color, bool wrap)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            int size = Mathf.RoundToInt(nominalSize * ScaleFactor());
            t.font = UIHelpers.GetDefaultFont(size);
            t.fontSize = size; t.alignment = align; t.color = color;
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static float ScaleFactor() => Mathf.Max(0.5f, Screen.height / 1080f);

        private void LateUpdate()
        {
            if (_shown) Layout();
        }

        private void Layout()
        {
            if (_target == null || _canvasGO == null) return;
            float w = Screen.width, h = Screen.height;

            // Target screen rect: an overlay canvas's world corners are screen pixels.
            _target.GetWorldCorners(_corners);
            float left = _corners[0].x - HolePad;
            float bottom = _corners[0].y - HolePad;
            float right = _corners[2].x + HolePad;
            float top = _corners[2].y + HolePad;

            // Guarantee a sensible minimum hole even if the element is tiny (e.g. a single relic).
            float minHole = 60f * ScaleFactor();
            if (right - left < minHole) { float c = (left + right) * 0.5f; left = c - minHole * 0.5f; right = c + minHole * 0.5f; }
            if (top - bottom < minHole) { float c = (top + bottom) * 0.5f; bottom = c - minHole * 0.5f; top = c + minHole * 0.5f; }

            left = Mathf.Clamp(left, 0f, w); right = Mathf.Clamp(right, 0f, w);
            bottom = Mathf.Clamp(bottom, 0f, h); top = Mathf.Clamp(top, 0f, h);

            float bandH = top - bottom;
            SetRect(_left, 0f, bottom, left, bandH);
            SetRect(_right, right, bottom, w - right, bandH);
            SetRect(_top, 0f, top, w, h - top);
            SetRect(_bottom, 0f, 0f, w, bottom);

            // Pulsing ring just outside the hole edges.
            float pulse = 0.55f + 0.45f * Mathf.PingPong(Time.unscaledTime * 2f, 1f);
            SetAlpha(_ringL, pulse); SetAlpha(_ringR, pulse); SetAlpha(_ringT, pulse); SetAlpha(_ringB, pulse);
            SetRect(_ringL, left - RingWidth, bottom, RingWidth, bandH);
            SetRect(_ringR, right, bottom, RingWidth, bandH);
            SetRect(_ringT, left - RingWidth, top, right - left + RingWidth * 2f, RingWidth);
            SetRect(_ringB, left - RingWidth, bottom - RingWidth, right - left + RingWidth * 2f, RingWidth);

            // Card: lower-center, sized to resolution, kept clear of the top-anchored targets.
            float s = ScaleFactor();
            _card.sizeDelta = new Vector2(Mathf.Min(1100f * s, w * 0.9f), 380f * s);
            _card.anchoredPosition = new Vector2(0f, h * 0.12f);
        }

        private void SetAlpha(RectTransform rt, float a)
        {
            var img = rt.GetComponent<Image>();
            var c = img.color; c.a = a; img.color = c;
        }

        private void SetRect(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(Mathf.Max(0f, w), Mathf.Max(0f, h));
        }

        private void OnDestroy()
        {
            if (_canvasGO != null) Destroy(_canvasGO);
        }
    }
}
