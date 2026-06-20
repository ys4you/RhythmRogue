using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.Core;
using RhythmRogue.UI;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// While the song is playing in the onboarding, darkens the screen and frames the lane whose
    /// note is next with bright rails, so a new player's eye is pulled to where they have to act.
    /// The lit window slides between lanes as the upcoming note changes and widens for a chord.
    /// BattleManager only adds it in practice mode, so normal fights are never dimmed.
    ///
    /// Kept off during the intro/lesson (while the conductor is not playing) so the teaching text is
    /// read on a clean screen; it engages the moment the song starts. This is also the visual
    /// primitive the guided "freeze on the first note" step builds on.
    ///
    /// Implementation: a screen-space overlay on its own canvas, with NO CanvasScaler so its
    /// coordinates are raw screen pixels that line up with the camera projection. Sorted at 50,
    /// below the battle HUD (100) and the lane key labels (95), so the world dims while those stay
    /// readable. The bright window is the gap left between four dim panels (so the active lane's
    /// notes render untinted), and two bright rails mark its edges so the focus reads even on a
    /// dark scene. Sibling component on the player highway.
    /// </summary>
    [RequireComponent(typeof(NoteHighway))]
    public class LaneFocus : MonoBehaviour
    {
        [Tooltip("Darkness outside the bright window. 0 = no dim, 1 = fully opaque.")]
        [SerializeField, Range(0f, 1f)] private float _dimStrength = 0.82f;
        [Tooltip("Width of the bright rails framing the active lane, in screen pixels.")]
        [SerializeField] private float _railWidth = 6f;
        [Tooltip("World-units of width added on each side of the highlighted lane(s) for the window.")]
        [SerializeField] private float _lanePadding = 0.45f;
        [Tooltip("How quickly the window slides to a new lane. Higher is snappier.")]
        [SerializeField] private float _slideSpeed = 10f;
        [Tooltip("World Y of the window's bottom edge. The default sits below the view (no bottom dim).")]
        [SerializeField] private float _windowBottomY = -100f;
        [Tooltip("World Y of the window's top edge. Lower it below the enemy to also dim the enemy band.")]
        [SerializeField] private float _windowTopY = 100f;
        [Tooltip("Only notes within this many beats ahead can claim the focus, so it does not jump to far-off notes.")]
        [SerializeField] private float _lookaheadBeats = 8f;

        private NoteHighway _highway;
        private Conductor _conductor;
        private Camera _camera;
        private GameObject _canvasGO;
        private RectTransform _left, _right, _top, _bottom, _railLeft, _railRight;
        private bool _ready;

        // Current (lerped) window horizontal edges, in screen pixels.
        private float _winLeftPx, _winRightPx;
        private bool _haveWindow;

        private void Start()
        {
            _highway = GetComponent<NoteHighway>();
            StartCoroutine(InitNextFrame());
        }

        private System.Collections.IEnumerator InitNextFrame()
        {
            yield return null;
            _conductor = Conductor.Instance;
            _camera = Camera.main;
            if (_highway == null || _highway.LanePositions == null || _highway.LanePositions.Count == 0)
                yield break;

            // Own canvas, screen-space overlay, NO scaler so 1 unit == 1 screen pixel.
            _canvasGO = new GameObject("LaneFocusCanvas", typeof(RectTransform), typeof(Canvas));
            var canvas = _canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            Color dim = new Color(UIHelpers.BgDeep.r, UIHelpers.BgDeep.g, UIHelpers.BgDeep.b, _dimStrength);
            _left = MakeRect("FocusDimLeft", dim);
            _right = MakeRect("FocusDimRight", dim);
            _top = MakeRect("FocusDimTop", dim);
            _bottom = MakeRect("FocusDimBottom", dim);

            // Rails created last so they draw on top of the dim panels.
            Color rail = new Color(UIHelpers.WarmGold.r, UIHelpers.WarmGold.g, UIHelpers.WarmGold.b, 0.95f);
            _railLeft = MakeRect("FocusRailLeft", rail);
            _railRight = MakeRect("FocusRailRight", rail);

            _canvasGO.SetActive(false);
            _ready = true;
        }

        private RectTransform MakeRect(string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_canvasGO.transform, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.zero; rt.pivot = Vector2.zero;
            return rt;
        }

        private void LateUpdate()
        {
            if (!_ready) return;

            // Only dim while the song is actually playing, so the intro and lesson screens stay
            // clean. Resets the window so it re-snaps to the first note when play resumes.
            bool active = _conductor != null && _conductor.IsPlaying && !_conductor.IsPaused;
            if (_canvasGO.activeSelf != active) _canvasGO.SetActive(active);
            if (!active) { _haveWindow = false; return; }

            if (_camera == null) { _camera = Camera.main; if (_camera == null) return; }

            GetActiveLaneRange(out float worldLeftX, out float worldRightX);
            float targetLeftPx = WorldXToScreenX(worldLeftX - _lanePadding);
            float targetRightPx = WorldXToScreenX(worldRightX + _lanePadding);

            if (!_haveWindow)
            {
                _winLeftPx = targetLeftPx; _winRightPx = targetRightPx; _haveWindow = true;
            }
            else
            {
                float t = 1f - Mathf.Exp(-_slideSpeed * Time.unscaledDeltaTime);
                _winLeftPx = Mathf.Lerp(_winLeftPx, targetLeftPx, t);
                _winRightPx = Mathf.Lerp(_winRightPx, targetRightPx, t);
            }

            LayoutDim();
        }

        // Find the lane(s) whose nearest upcoming, not-yet-resolved note is soonest, and return the
        // world-x span covering them. Falls back to the whole lane strip when nothing is upcoming
        // (gaps, end of chart), so the screen never reads as focused on nothing.
        private void GetActiveLaneRange(out float leftX, out float rightX)
        {
            var lanes = _highway.LanePositions;
            float beatNow = _conductor != null ? _conductor.SongPositionInBeats : 0f;
            var notes = _highway.ActiveNotes;

            float bestBeat = float.MaxValue;
            for (int i = 0; i < notes.Count; i++)
            {
                var n = notes[i];
                if (n == null || n.IsProcessed || n.IsHit || n.IsMissed) continue;
                float b = n.Data.BeatPosition;
                if (b < beatNow - 0.05f) continue;            // already at/under the line
                if (b > beatNow + _lookaheadBeats) continue;  // too far out to focus yet
                if (b < bestBeat) bestBeat = b;
            }

            if (bestBeat == float.MaxValue)
            {
                leftX = lanes[0]; rightX = lanes[0];
                for (int i = 1; i < lanes.Count; i++)
                {
                    leftX = Mathf.Min(leftX, lanes[i]);
                    rightX = Mathf.Max(rightX, lanes[i]);
                }
                return;
            }

            // Cover every lane with a note at ~that soonest beat (so a chord lights all its lanes).
            leftX = float.MaxValue; rightX = float.MinValue;
            for (int i = 0; i < notes.Count; i++)
            {
                var n = notes[i];
                if (n == null || n.IsProcessed || n.IsHit || n.IsMissed) continue;
                if (Mathf.Abs(n.Data.BeatPosition - bestBeat) > 0.05f) continue;
                int lane = Mathf.Clamp(n.Data.Lane, 0, lanes.Count - 1);
                leftX = Mathf.Min(leftX, lanes[lane]);
                rightX = Mathf.Max(rightX, lanes[lane]);
            }
            if (leftX == float.MaxValue) { leftX = lanes[0]; rightX = lanes[lanes.Count - 1]; }
        }

        private float WorldXToScreenX(float worldX)
            => _camera.WorldToScreenPoint(new Vector3(worldX, _highway.ReceptorY, 0f)).x;

        private float WorldYToScreenY(float worldY)
            => _camera.WorldToScreenPoint(new Vector3(_highway.LanePositions[0], worldY, 0f)).y;

        // Place the four dim panels so they cover the whole screen except the bright window rect,
        // then put the two bright rails on the window's vertical edges.
        private void LayoutDim()
        {
            float w = Screen.width, h = Screen.height;

            float left = Mathf.Clamp(_winLeftPx, 0f, w);
            float right = Mathf.Clamp(_winRightPx, 0f, w);
            if (right < left) { (left, right) = (right, left); }

            float topPx = Mathf.Clamp(WorldYToScreenY(_windowTopY), 0f, h);
            float botPx = Mathf.Clamp(WorldYToScreenY(_windowBottomY), 0f, h);
            if (topPx < botPx) { (topPx, botPx) = (botPx, topPx); }

            float bandH = topPx - botPx;
            SetRect(_left, 0f, botPx, left, bandH);
            SetRect(_right, right, botPx, w - right, bandH);
            SetRect(_top, 0f, topPx, w, h - topPx);
            SetRect(_bottom, 0f, 0f, w, botPx);

            SetRect(_railLeft, left - _railWidth * 0.5f, botPx, _railWidth, bandH);
            SetRect(_railRight, right - _railWidth * 0.5f, botPx, _railWidth, bandH);
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
