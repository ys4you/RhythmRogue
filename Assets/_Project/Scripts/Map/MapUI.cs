using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.UI.Navigation;

namespace RhythmRogue.Map
{
    /// <summary>
    /// Renders the run map as a Canvas UI.
    /// 
    /// Takes MapData from the generator and creates:
    ///   - Clickable node icons with type-specific colors/labels
    ///   - Connection lines between nodes
    ///   - Player position marker
    ///   - HP display and seed text
    ///   - Node info panel on selection
    /// 
    /// Fully keyboard/gamepad navigable:
    ///   - Up/Down moves between map layers
    ///   - Left/Right moves within a layer
    ///   - Enter opens info panel → Enter again confirms node
    ///   - Escape closes info panel and restores node focus
    /// 
    /// Inscryption/Die in the Dungeon style: vertical layout,
    /// bottom (start) to top (boss), branching paths.
    /// 
    /// All created in code — no prefabs needed for prototype.
    /// Sized for 384×216 reference resolution.
    /// </summary>
    public class MapUI : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("Map Area")]
        [Tooltip("Padding from screen edges in canvas units.")]
        [SerializeField] private float _mapPadding = 30f;

        [Tooltip("Vertical padding — extra space at bottom for HP, top for seed.")]
        [SerializeField] private float _mapPaddingBottom = 24f;
        [SerializeField] private float _mapPaddingTop = 16f;

        [Header("Node Sizes")]
        [SerializeField] private float _nodeSize = 20f;
        [SerializeField] private float _bossNodeSize = 26f;

        // =================================================================
        // EVENTS
        // =================================================================

        /// <summary>
        /// Fired when the player confirms a node selection.
        /// Parameter: the selected MapNode.
        /// </summary>
        public event Action<MapNode> OnNodeConfirmed;

        // =================================================================
        // STATE
        // =================================================================

        private Canvas _canvas;
        private RectTransform _canvasRT;
        private MapData _mapData;

        private readonly Dictionary<int, NodeVisual> _nodeVisuals = new();
        private readonly List<GameObject> _lineObjects = new();
        private MapNode _selectedNode;

        // UI elements
        private Text _seedText;
        private Text _hpText;
        private GameObject _infoPanel;
        private Text _infoTitle;
        private Text _infoSub;
        private Button _confirmButton;
        private Image _playerMarker;

        // Navigation
        private UIFocusSetter _focusSetter;
        private UICancelHandler _cancelHandler;

        // Colors
        private static readonly Color EnemyColor = new Color(0.8f, 0.3f, 0.3f);
        private static readonly Color RestColor = new Color(0.3f, 0.8f, 0.4f);
        private static readonly Color BossColor = new Color(0.9f, 0.2f, 0.2f);
        private static readonly Color LockedColor = new Color(0.25f, 0.25f, 0.25f);
        private static readonly Color CompletedColor = new Color(0.4f, 0.4f, 0.4f);
        private static readonly Color AccessibleGlow = new Color(1f, 1f, 0.6f);
        private static readonly Color LineColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
        private static readonly Color LineCompletedColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);

        // =================================================================
        // PUBLIC
        // =================================================================

        /// <summary>
        /// Build the map UI from MapData. Call once after generation.
        /// </summary>
        public void BuildMap(MapData mapData)
        {
            _mapData = mapData;

            ClearMap();
            CreateCanvas();
            CreateHUD();
            CreateLines();
            CreateNodes();
            CreatePlayerMarker();
            CreateInfoPanel();
            SetupNavigationComponents();
            UpdateVisuals();
        }

        /// <summary>
        /// Refresh visuals after a node is completed.
        /// Call after MapData.CompleteNode().
        /// </summary>
        public void UpdateVisuals()
        {
            if (_mapData == null) return;

            foreach (var kvp in _nodeVisuals)
            {
                var node = FindNode(kvp.Key);
                var vis = kvp.Value;

                if (node == null) continue;

                UpdateNodeVisual(node, vis);
            }

            UpdatePlayerMarker();
            UpdateHUD();

            // Hide info panel on refresh
            if (_infoPanel != null)
                _infoPanel.SetActive(false);

            _selectedNode = null;

            // Rebuild navigation graph for current accessibility state
            RebuildNavigation();
        }

        // =================================================================
        // NAVIGATION
        // =================================================================

        private void SetupNavigationComponents()
        {
            // Focus setter
            _focusSetter = gameObject.GetComponent<UIFocusSetter>();
            if (_focusSetter == null)
                _focusSetter = gameObject.AddComponent<UIFocusSetter>();

            // Cancel handler — base action does nothing on map (no "back" from map)
            _cancelHandler = gameObject.GetComponent<UICancelHandler>();
            if (_cancelHandler == null)
                _cancelHandler = gameObject.AddComponent<UICancelHandler>();

            _cancelHandler.ClearStack();
        }

        /// <summary>
        /// Wire keyboard/gamepad navigation for all accessible map nodes.
        /// Called after BuildMap and after UpdateVisuals (accessibility changes).
        /// </summary>
        private void RebuildNavigation()
        {
            var entries = new List<MapNavigationBuilder.NodeEntry>();

            foreach (var kvp in _nodeVisuals)
            {
                var node = FindNode(kvp.Key);
                if (node == null) continue;

                bool isNavigable = node.IsAccessible && !node.IsCompleted;

                entries.Add(new MapNavigationBuilder.NodeEntry
                {
                    Selectable = kvp.Value.Button,
                    Layer = node.Layer,
                    Column = node.Column,
                    IsAccessible = isNavigable
                });

                // Apply visual focus style to accessible nodes
                if (isNavigable)
                    UISelectableStyle.Apply(kvp.Value.Button);
            }

            Selectable firstNode = MapNavigationBuilder.Build(entries);

            if (firstNode != null && _focusSetter != null)
                _focusSetter.SetDefault(firstNode.gameObject);
        }

        // =================================================================
        // CANVAS
        // =================================================================

        private void CreateCanvas()
        {
            GameObject canvasGO = new GameObject("MapCanvas");
            canvasGO.transform.SetParent(transform);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(384, 216);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();
            _canvasRT = canvasGO.GetComponent<RectTransform>();

            // EventSystem — uses InputSystemUIInputModule
            UIEventSystemProvider.EnsureEventSystem();
        }

        // =================================================================
        // HUD — HP and seed
        // =================================================================

        private void CreateHUD()
        {
            // Seed text — top left
            _seedText = MakeText(_canvasRT, "SeedText",
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(4, -3), new Vector2(100, 10),
                5, TextAnchor.MiddleLeft, new Color(0.6f, 0.6f, 0.6f));
            _seedText.text = $"Seed: {_mapData.Seed}";

            // HP text — bottom left
            _hpText = MakeText(_canvasRT, "HPText",
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(4, 4), new Vector2(80, 10),
                6, TextAnchor.MiddleLeft, Color.white);
        }

        private void UpdateHUD()
        {
            var ph = Battle.PlayerHealth.Instance;
            if (ph != null && _hpText != null)
            {
                float pct = ph.HPPercent;
                string color = pct > 0.5f ? "lime" : pct > 0.25f ? "yellow" : "red";
                _hpText.text = $"HP: <color={color}>{ph.CurrentHP}/{ph.MaxHP}</color>";
            }
        }

        // =================================================================
        // CONNECTION LINES
        // =================================================================

        private void CreateLines()
        {
            foreach (var node in _mapData.AllNodes)
            {
                Vector2 fromPos = NodeToCanvas(node.Position);

                foreach (var conn in node.Connections)
                {
                    Vector2 toPos = NodeToCanvas(conn.Position);
                    bool completed = node.IsCompleted;

                    CreateLine(fromPos, toPos, completed);
                }
            }
        }

        private void CreateLine(Vector2 from, Vector2 to, bool completed)
        {
            GameObject lineGO = new GameObject("Line", typeof(RectTransform), typeof(Image));
            lineGO.transform.SetParent(_canvasRT, false);

            lineGO.transform.SetAsFirstSibling();

            Image img = lineGO.GetComponent<Image>();
            img.color = completed ? LineCompletedColor : LineColor;

            RectTransform rt = lineGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);

            Vector2 diff = to - from;
            float dist = diff.magnitude;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            rt.anchoredPosition = from;
            rt.sizeDelta = new Vector2(dist, 1.5f);
            rt.localRotation = Quaternion.Euler(0, 0, angle);

            _lineObjects.Add(lineGO);
        }

        // =================================================================
        // NODES
        // =================================================================

        private void CreateNodes()
        {
            foreach (var node in _mapData.AllNodes)
            {
                var vis = CreateNodeVisual(node);
                _nodeVisuals[node.Id] = vis;
            }
        }

        private NodeVisual CreateNodeVisual(MapNode node)
        {
            float size = node.Type == NodeType.Boss ? _bossNodeSize : _nodeSize;

            // Root button
            GameObject rootGO = new GameObject($"Node_{node.Id}", typeof(RectTransform), typeof(Image), typeof(Button));
            rootGO.transform.SetParent(_canvasRT, false);

            RectTransform rt = rootGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = NodeToCanvas(node.Position);
            rt.sizeDelta = new Vector2(size, size);

            Image bg = rootGO.GetComponent<Image>();
            bg.color = GetNodeColor(node);

            Button btn = rootGO.GetComponent<Button>();
            int nodeId = node.Id;
            btn.onClick.AddListener(() => OnNodeClicked(nodeId));

            // Type label
            Text label = MakeText(rt, "Label",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(size + 4, size),
                node.Type == NodeType.Boss ? 7 : 6, TextAnchor.MiddleCenter, Color.white);
            label.text = GetNodeIcon(node.Type);
            label.fontStyle = FontStyle.Bold;

            // Glow border (accessible indicator)
            GameObject glowGO = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            glowGO.transform.SetParent(rootGO.transform, false);

            RectTransform glowRT = glowGO.GetComponent<RectTransform>();
            glowRT.anchorMin = Vector2.zero;
            glowRT.anchorMax = Vector2.one;
            glowRT.offsetMin = new Vector2(-2, -2);
            glowRT.offsetMax = new Vector2(2, 2);
            glowRT.SetAsFirstSibling();

            Image glow = glowGO.GetComponent<Image>();
            glow.color = new Color(0, 0, 0, 0);

            // Completion checkmark
            Text check = MakeText(rt, "Check",
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(2, 2), new Vector2(10, 10),
                6, TextAnchor.MiddleCenter, Color.white);
            check.text = "";

            return new NodeVisual
            {
                Root = rootGO,
                Background = bg,
                Label = label,
                Glow = glow,
                Checkmark = check,
                Button = btn
            };
        }

        private void UpdateNodeVisual(MapNode node, NodeVisual vis)
        {
            if (node.IsCompleted)
            {
                vis.Background.color = CompletedColor;
                vis.Glow.color = new Color(0, 0, 0, 0);
                vis.Checkmark.text = "✓";
                vis.Button.interactable = false;
            }
            else if (node.IsAccessible)
            {
                vis.Background.color = GetNodeColor(node);
                vis.Glow.color = AccessibleGlow;
                vis.Checkmark.text = "";
                vis.Button.interactable = true;
            }
            else
            {
                vis.Background.color = LockedColor;
                vis.Glow.color = new Color(0, 0, 0, 0);
                vis.Checkmark.text = "";
                vis.Button.interactable = false;
            }
        }

        // =================================================================
        // PLAYER MARKER
        // =================================================================

        private void CreatePlayerMarker()
        {
            GameObject markerGO = new GameObject("PlayerMarker", typeof(RectTransform), typeof(Image));
            markerGO.transform.SetParent(_canvasRT, false);

            RectTransform rt = markerGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(6, 6);

            _playerMarker = markerGO.GetComponent<Image>();
            _playerMarker.color = new Color(0.3f, 0.8f, 1f);

            UpdatePlayerMarker();
        }

        private void UpdatePlayerMarker()
        {
            if (_playerMarker == null) return;

            if (_mapData.CurrentNode != null)
            {
                Vector2 pos = NodeToCanvas(_mapData.CurrentNode.Position);
                _playerMarker.rectTransform.anchoredPosition = pos + new Vector2(0, -(_nodeSize * 0.5f + 5f));
                _playerMarker.gameObject.SetActive(true);
            }
            else
            {
                _playerMarker.rectTransform.anchoredPosition = new Vector2(0, -(_canvasRT.rect.height * 0.5f - _mapPaddingBottom - 10f));
                _playerMarker.gameObject.SetActive(true);
            }
        }

        // =================================================================
        // INFO PANEL — shows on node click / Enter
        // =================================================================

        private void CreateInfoPanel()
        {
            _infoPanel = new GameObject("InfoPanel", typeof(RectTransform), typeof(Image));
            _infoPanel.transform.SetParent(_canvasRT, false);

            RectTransform rt = _infoPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0);
            rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = new Vector2(0, 16);
            rt.sizeDelta = new Vector2(140, 36);

            Image bg = _infoPanel.GetComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

            // Title
            _infoTitle = MakeText(rt, "Title",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -4), new Vector2(130, 12),
                7, TextAnchor.MiddleCenter, Color.white);
            _infoTitle.fontStyle = FontStyle.Bold;

            // Subtitle
            _infoSub = MakeText(rt, "Sub",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -15), new Vector2(130, 10),
                5, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.7f));

            // Confirm button
            GameObject btnGO = new GameObject("ConfirmBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(rt, false);

            RectTransform btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0);
            btnRT.anchorMax = new Vector2(0.5f, 0);
            btnRT.pivot = new Vector2(0.5f, 0);
            btnRT.anchoredPosition = new Vector2(0, 2);
            btnRT.sizeDelta = new Vector2(50, 12);

            Image btnBg = btnGO.GetComponent<Image>();
            btnBg.color = new Color(0.2f, 0.5f, 0.2f);

            Text btnText = MakeText(btnRT, "BtnText",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(50, 12),
                6, TextAnchor.MiddleCenter, Color.white);
            btnText.text = "Enter";

            _confirmButton = btnGO.GetComponent<Button>();
            _confirmButton.onClick.AddListener(OnConfirmClicked);

            // Apply style to confirm button
            UISelectableStyle.Apply(_confirmButton);

            _infoPanel.SetActive(false);
        }

        // =================================================================
        // NODE SELECTION
        // =================================================================

        private void OnNodeClicked(int nodeId)
        {
            var node = FindNode(nodeId);
            if (node == null || !node.IsAccessible || node.IsCompleted) return;

            _selectedNode = node;

            // Update info panel
            _infoTitle.text = GetNodeTitle(node);
            _infoSub.text = GetNodeSubtitle(node);
            _infoPanel.SetActive(true);

            // Highlight selected (reset others)
            foreach (var kvp in _nodeVisuals)
            {
                var n = FindNode(kvp.Key);
                if (n != null && n.IsAccessible && !n.IsCompleted)
                {
                    kvp.Value.Glow.color = (n.Id == nodeId) ? Color.white : AccessibleGlow;
                }
            }

            // Wire navigation: selected node ↔ confirm button
            if (_nodeVisuals.TryGetValue(nodeId, out var vis))
            {
                MapNavigationBuilder.WireInfoPanel(vis.Button, _confirmButton);
            }

            // Push Escape handler to close info panel
            _cancelHandler.Push(() => CloseInfoPanel(nodeId));

            // Focus the confirm button
            _focusSetter.FocusOn(_confirmButton.gameObject);
        }

        private void CloseInfoPanel(int nodeIdToRestore)
        {
            _infoPanel.SetActive(false);
            _selectedNode = null;

            // Reset glow highlights
            foreach (var kvp in _nodeVisuals)
            {
                var n = FindNode(kvp.Key);
                if (n != null && n.IsAccessible && !n.IsCompleted)
                    kvp.Value.Glow.color = AccessibleGlow;
            }

            // Rebuild navigation (removes info panel links)
            RebuildNavigation();

            // Restore focus to the node that was selected
            if (_nodeVisuals.TryGetValue(nodeIdToRestore, out var vis) && vis.Button.IsInteractable())
            {
                _focusSetter.FocusOn(vis.Button.gameObject);
            }
        }

        private void OnConfirmClicked()
        {
            if (_selectedNode == null) return;

            // Pop the info panel cancel handler since we're confirming
            _cancelHandler.Pop();

            OnNodeConfirmed?.Invoke(_selectedNode);
        }

        // =================================================================
        // HELPERS
        // =================================================================

        private Vector2 NodeToCanvas(Vector2 normalized)
        {
            float mapWidth = 384f - _mapPadding * 2f;
            float mapHeight = 216f - _mapPaddingBottom - _mapPaddingTop;

            float x = (normalized.x - 0.5f) * mapWidth;
            float y = (normalized.y - 0.5f) * mapHeight;

            y += (_mapPaddingBottom - _mapPaddingTop) * 0.5f;

            return new Vector2(x, y);
        }

        private static Color GetNodeColor(MapNode node)
        {
            return node.Type switch
            {
                NodeType.Enemy => EnemyColor,
                NodeType.Rest => RestColor,
                NodeType.Boss => BossColor,
                NodeType.Elite => new Color(0.7f, 0.3f, 0.7f),
                NodeType.Shop => new Color(0.3f, 0.5f, 0.8f),
                _ => EnemyColor
            };
        }

        private static string GetNodeIcon(NodeType type)
        {
            return type switch
            {
                NodeType.Enemy => "⚔",
                NodeType.Rest => "♥",
                NodeType.Boss => "☠",
                NodeType.Elite => "★",
                NodeType.Shop => "$",
                NodeType.Event => "?",
                _ => "?"
            };
        }

        private static string GetNodeTitle(MapNode node)
        {
            if (node.Type == NodeType.Rest) return "Rest";
            if (node.Type == NodeType.Elite && node.EnemyData != null)
                return $"Elite {node.EnemyData.enemyName}";
            if (node.EnemyData != null) return node.EnemyData.enemyName;
            return node.Type.ToString();
        }

        private static string GetNodeSubtitle(MapNode node)
        {
            return node.Type switch
            {
                NodeType.Enemy => node.EnemyData != null ? $"HP: {node.EnemyData.maxHP}" : "Battle",
                NodeType.Elite => node.EnemyData != null
                    ? $"ELITE — HP: ~{Mathf.RoundToInt(node.EnemyData.maxHP * 1.75f)}  |  Harder patterns"
                    : "Elite Battle",
                NodeType.Rest => "Heal 30% HP",
                NodeType.Boss => node.EnemyData != null ? $"BOSS — HP: {node.EnemyData.maxHP}" : "Boss Battle",
                _ => ""
            };
        }
        
    

        private MapNode FindNode(int id)
        {
            foreach (var node in _mapData.AllNodes)
            {
                if (node.Id == id) return node;
            }
            return null;
        }

        private void ClearMap()
        {
            _nodeVisuals.Clear();
            _lineObjects.Clear();
            _selectedNode = null;

            if (_canvas != null)
                Destroy(_canvas.gameObject);
        }

        // =================================================================
        // TEXT HELPER
        // =================================================================

        private static Text MakeText(RectTransform parent, string name,
            Vector2 ancMin, Vector2 ancMax, Vector2 pivot,
            Vector2 pos, Vector2 size,
            int fontSize, TextAnchor align, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = ancMin;
            rt.anchorMax = ancMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Text t = obj.GetComponent<Text>();
            t.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;

            return t;
        }

        // =================================================================
        // CLEANUP
        // =================================================================

        private void OnDestroy()
        {
            OnNodeConfirmed = null;
        }

        // =================================================================
        // INNER TYPE
        // =================================================================

        private class NodeVisual
        {
            public GameObject Root;
            public Image Background;
            public Text Label;
            public Image Glow;
            public Text Checkmark;
            public Button Button;
        }
    }
}