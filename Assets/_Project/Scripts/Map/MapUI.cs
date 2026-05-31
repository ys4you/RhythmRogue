using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RhythmRogue.UI;
using RhythmRogue.UI.Navigation;

namespace RhythmRogue.Map
{
    /// <summary>
    /// Renders the run map as a scrollable, vertically-progressing graph.
    /// Style targets: Slay the Spire 2 (scroll behaviour) + Inscryption (atmospheric look).
    ///
    /// Layout: layer 0 sits at the bottom of the content, the boss layer at the top.
    /// Content height grows with the number of layers; viewport scrolls vertically.
    ///
    /// Sibling order inside Content (back to front):
    ///   1. DecorationLayer - empty by default. Exposed as a public Transform so future
    ///      atmosphere sprites (trees, rocks, candles) can be parented here without
    ///      touching this class. Scrolls together with nodes and lines.
    ///   2. LineLayer - connection edges between nodes.
    ///   3. NodeLayer - interactive node buttons plus the player marker.
    ///
    /// The map auto-scrolls so the current node stays visible:
    ///   - On BuildMap: snaps to current node (or bottom if no current node).
    ///   - On UpdateVisuals (after the player advances): smooth-scrolls to the new current node.
    ///   - On keyboard focus change: smooth-scrolls to the focused node.
    /// Mouse wheel and drag scrolling still work normally via ScrollRect.
    /// </summary>
    public class MapUI : MonoBehaviour
    {
        [Header("Map Sizing")]
        [Tooltip("Vertical pixels between adjacent layers. Larger = more cinematic, more scrolling.")]
        [SerializeField] private float _layerSpacing = 280f;

        [Tooltip("Horizontal pixel range across which nodes are spread.")]
        [SerializeField] private float _mapWidth = 1500f;

        [SerializeField] private float _topPadding = 250f;
        [SerializeField] private float _bottomPadding = 250f;

        [Header("Node Sizes")]
        [SerializeField] private float _nodeSize = 100f;
        [SerializeField] private float _bossNodeSize = 130f;

        [Header("Scroll Behaviour")]
        [Tooltip("Where the focused node should appear inside the viewport. " +
                 "0 = at the bottom edge, 1 = at the top edge. " +
                 "Lower values leave more room above the player so upcoming nodes are visible.")]
        [SerializeField, Range(0f, 1f)] private float _focusViewportRatio = 0.4f;

        [Tooltip("Speed of the smooth scroll lerp. Higher = snappier.")]
        [SerializeField] private float _scrollLerpSpeed = 10f;

        [Header("Accessible Node Pulse")]
        [Tooltip("Rate of the glow pulse on accessible nodes (radians/second). Higher = faster heartbeat.")]
        [SerializeField] private float _glowPulseSpeed = 3f;
        [Tooltip("Minimum glow alpha during the pulse dip. 0 = invisible at the trough, 1 = no fade.")]
        [SerializeField, Range(0f, 1f)] private float _glowPulseMinAlpha = 0.4f;
        [Tooltip("How much the glow grows at the peak of the pulse. 0 = no scale, 0.1 = 10% larger.")]
        [SerializeField, Range(0f, 0.3f)] private float _glowPulseScale = 0.08f;

        [Header("Boss Node Pulse")]
        [Tooltip("Rate of the boss menace pulse (radians/second). Default is roughly half the accessible pulse so it reads as slower and heavier.")]
        [SerializeField] private float _bossPulseSpeed = 1.5f;
        [Tooltip("Minimum glow alpha for the boss pulse. Higher than accessible so the boss never disappears from view.")]
        [SerializeField, Range(0f, 1f)] private float _bossPulseMinAlpha = 0.5f;
        [Tooltip("Boss pulse scale growth at peak. Slightly more pronounced than accessible pulse for emphasis.")]
        [SerializeField, Range(0f, 0.3f)] private float _bossPulseScale = 0.1f;

        [Header("Player Marker Bob")]
        [Tooltip("How fast the player marker bobs up and down (radians/second). A full breath cycle takes 2*PI / speed seconds.")]
        [SerializeField] private float _markerBobSpeed = 2.5f;
        [Tooltip("How many pixels the marker travels above/below its rest position at the peak of the bob.")]
        [SerializeField] private float _markerBobAmplitude = 6f;

        [Header("Node Icons (32x32 sprites, optional)")]
        [Tooltip("If null, auto-loads from Resources/MapIcons/node_<type>. Falls back to emoji text if not found.")]
        [SerializeField] private Sprite _iconEnemy;
        [SerializeField] private Sprite _iconRest;
        [SerializeField] private Sprite _iconBoss;
        [SerializeField] private Sprite _iconElite;
        [SerializeField] private Sprite _iconShop;
        [SerializeField] private Sprite _iconEvent;

        public event Action<MapNode> OnNodeConfirmed;

        /// <summary>
        /// Transform under which future decorative sprites (trees, rocks, candles, etc.)
        /// should be parented. Lives behind the lines/nodes and scrolls with them.
        /// </summary>
        public Transform DecorationLayer => _decorLayer != null ? _decorLayer.transform : null;

        // Root canvas
        private Canvas _canvas;
        private RectTransform _canvasRT;

        // Scroll structure
        private ScrollRect _scrollRect;
        private RectTransform _viewportRT;
        private RectTransform _contentRT;

        // Content sibling layers (back to front)
        private GameObject _decorLayer;
        private RectTransform _lineLayerRT;
        private RectTransform _nodeLayerRT;

        // Map state
        private MapData _mapData;
        private RhythmRogue.Core.RunState _runState; // for currency HUD; set by MapScreen before BuildMap
        private readonly Dictionary<int, NodeVisual> _nodeVisuals = new();
        private readonly List<GameObject> _lineObjects = new();
        private MapNode _selectedNode;
        private float _contentHeight = 0f;

        // HUD + info
        private Text _seedText, _hpText, _currencyText;
        // HP bar uses direct RectTransform width scaling (same approach as BattleUI). The
        // fill panel has pivot (0, 0.5) so its sizeDelta.x is the literal rendered width.
        // No Image.fillAmount, no ghost trail - just a clean bar that shrinks visibly.
        private RectTransform _hpFillRT;
        private Image _hpFill;
        private float _hpBarMaxWidth;
        private float _hpFillTarget = 1f, _hpFillDisplay = 1f;
        private GameObject _infoPanel;
        private Text _infoTitle, _infoSub;
        private Button _confirmButton;
        private Image _playerMarker;

        // Navigation helpers
        private UIFocusSetter _focusSetter;
        private UICancelHandler _cancelHandler;

        // Scroll animation
        private float _targetNormalizedY = 0f;
        private bool _scrollAnimating = false;
        private GameObject _lastSelectedForScroll;

        // Glow pulse animation. Continuous timer; only nodes flagged PulseGlow react.
        private float _pulseTime = 0f;
        // Independent timer for the boss menace pulse so its slower rate can't be
        // derived from the accessible timer (avoids beat-locking the two animations).
        private float _bossPulseTime = 0f;

        // Player marker bob animation. _markerBasePos is the "rest" position under the
        // current node, set by UpdatePlayerMarker. The per-frame Update adds a sine Y
        // offset on top, so the marker visibly floats without drifting from its anchor.
        private Vector2 _markerBasePos = Vector2.zero;
        private bool _hasMarkerBase = false;
        private float _markerBobTime = 0f;

        private static Color EnemyColor => UIHelpers.RustOrange;
        private static Color RestColor => UIHelpers.WarmGold;
        private static Color BossColor => UIHelpers.RustOrange;
        private static Color EliteColor => UIHelpers.BgLight;
        private static Color ShopColor => UIHelpers.AmberOrange;
        private static Color EventColor => UIHelpers.BgLight;
        private static Color LockedColor => UIHelpers.BgSurface;
        private static Color CompletedColor => UIHelpers.Shadow;
        private static Color AccessibleGlow => UIHelpers.WarmGold;
        // Boss menace pulse uses the darkest warm-palette tone (RustOrange). It reads as
        // ominous within the palette - separate from the gold accessible glow - without
        // breaking the colour cohesion the way a true red would. The slower pulse rate and
        // larger scale do most of the work; the colour just supports it.
        private static Color BossGlow => UIHelpers.RustOrange;

        private void Awake()
        {
            // Auto-load sprites from Resources/MapIcons/ if not assigned in Inspector
            if (_iconEnemy == null) _iconEnemy = Resources.Load<Sprite>("MapIcons/node_enemy");
            if (_iconRest == null) _iconRest = Resources.Load<Sprite>("MapIcons/node_rest");
            if (_iconBoss == null) _iconBoss = Resources.Load<Sprite>("MapIcons/node_boss");
            if (_iconElite == null) _iconElite = Resources.Load<Sprite>("MapIcons/node_elite");
            if (_iconShop == null) _iconShop = Resources.Load<Sprite>("MapIcons/node_shop");
            if (_iconEvent == null) _iconEvent = Resources.Load<Sprite>("MapIcons/node_event");
        }

        private void Update()
        {
            // Scroll-follow keyboard focus: when the EventSystem switches selection to
            // one of our node buttons, smooth-scroll it into view.
            HandleSelectionAutoScroll();

            // Drive the smooth scroll lerp toward _targetNormalizedY if active.
            HandleSmoothScroll();

            // Pulse accessible node glows so the player can see at a glance what's clickable.
            UpdateGlowPulse(Time.unscaledDeltaTime);

            // Float the player marker so it reads as a character rather than static UI.
            UpdateMarkerBob(Time.unscaledDeltaTime);

            // Lerp the HP bar fill toward target so damage/healing animates rather than snapping.
            UpdateHPBar(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Provide the RunState so the HUD can show the run currency. Optional: if not set,
        /// the currency readout simply doesn't appear. Call before BuildMap.
        /// </summary>
        public void SetRunState(RhythmRogue.Core.RunState runState)
        {
            _runState = runState;
        }

        public void BuildMap(MapData mapData)
        {
            _mapData = mapData;
            ClearMap();
            CreateCanvas();
            CreateScrollArea();
            CreateHUD();
            CreateLines();
            CreateNodes();
            CreatePlayerMarker();
            CreateInfoPanel();
            SetupNavigationComponents();
            UpdateVisuals();

            // Snap straight to the current node on first build (no smooth scroll).
            ScrollToCurrentNode(instant: true);
        }

        public void UpdateVisuals()
        {
            if (_mapData == null) return;
            foreach (var kvp in _nodeVisuals)
            {
                var node = FindNode(kvp.Key);
                if (node != null) UpdateNodeVisual(node, kvp.Value);
            }
            UpdatePlayerMarker();
            UpdateHUD();
            if (_infoPanel != null) _infoPanel.SetActive(false);
            _selectedNode = null;
            RebuildNavigation();

            // Smooth-pan to the new current node after the player advances.
            ScrollToCurrentNode(instant: false);
        }

        private void SetupNavigationComponents()
        {
            _focusSetter = gameObject.GetComponent<UIFocusSetter>() ?? gameObject.AddComponent<UIFocusSetter>();
            _cancelHandler = gameObject.GetComponent<UICancelHandler>() ?? gameObject.AddComponent<UICancelHandler>();
            _cancelHandler.ClearStack();
        }

        private void RebuildNavigation()
        {
            var entries = new List<MapNavigationBuilder.NodeEntry>();
            foreach (var kvp in _nodeVisuals)
            {
                var node = FindNode(kvp.Key);
                if (node == null) continue;
                bool nav = node.IsAccessible && !node.IsCompleted;
                entries.Add(new MapNavigationBuilder.NodeEntry { Selectable = kvp.Value.Button, Layer = node.Layer, Column = node.Column, IsAccessible = nav });
                if (nav) UISelectableStyle.Apply(kvp.Value.Button);
            }
            Selectable first = MapNavigationBuilder.Build(entries);
            if (first != null && _focusSetter != null) _focusSetter.SetDefault(first.gameObject);
        }

        private void CreateCanvas()
        {
            var canvasGO = new GameObject("MapCanvas");
            canvasGO.transform.SetParent(transform);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            _canvasRT = canvasGO.GetComponent<RectTransform>();
            UIEventSystemProvider.EnsureEventSystem();
        }

        /// <summary>
        /// Builds the ScrollRect + Viewport + Content + three sibling layers.
        /// Content height is computed from the layer count so deeper maps automatically
        /// produce more scrollable space. Layer count is read from _mapData.
        /// </summary>
        private void CreateScrollArea()
        {
            // ScrollView root: fills the canvas but leaves room at top/bottom for HUD.
            var scrollGO = new GameObject("ScrollView",
                typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            scrollGO.transform.SetParent(_canvasRT, false);
            var scrollRT = scrollGO.GetComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(0, 80);   // leave space for bottom HUD
            scrollRT.offsetMax = new Vector2(0, -80);  // leave space for top HUD
            scrollGO.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f); // near-transparent, gives the rect mask a graphic

            _scrollRect = scrollGO.GetComponent<ScrollRect>();
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.inertia = true;
            _scrollRect.scrollSensitivity = 30f;

            // Viewport: same rect as ScrollView, but separate object as ScrollRect.viewport.
            var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer));
            viewportGO.transform.SetParent(scrollGO.transform, false);
            _viewportRT = viewportGO.GetComponent<RectTransform>();
            _viewportRT.anchorMin = Vector2.zero;
            _viewportRT.anchorMax = Vector2.one;
            _viewportRT.offsetMin = Vector2.zero;
            _viewportRT.offsetMax = Vector2.zero;
            _scrollRect.viewport = _viewportRT;

            // Content: anchored bottom-center of viewport, pivot bottom-center.
            // Children use anchorMin/Max (0.5, 0) so their y is measured up from the bottom.
            int layerCount = _mapData != null ? _mapData.Layers.Count : 1;
            float computedHeight = _topPadding + _bottomPadding + Mathf.Max(0, layerCount - 1) * _layerSpacing;
            _contentHeight = Mathf.Max(computedHeight, 1080f);

            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            _contentRT = contentGO.GetComponent<RectTransform>();
            _contentRT.anchorMin = new Vector2(0.5f, 0);
            _contentRT.anchorMax = new Vector2(0.5f, 0);
            _contentRT.pivot = new Vector2(0.5f, 0);
            _contentRT.anchoredPosition = Vector2.zero;
            _contentRT.sizeDelta = new Vector2(_mapWidth, _contentHeight);
            _scrollRect.content = _contentRT;

            // Three child layers. Order matters: first child is back-most.
            _decorLayer = CreateContentLayer("DecorationLayer");
            _lineLayerRT = CreateContentLayer("LineLayer").GetComponent<RectTransform>();
            _nodeLayerRT = CreateContentLayer("NodeLayer").GetComponent<RectTransform>();
        }

        private GameObject CreateContentLayer(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_contentRT, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go;
        }

        private void CreateHUD()
        {
            // Seed text: top-left of canvas (outside scroll area).
            _seedText = MakeText(_canvasRT, "SeedText", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(50, -50), new Vector2(500, 50), 22, TextAnchor.MiddleLeft, UIHelpers.Shadow);
            _seedText.text = $"Seed: {_mapData.Seed}";

            // HP HUD: bar + number, bottom-left corner of the screen (outside scroll area).
            // Layout: number text sits above the bar so it's readable; bar uses direct width
            // scaling for a clean 'traditional HP bar' shrink animation.
            const float hpBarWidth = 400f;
            const float hpBarHeight = 28f;
            const float hpBarX = 50f;
            const float hpBarY = 50f;
            const float pad = 2f;

            // Bar background (dark surface, slightly transparent).
            var bgGO = MakePanel(_canvasRT, "HPBarBG",
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(hpBarX, hpBarY), new Vector2(hpBarWidth, hpBarHeight),
                new Color(UIHelpers.BgSurface.r, UIHelpers.BgSurface.g, UIHelpers.BgSurface.b, 0.85f));
            var bgRT = bgGO.GetComponent<RectTransform>();

            // Fill: anchored top-left + bottom-left of the background with pivot (0, 0.5).
            // sizeDelta.x is the rendered pixel width. UpdateHPBar resizes this each frame.
            _hpBarMaxWidth = hpBarWidth - pad * 2f;
            var fillGO = new GameObject("HPFill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(bgRT, false);
            _hpFillRT = fillGO.GetComponent<RectTransform>();
            _hpFillRT.anchorMin = new Vector2(0, 0.5f);
            _hpFillRT.anchorMax = new Vector2(0, 0.5f);
            _hpFillRT.pivot = new Vector2(0, 0.5f);
            _hpFillRT.anchoredPosition = new Vector2(pad, 0);
            _hpFillRT.sizeDelta = new Vector2(_hpBarMaxWidth, hpBarHeight - pad * 2f);
            _hpFill = fillGO.GetComponent<Image>();
            _hpFill.color = UIHelpers.WarmGold;

            // HP number text: sits above the bar.
            _hpText = MakeText(_canvasRT, "HPText", new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(hpBarX, hpBarY + hpBarHeight + 4f), new Vector2(hpBarWidth, 40f),
                24, TextAnchor.MiddleLeft, UIHelpers.OffWhite);

            // Currency readout: top-right of canvas, mirroring the seed text top-left.
            // Shown only if a RunState was provided. Uses the configurable currency name.
            _currencyText = MakeText(_canvasRT, "CurrencyText", new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-50, -50), new Vector2(500, 50), 26, TextAnchor.MiddleRight, UIHelpers.WarmGold);
            _currencyText.fontStyle = FontStyle.Bold;
            UpdateCurrencyText();
        }

        /// <summary>
        /// Render the current currency using the configurable name from EconomyConfig
        /// (e.g. "Beats: 120"). Hidden if no RunState was supplied.
        /// </summary>
        private void UpdateCurrencyText()
        {
            if (_currencyText == null) return;
            if (_runState == null)
            {
                _currencyText.gameObject.SetActive(false);
                return;
            }
            _currencyText.gameObject.SetActive(true);
            string name = _runState.Economy != null ? _runState.Economy.CurrencyName : "Beats";
            _currencyText.text = $"{name}: {_runState.Currency}";
        }

        private void UpdateHUD()
        {
            var ph = Battle.PlayerHealth.Instance;
            if (ph == null || _hpText == null) return;

            float pct = ph.HPPercent;
            _hpFillTarget = pct;

            Color c = UIHelpers.HPColor(pct);
            string hex = ColorUtility.ToHtmlStringRGB(c);
            _hpText.text = $"HP <color=#{hex}>{ph.CurrentHP}/{ph.MaxHP}</color>";

            // Currency can change between map visits (earned in battle), so refresh it here too.
            UpdateCurrencyText();
        }

        /// <summary>
        /// Per-frame lerp for the HP bar. Direct width scaling on the fill RectTransform
        /// (no Image.fillAmount). Colour is recomputed each frame from the displayed value
        /// so the bar reads correctly mid-animation.
        /// </summary>
        private void UpdateHPBar(float dt)
        {
            if (_hpFill == null || _hpFillRT == null) return;
            _hpFillDisplay = Mathf.Lerp(_hpFillDisplay, _hpFillTarget, dt * 8f);
            float w = Mathf.Max(0f, _hpBarMaxWidth * Mathf.Clamp01(_hpFillDisplay));
            var sd = _hpFillRT.sizeDelta;
            _hpFillRT.sizeDelta = new Vector2(w, sd.y);
            _hpFill.color = UIHelpers.HPColor(_hpFillDisplay);
        }

        /// <summary>Helper: create a uniformly-coloured RectTransform panel.</summary>
        private static GameObject MakePanel(RectTransform parent, string name, Vector2 ancMin, Vector2 ancMax, Vector2 pivot,
            Vector2 pos, Vector2 size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = ancMin; rt.anchorMax = ancMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            obj.GetComponent<Image>().color = color;
            return obj;
        }

        private void CreateLines()
        {
            foreach (var node in _mapData.AllNodes)
            {
                Vector2 from = NodeToContent(node.Position);
                foreach (var conn in node.Connections)
                    CreateLine(from, NodeToContent(conn.Position), node.IsCompleted);
            }
        }

        private void CreateLine(Vector2 from, Vector2 to, bool completed)
        {
            var lineGO = new GameObject("Line", typeof(RectTransform), typeof(Image));
            lineGO.transform.SetParent(_lineLayerRT, false);
            Color lineColor = completed
                ? new Color(UIHelpers.AmberOrange.r, UIHelpers.AmberOrange.g, UIHelpers.AmberOrange.b, 0.8f)
                : new Color(UIHelpers.Shadow.r, UIHelpers.Shadow.g, UIHelpers.Shadow.b, 0.6f);
            lineGO.GetComponent<Image>().color = lineColor;
            var rt = lineGO.GetComponent<RectTransform>();
            // Anchored to bottom-center of LineLayer (which fills Content), so positions
            // line up with NodeToContent output.
            rt.anchorMin = new Vector2(0.5f, 0);
            rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0, 0.5f);
            Vector2 diff = to - from;
            rt.anchoredPosition = from;
            rt.sizeDelta = new Vector2(diff.magnitude, 4f);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg);
            _lineObjects.Add(lineGO);
        }

        private void CreateNodes()
        {
            foreach (var node in _mapData.AllNodes)
                _nodeVisuals[node.Id] = CreateNodeVisual(node);
        }

        private NodeVisual CreateNodeVisual(MapNode node)
        {
            float size = node.Type == NodeType.Boss ? _bossNodeSize : _nodeSize;
            var rootGO = new GameObject($"Node_{node.Id}", typeof(RectTransform), typeof(Image), typeof(Button));
            rootGO.transform.SetParent(_nodeLayerRT, false);
            var rt = rootGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0);
            rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = NodeToContent(node.Position);
            rt.sizeDelta = new Vector2(size, size);
            Image bg = rootGO.GetComponent<Image>(); bg.color = GetNodeColor(node);
            Button btn = rootGO.GetComponent<Button>();
            int nodeId = node.Id; btn.onClick.AddListener(() => OnNodeClicked(nodeId));

            Sprite icon = GetNodeSprite(node.Type);
            Image iconImage = null;
            Text iconLabel = null;

            if (icon != null)
            {
                var iconGO = new GameObject("IconSprite", typeof(RectTransform), typeof(Image));
                iconGO.transform.SetParent(rootGO.transform, false);
                var iconRT = iconGO.GetComponent<RectTransform>();
                iconRT.anchorMin = iconRT.anchorMax = new Vector2(0.5f, 0.5f);
                iconRT.pivot = new Vector2(0.5f, 0.5f);
                iconRT.anchoredPosition = Vector2.zero;
                float iconSize = size * 2.0f;
                iconRT.sizeDelta = new Vector2(iconSize, iconSize);
                iconImage = iconGO.GetComponent<Image>();
                iconImage.sprite = icon;
                iconImage.color = UIHelpers.OffWhite;
                iconImage.raycastTarget = false;
                iconImage.preserveAspect = true;
            }
            else
            {
                iconLabel = MakeText(rt, "Label", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(size + 20, size),
                    node.Type == NodeType.Boss ? 36 : 30, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
                iconLabel.text = GetNodeIconEmoji(node.Type);
                iconLabel.fontStyle = FontStyle.Bold;
            }

            var glowGO = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            glowGO.transform.SetParent(rootGO.transform, false);
            var glowRT = glowGO.GetComponent<RectTransform>();
            glowRT.anchorMin = Vector2.zero; glowRT.anchorMax = Vector2.one;
            glowRT.offsetMin = new Vector2(-10, -10); glowRT.offsetMax = new Vector2(10, 10);
            glowRT.SetAsFirstSibling();
            Image glow = glowGO.GetComponent<Image>(); glow.color = new Color(0, 0, 0, 0);

            Text check = MakeText(rt, "Check", new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(10, 10), new Vector2(50, 50), 26, TextAnchor.MiddleCenter, UIHelpers.WarmGold);
            check.text = "";

            return new NodeVisual { Root = rootGO, Background = bg, IconImage = iconImage, IconLabel = iconLabel, Glow = glow, GlowRT = glowRT, Checkmark = check, Button = btn, IsBoss = node.Type == NodeType.Boss };
        }

        private void UpdateNodeVisual(MapNode node, NodeVisual vis)
        {
            if (node.IsCompleted)
            {
                vis.Background.color = CompletedColor;
                vis.Glow.color = new Color(0, 0, 0, 0);
                vis.Checkmark.text = "✓";
                vis.Button.interactable = false;
                vis.PulseGlow = false;
                if (vis.GlowRT != null) vis.GlowRT.localScale = Vector3.one;
                if (vis.IconImage != null) vis.IconImage.color = new Color(UIHelpers.OffWhite.r, UIHelpers.OffWhite.g, UIHelpers.OffWhite.b, 0.4f);
                if (vis.IconLabel != null) vis.IconLabel.color = new Color(UIHelpers.OffWhite.r, UIHelpers.OffWhite.g, UIHelpers.OffWhite.b, 0.4f);
            }
            else if (node.IsAccessible)
            {
                vis.Background.color = GetNodeColor(node);
                vis.Glow.color = vis.IsBoss ? BossGlow : AccessibleGlow;
                vis.Checkmark.text = "";
                vis.Button.interactable = true;
                vis.PulseGlow = true;
                if (vis.IconImage != null) vis.IconImage.color = UIHelpers.OffWhite;
                if (vis.IconLabel != null) vis.IconLabel.color = UIHelpers.OffWhite;
            }
            else
            {
                // Locked. Boss is an exception: it keeps pulsing red even while locked so the
                // player sees "the end of this path" looming from the moment the map loads.
                if (vis.IsBoss)
                {
                    vis.Background.color = LockedColor;
                    vis.Glow.color = BossGlow;
                    vis.Checkmark.text = "";
                    vis.Button.interactable = false;
                    vis.PulseGlow = true;
                    // Icon kept a touch dim so it still reads as locked, but more visible than
                    // other locked nodes (0.7 vs 0.5) so the boss silhouette is recognisable.
                    if (vis.IconImage != null) vis.IconImage.color = new Color(UIHelpers.OffWhite.r, UIHelpers.OffWhite.g, UIHelpers.OffWhite.b, 0.7f);
                    if (vis.IconLabel != null) vis.IconLabel.color = new Color(UIHelpers.OffWhite.r, UIHelpers.OffWhite.g, UIHelpers.OffWhite.b, 0.7f);
                }
                else
                {
                    vis.Background.color = LockedColor;
                    vis.Glow.color = new Color(0, 0, 0, 0);
                    vis.Checkmark.text = "";
                    vis.Button.interactable = false;
                    vis.PulseGlow = false;
                    if (vis.GlowRT != null) vis.GlowRT.localScale = Vector3.one;
                    if (vis.IconImage != null) vis.IconImage.color = new Color(UIHelpers.OffWhite.r, UIHelpers.OffWhite.g, UIHelpers.OffWhite.b, 0.5f);
                    if (vis.IconLabel != null) vis.IconLabel.color = new Color(UIHelpers.OffWhite.r, UIHelpers.OffWhite.g, UIHelpers.OffWhite.b, 0.5f);
                }
            }
        }

        private void CreatePlayerMarker()
        {
            var markerGO = new GameObject("PlayerMarker", typeof(RectTransform), typeof(Image));
            markerGO.transform.SetParent(_nodeLayerRT, false);
            var rt = markerGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0);
            rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(30, 30);
            _playerMarker = markerGO.GetComponent<Image>();
            _playerMarker.color = UIHelpers.WarmGold;
            UpdatePlayerMarker();
        }

        private void UpdatePlayerMarker()
        {
            if (_playerMarker == null) return;
            if (_mapData.CurrentNode != null)
            {
                Vector2 pos = NodeToContent(_mapData.CurrentNode.Position);
                _markerBasePos = pos + new Vector2(0, -(_nodeSize * 0.5f + 25f));
                _hasMarkerBase = true;
                _playerMarker.gameObject.SetActive(true);
            }
            else
            {
                // No node selected yet (start of run): park the marker just under layer 0.
                if (_mapData.Layers.Count > 0 && _mapData.Layers[0].Count > 0)
                {
                    Vector2 pos = NodeToContent(_mapData.Layers[0][0].Position);
                    _markerBasePos = pos + new Vector2(0, -(_nodeSize * 0.5f + 50f));
                }
                else
                {
                    _markerBasePos = new Vector2(0, _bottomPadding * 0.5f);
                }
                _hasMarkerBase = true;
                _playerMarker.gameObject.SetActive(true);
            }
            // Snap to base immediately so the marker doesn't sit at (0,0) for a frame on
            // first build before Update applies the bob.
            _playerMarker.rectTransform.anchoredPosition = _markerBasePos;
        }

        /// <summary>
        /// Adds a vertical sine offset to the player marker each frame so it gently bobs.
        /// The base position is set by UpdatePlayerMarker; this only adds the wave on top,
        /// so changing nodes (which moves the base) doesn't reset the bob phase. Uses
        /// unscaled time so the animation continues even if Time.timeScale is 0.
        /// </summary>
        private void UpdateMarkerBob(float dt)
        {
            if (_playerMarker == null || !_hasMarkerBase) return;
            _markerBobTime += dt * _markerBobSpeed;
            float yOffset = Mathf.Sin(_markerBobTime) * _markerBobAmplitude;
            _playerMarker.rectTransform.anchoredPosition = _markerBasePos + new Vector2(0, yOffset);
        }

        private void CreateInfoPanel()
        {
            // Lives on the canvas root (outside the scroll area) so it stays anchored
            // at the bottom of the screen even while the map scrolls.
            _infoPanel = new GameObject("InfoPanel", typeof(RectTransform), typeof(Image));
            _infoPanel.transform.SetParent(_canvasRT, false);
            var rt = _infoPanel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = new Vector2(0, 100);
            rt.sizeDelta = new Vector2(700, 180);
            _infoPanel.GetComponent<Image>().color = new Color(UIHelpers.BgSurface.r, UIHelpers.BgSurface.g, UIHelpers.BgSurface.b, 0.95f);

            _infoTitle = MakeText(rt, "Title", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -20), new Vector2(650, 60), 32, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
            _infoTitle.fontStyle = FontStyle.Bold;

            _infoSub = MakeText(rt, "Sub", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -75), new Vector2(650, 50), 22, TextAnchor.MiddleCenter, UIHelpers.AmberOrange);

            var btnGO = new GameObject("ConfirmBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(rt, false);
            var btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.anchorMin = btnRT.anchorMax = new Vector2(0.5f, 0);
            btnRT.pivot = new Vector2(0.5f, 0);
            btnRT.anchoredPosition = new Vector2(0, 10);
            btnRT.sizeDelta = new Vector2(250, 60);
            btnGO.GetComponent<Image>().color = UIHelpers.RustOrange;

            Text btnText = MakeText(btnRT, "BtnText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(250, 60), 26, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
            btnText.text = "Enter";

            _confirmButton = btnGO.GetComponent<Button>();
            _confirmButton.onClick.AddListener(OnConfirmClicked);
            UISelectableStyle.Apply(_confirmButton);
            _infoPanel.SetActive(false);
        }

        private void OnNodeClicked(int nodeId)
        {
            var node = FindNode(nodeId);
            if (node == null || !node.IsAccessible || node.IsCompleted) return;
            _selectedNode = node;
            _infoTitle.text = GetNodeTitle(node);
            _infoSub.text = GetNodeSubtitle(node);
            _infoPanel.SetActive(true);
            foreach (var kvp in _nodeVisuals)
            {
                var n = FindNode(kvp.Key);
                if (n != null && n.IsAccessible && !n.IsCompleted)
                {
                    bool isClicked = (n.Id == nodeId);
                    Color baseGlow = kvp.Value.IsBoss ? BossGlow : AccessibleGlow;
                    kvp.Value.Glow.color = isClicked ? UIHelpers.OffWhite : baseGlow;
                    // The clicked node holds steady at full intensity so it reads as the focus
                    // target; other accessible nodes keep pulsing in the background.
                    kvp.Value.PulseGlow = !isClicked;
                    if (isClicked && kvp.Value.GlowRT != null) kvp.Value.GlowRT.localScale = Vector3.one;
                }
            }
            if (_nodeVisuals.TryGetValue(nodeId, out var vis)) MapNavigationBuilder.WireInfoPanel(vis.Button, _confirmButton);
            _cancelHandler.Push(() => CloseInfoPanel(nodeId));
            _focusSetter.FocusOn(_confirmButton.gameObject);
        }

        private void CloseInfoPanel(int nodeIdToRestore)
        {
            _infoPanel.SetActive(false); _selectedNode = null;
            foreach (var kvp in _nodeVisuals)
            {
                var n = FindNode(kvp.Key);
                if (n != null && n.IsAccessible && !n.IsCompleted)
                {
                    kvp.Value.Glow.color = kvp.Value.IsBoss ? BossGlow : AccessibleGlow;
                    kvp.Value.PulseGlow = true;
                }
            }
            RebuildNavigation();
            if (_nodeVisuals.TryGetValue(nodeIdToRestore, out var vis) && vis.Button.IsInteractable())
                _focusSetter.FocusOn(vis.Button.gameObject);
        }

        private void OnConfirmClicked()
        {
            if (_selectedNode == null) return;
            _cancelHandler.Pop();
            OnNodeConfirmed?.Invoke(_selectedNode);
        }

        // ============================================================
        // Scrolling
        // ============================================================

        /// <summary>
        /// Convert a node's normalised (0..1, 0..1) position into Content-local pixel coords.
        /// X = horizontal spread (centered on Content's middle column).
        /// Y = vertical position, measured from the bottom of Content.
        /// </summary>
        private Vector2 NodeToContent(Vector2 normalized)
        {
            float x = (normalized.x - 0.5f) * _mapWidth;
            float mapAreaHeight = _contentHeight - _topPadding - _bottomPadding;
            float y = _bottomPadding + normalized.y * mapAreaHeight;
            return new Vector2(x, y);
        }

        private void ScrollToCurrentNode(bool instant)
        {
            MapNode target = _mapData.CurrentNode;
            if (target == null && _mapData.Layers.Count > 0 && _mapData.Layers[0].Count > 0)
                target = _mapData.Layers[0][0];
            if (target != null) ScrollToNode(target, instant);
        }

        /// <summary>
        /// Smooth-scroll (or snap) the view so the given node sits at the configured
        /// vertical ratio inside the viewport. Mouse wheel / drag input can override
        /// the animation just by interrupting it - the user's input wins.
        /// </summary>
        private void ScrollToNode(MapNode node, bool instant)
        {
            if (node == null || _scrollRect == null || _contentRT == null || _viewportRT == null) return;

            float vh = _viewportRT.rect.height;
            float ch = _contentRT.rect.height;
            if (ch <= vh) return; // Map fits entirely; no scrolling needed.

            Vector2 contentPos = NodeToContent(node.Position);

            // Target window bottom in content coords: place the node at vh * ratio above the
            // viewport's bottom edge. Inverting gives the window bottom we need.
            float windowBottom = contentPos.y - vh * _focusViewportRatio;
            float normalized = Mathf.Clamp01(windowBottom / (ch - vh));

            _targetNormalizedY = normalized;
            if (instant)
            {
                _scrollRect.verticalNormalizedPosition = _targetNormalizedY;
                _scrollAnimating = false;
            }
            else
            {
                _scrollAnimating = true;
            }
        }

        private void HandleSmoothScroll()
        {
            if (!_scrollAnimating || _scrollRect == null) return;
            float current = _scrollRect.verticalNormalizedPosition;
            float next = Mathf.Lerp(current, _targetNormalizedY, Time.unscaledDeltaTime * _scrollLerpSpeed);
            _scrollRect.verticalNormalizedPosition = next;
            if (Mathf.Abs(next - _targetNormalizedY) < 0.001f)
            {
                _scrollRect.verticalNormalizedPosition = _targetNormalizedY;
                _scrollAnimating = false;
            }
        }

        private void HandleSelectionAutoScroll()
        {
            if (_scrollRect == null || _nodeVisuals.Count == 0) return;
            var es = EventSystem.current;
            if (es == null) return;
            var selected = es.currentSelectedGameObject;
            if (selected == _lastSelectedForScroll) return;
            _lastSelectedForScroll = selected;
            if (selected == null) return;

            // Match the selected GameObject against our node roots. Cheap because
            // map node counts are small (< 30).
            foreach (var kvp in _nodeVisuals)
            {
                if (kvp.Value.Root == selected)
                {
                    var node = FindNode(kvp.Key);
                    if (node != null) ScrollToNode(node, instant: false);
                    return;
                }
            }
        }

        /// <summary>
        /// Per-frame pulse for accessible node glows. Accessible nodes share one sine timer
        /// so they breathe in unison; the boss runs on a separate, slower timer with a red
        /// colour so it stands apart. Timers aren't multiples of each other on purpose, so
        /// the two pulses drift against each other instead of beat-locking.
        ///
        /// Cheap: O(n) over node count, no allocations. With < 30 nodes per map this is
        /// well below any noticeable cost.
        /// </summary>
        private void UpdateGlowPulse(float dt)
        {
            if (_nodeVisuals.Count == 0) return;
            _pulseTime += dt * _glowPulseSpeed;
            _bossPulseTime += dt * _bossPulseSpeed;

            // Sine waves shifted to [0, 1] for each timer.
            float t = (Mathf.Sin(_pulseTime) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(_glowPulseMinAlpha, 1f, t);
            float scale = 1f + _glowPulseScale * t;

            float bossT = (Mathf.Sin(_bossPulseTime) + 1f) * 0.5f;
            float bossAlpha = Mathf.Lerp(_bossPulseMinAlpha, 1f, bossT);
            float bossScale = 1f + _bossPulseScale * bossT;

            foreach (var kvp in _nodeVisuals)
            {
                var vis = kvp.Value;
                if (!vis.PulseGlow) continue;

                if (vis.IsBoss)
                {
                    Color c = BossGlow;
                    c.a = bossAlpha;
                    vis.Glow.color = c;
                    if (vis.GlowRT != null) vis.GlowRT.localScale = new Vector3(bossScale, bossScale, 1f);
                }
                else
                {
                    Color c = vis.Glow.color;
                    c.a = alpha;
                    vis.Glow.color = c;
                    if (vis.GlowRT != null) vis.GlowRT.localScale = new Vector3(scale, scale, 1f);
                }
            }
        }

        // ============================================================
        // Helpers
        // ============================================================

        private Sprite GetNodeSprite(NodeType type) => type switch
        {
            NodeType.Enemy => _iconEnemy,
            NodeType.Rest => _iconRest,
            NodeType.Boss => _iconBoss,
            NodeType.Elite => _iconElite,
            NodeType.Shop => _iconShop,
            NodeType.Event => _iconEvent,
            _ => null
        };

        private static Color GetNodeColor(MapNode node) => node.Type switch
        {
            NodeType.Enemy => EnemyColor,
            NodeType.Rest => RestColor,
            NodeType.Boss => BossColor,
            NodeType.Elite => EliteColor,
            NodeType.Shop => ShopColor,
            NodeType.Event => EventColor,
            _ => EnemyColor
        };

        private static string GetNodeIconEmoji(NodeType type) => type switch
        {
            NodeType.Enemy => "⚔", NodeType.Rest => "♥", NodeType.Boss => "☠",
            NodeType.Elite => "★", NodeType.Shop => "$", NodeType.Event => "?", _ => "?"
        };

        // Node titles and subtitles deliberately hide the enemy's identity and stats.
        // The roguelike tension comes from committing to a fight without knowing what waits;
        // discovery happens on the battle screen, not on the map. Only node type and broad
        // hints (danger, reward) are surfaced here.
        private static string GetNodeTitle(MapNode node) => node.Type switch
        {
            NodeType.Enemy => "Battle",
            NodeType.Elite => "Elite Battle",
            NodeType.Boss => "Boss Battle",
            NodeType.Rest => "Rest",
            NodeType.Shop => "Merchant",
            NodeType.Event => "Unknown",
            _ => node.Type.ToString()
        };

        private static string GetNodeSubtitle(MapNode node) => node.Type switch
        {
            NodeType.Enemy => "Something lurks here.",
            NodeType.Elite => "A stronger foe. Better rewards.",
            NodeType.Boss => "The end of this path.",
            NodeType.Rest => "Heal 30% HP",
            NodeType.Shop => "Spend Beats on relics and curiosities.",
            NodeType.Event => "Anything could happen.",
            _ => ""
        };

        private MapNode FindNode(int id) { foreach (var n in _mapData.AllNodes) if (n.Id == id) return n; return null; }

        private void ClearMap()
        {
            _nodeVisuals.Clear();
            _lineObjects.Clear();
            _selectedNode = null;
            _scrollAnimating = false;
            _lastSelectedForScroll = null;
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        private static Text MakeText(RectTransform parent, string name, Vector2 ancMin, Vector2 ancMax, Vector2 pivot,
            Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = ancMin; rt.anchorMax = ancMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = obj.GetComponent<Text>();
            t.font = UIHelpers.GetDefaultFont(fontSize); t.fontSize = fontSize;
            t.alignment = align; t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            return t;
        }

        private void OnDestroy() { OnNodeConfirmed = null; }

        private class NodeVisual
        {
            public GameObject Root; public Image Background;
            public Image IconImage; public Text IconLabel;
            public Image Glow; public RectTransform GlowRT;
            public Text Checkmark; public Button Button;
            public bool PulseGlow; // True when accessible (or boss-locked) and not currently the clicked focus.
            public bool IsBoss;    // True for the boss node. Drives red pulse + 'always visible' locked behaviour.
        }
    }
}
