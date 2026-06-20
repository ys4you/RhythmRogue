using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.Core;
using RhythmRogue.Data;
using RhythmRogue.UI;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Shows the bound key for each lane next to its receptor, so a new player can see at a glance
    /// which key hits which lane. The label text is read from the live binding via KeybindManager,
    /// so it stays correct after the player rebinds, and the labels re-project every frame so they
    /// track an upscroll/downscroll flip.
    ///
    /// Rendered on its own screen-space overlay canvas using the shared pixel font (URP-safe UI
    /// shader, visually consistent with the menus). Positioned by projecting each lane's world
    /// position through the battle camera, then clamped inside the view so a receptor near the
    /// bottom in downscroll never pushes a label off-screen. Sibling component on the player
    /// highway, the same way ReceptorAnimator is; NoteHighway ensures one exists so no scene wiring
    /// is required.
    /// </summary>
    [RequireComponent(typeof(NoteHighway))]
    public class LaneKeyLabels : MonoBehaviour
    {
        [Tooltip("Hide the labels. The battle shows these only in practice (onboarding) and hides " +
                 "them in a normal run; this is the manual override for testing.")]
        [SerializeField] private bool _hidden;
        [Tooltip("World-unit gap from the receptor to the label, on the side away from incoming notes.")]
        [SerializeField] private float _gap = 0.7f;
        [Tooltip("Label font size, in the shared 1920x1080 UI space.")]
        [SerializeField] private int _fontSize = 34;
        [Tooltip("Keep labels at least this many screen pixels inside the view edges.")]
        [SerializeField] private float _screenMargin = 24f;

        private HighwayBase _highway;
        private Camera _camera;
        private RectTransform _canvasRT;
        private Text[] _labels;
        private bool _ready;

        private void Start()
        {
            _highway = GetComponent<HighwayBase>();
            // One frame later: the highway reads its receptor lane positions in Awake, and
            // Camera.main is reliably set by then.
            StartCoroutine(InitNextFrame());
        }

        private System.Collections.IEnumerator InitNextFrame()
        {
            yield return null;

            _camera = Camera.main;
            if (_highway == null || _highway.LanePositions == null ||
                _highway.LanePositions.Count < KeybindManager.LaneCount)
                yield break;

            _canvasRT = UIHelpers.CreateCanvas(null, "LaneKeyLabelCanvas", sortingOrder: 95, ensureEventSystem: false);
            _labels = new Text[KeybindManager.LaneCount];

            Color[] laneColors = { UIHelpers.LaneLeft, UIHelpers.LaneDown, UIHelpers.LaneUp, UIHelpers.LaneRight };
            for (int i = 0; i < _labels.Length; i++)
            {
                Text t = UIHelpers.MakeText(_canvasRT, $"LaneKey_{i}",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(72f, 56f), _fontSize, TextAnchor.MiddleCenter,
                    laneColors[i]);
                t.fontStyle = FontStyle.Bold;

                // Dark outline so the key reads against any lane color or background art.
                var outline = t.gameObject.AddComponent<Outline>();
                outline.effectColor = UIHelpers.BgDeep;
                outline.effectDistance = new Vector2(2f, -2f);

                _labels[i] = t;
            }

            _ready = true;
            Refresh();
        }

        /// <summary>Re-read the bound key for each lane. Call after a rebind; safe to call anytime.</summary>
        public void Refresh()
        {
            if (!_ready) return;
            for (int i = 0; i < _labels.Length; i++)
            {
                var indices = KeybindManager.GetKeyboardBindingIndices(i);
                _labels[i].text = indices.Count > 0
                    ? KeybindManager.GetBindingDisplayString(i, indices[0], shortNames: true)
                    : "?";
            }
        }

        /// <summary>Show or hide the labels. The battle gates these to practice (onboarding) only,
        /// since a returning player does not need per-lane key reminders during a real run.</summary>
        public void SetHidden(bool hidden) => _hidden = hidden;

        private void LateUpdate()
        {
            if (!_ready) return;

            bool visible = !_hidden;
            if (_canvasRT.gameObject.activeSelf != visible) _canvasRT.gameObject.SetActive(visible);
            if (!visible) return;

            if (_camera == null) { _camera = Camera.main; if (_camera == null) return; }

            // Place the label on the side of the receptor away from the incoming notes: below in
            // downscroll, above in upscroll. Then clamp into the view so a receptor near an edge
            // does not push the label off-screen.
            float dir = ScrollDirectionSetting.Downscroll ? -1f : 1f;
            float worldY = _highway.ReceptorY + dir * _gap;

            for (int i = 0; i < _labels.Length; i++)
            {
                Vector3 screen = _camera.WorldToScreenPoint(new Vector3(_highway.LanePositions[i], worldY, 0f));
                screen.y = Mathf.Clamp(screen.y, _screenMargin, Screen.height - _screenMargin);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRT, screen, null, out Vector2 local))
                    _labels[i].rectTransform.anchoredPosition = local;
            }
        }

        private void OnDestroy()
        {
            if (_canvasRT != null) Destroy(_canvasRT.gameObject);
        }
    }
}
