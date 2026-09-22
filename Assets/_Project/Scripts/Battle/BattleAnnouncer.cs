using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.UI;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// A transient headline flashed over a fight when something dramatic happens ("IT REFUSES TO
    /// DIE"). Screen flash, a text pop, then it fades out on its own. Nothing to dismiss and
    /// nothing to wire: the fight keeps playing underneath.
    ///
    /// Self-contained on its own overlay canvas, built lazily on the first announcement, following
    /// the same Create() pattern as <see cref="LessonCallout"/> and RelicBar. It knows nothing
    /// about what triggered it, so any system can use it for any moment.
    ///
    /// Readability first: the banner sits high on the screen and clears the lane area, the flash is
    /// brief and low-alpha, and everything is raycast-transparent. A rhythm game cannot afford to
    /// hide notes for drama, so the hold is deliberately short.
    /// </summary>
    public class BattleAnnouncer : MonoBehaviour
    {
        // Flash: fast in, quick decay. Low alpha on purpose so notes stay readable through it.
        private const float FlashPeakAlpha = 0.28f;
        private const float FlashInSeconds = 0.05f;
        private const float FlashOutSeconds = 0.40f;

        // Text: pop in, short hold, fade out while drifting slightly larger.
        private const float TextInSeconds = 0.12f;
        private const float TextHoldSeconds = 0.80f;
        private const float TextOutSeconds = 0.45f;
        private const float PopScale = 1.45f;

        /// <summary>Height on screen, 0 = bottom, 1 = top. High enough to clear the lanes.
        /// Move this if the banner ever overlaps the highway on your layout.</summary>
        private const float ScreenHeightRatio = 0.82f;

        private GameObject _canvasGO;
        private Image _flash;
        private Text _text;
        private Outline _outline;
        private RectTransform _textRT;

        private float _elapsed;
        private bool _playing;

        private static float TotalSeconds => TextInSeconds + TextHoldSeconds + TextOutSeconds;

        public static BattleAnnouncer Create()
        {
            var go = new GameObject("BattleAnnouncer");
            return go.AddComponent<BattleAnnouncer>();
        }

        /// <summary>Flash a headline in the default warning tone. Retriggering restarts it.</summary>
        public void Announce(string text) => Announce(text, UIHelpers.RustOrange);

        /// <summary>Flash a headline in a given tone. Empty text is ignored.</summary>
        public void Announce(string text, Color tint)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (_canvasGO == null) Build();

            _text.text = text.ToUpperInvariant();
            _text.color = new Color(tint.r, tint.g, tint.b, 0f);
            _flash.color = new Color(tint.r, tint.g, tint.b, 0f);

            _canvasGO.SetActive(true);
            _elapsed = 0f;
            _playing = true;
            Apply();
        }

        private void Build()
        {
            _canvasGO = new GameObject("BattleAnnouncerCanvas", typeof(RectTransform), typeof(Canvas));
            _canvasGO.transform.SetParent(transform, false);
            var canvas = _canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the battle HUD (100) and relic bar (120); below the lesson callout (130) and
            // the pause menu, so a coach-mark or a pause always wins.
            canvas.sortingOrder = 125;
            var canvasRT = _canvasGO.GetComponent<RectTransform>();

            // Full-screen flash, behind the text.
            var flashGO = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            flashGO.transform.SetParent(canvasRT, false);
            var flashRT = flashGO.GetComponent<RectTransform>();
            flashRT.anchorMin = Vector2.zero;
            flashRT.anchorMax = Vector2.one;
            flashRT.offsetMin = Vector2.zero;
            flashRT.offsetMax = Vector2.zero;
            _flash = flashGO.GetComponent<Image>();
            _flash.raycastTarget = false;

            float s = ScaleFactor();

            var textGO = new GameObject("Headline", typeof(RectTransform), typeof(Text), typeof(Outline));
            textGO.transform.SetParent(canvasRT, false);
            _textRT = textGO.GetComponent<RectTransform>();
            _textRT.anchorMin = _textRT.anchorMax = new Vector2(0.5f, ScreenHeightRatio);
            _textRT.pivot = new Vector2(0.5f, 0.5f);
            _textRT.anchoredPosition = Vector2.zero;
            _textRT.sizeDelta = new Vector2(1400f * s, 120f * s);

            int size = Mathf.RoundToInt(64f * s);
            _text = textGO.GetComponent<Text>();
            _text.font = UIHelpers.GetDefaultFont(size);
            _text.fontSize = size;
            _text.fontStyle = FontStyle.Bold;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.raycastTarget = false;

            // Heavy dark outline so the headline stays legible over the enemy, the lanes and
            // whatever the backdrop colour happens to be.
            _outline = textGO.GetComponent<Outline>();
            _outline.effectColor = new Color(UIHelpers.BgDeep.r, UIHelpers.BgDeep.g, UIHelpers.BgDeep.b, 0f);
            _outline.effectDistance = new Vector2(3f * s, -3f * s);
            _outline.useGraphicAlpha = false;
        }

        private void Update()
        {
            if (!_playing) return;

            // Unscaled so the beat still lands if something has slowed or stopped time.
            _elapsed += Time.unscaledDeltaTime;

            if (_elapsed >= TotalSeconds)
            {
                _playing = false;
                if (_canvasGO != null) _canvasGO.SetActive(false);
                return;
            }

            Apply();
        }

        private void Apply()
        {
            SetImageAlpha(_flash, FlashAlpha(_elapsed));

            TextState(_elapsed, out float alpha, out float scale);
            SetTextAlpha(alpha);
            _textRT.localScale = new Vector3(scale, scale, 1f);
        }

        private static float FlashAlpha(float t)
        {
            if (t < FlashInSeconds) return Mathf.Lerp(0f, FlashPeakAlpha, t / FlashInSeconds);
            if (t < FlashInSeconds + FlashOutSeconds)
                return Mathf.Lerp(FlashPeakAlpha, 0f, (t - FlashInSeconds) / FlashOutSeconds);
            return 0f;
        }

        private static void TextState(float t, out float alpha, out float scale)
        {
            if (t < TextInSeconds)
            {
                float k = t / TextInSeconds;
                alpha = k;
                scale = Mathf.Lerp(PopScale, 1f, k);
                return;
            }

            if (t < TextInSeconds + TextHoldSeconds)
            {
                alpha = 1f;
                scale = 1f;
                return;
            }

            float o = (t - TextInSeconds - TextHoldSeconds) / TextOutSeconds;
            alpha = 1f - o;
            // Drifts a little larger as it fades, so it reads as dissipating rather than blinking off.
            scale = 1f + 0.10f * o;
        }

        private void SetTextAlpha(float a)
        {
            Color c = _text.color; c.a = a; _text.color = c;
            Color o = _outline.effectColor; o.a = a; _outline.effectColor = o;
        }

        private static void SetImageAlpha(Image img, float a)
        {
            Color c = img.color; c.a = a; img.color = c;
        }

        private static float ScaleFactor() => Mathf.Max(0.5f, Screen.height / 1080f);

        private void OnDestroy()
        {
            if (_canvasGO != null) Destroy(_canvasGO);
        }
    }
}
