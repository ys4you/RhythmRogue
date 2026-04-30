using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.UI;
using RhythmRogue.UI.Navigation;

namespace RhythmRogue.Map
{
    public class MapUI : MonoBehaviour
    {
        [Header("Map Area")]
        [SerializeField] private float _mapPadding = 200f;
        [SerializeField] private float _mapPaddingBottom = 160f;
        [SerializeField] private float _mapPaddingTop = 120f;

        [Header("Node Sizes")]
        [SerializeField] private float _nodeSize = 100f;
        [SerializeField] private float _bossNodeSize = 130f;

        public event Action<MapNode> OnNodeConfirmed;

        private Canvas _canvas;
        private RectTransform _canvasRT;
        private MapData _mapData;
        private readonly Dictionary<int, NodeVisual> _nodeVisuals = new();
        private readonly List<GameObject> _lineObjects = new();
        private MapNode _selectedNode;
        private Text _seedText, _hpText;
        private GameObject _infoPanel;
        private Text _infoTitle, _infoSub;
        private Button _confirmButton;
        private Image _playerMarker;
        private UIFocusSetter _focusSetter;
        private UICancelHandler _cancelHandler;

        private static readonly Color EnemyColor = new(0.8f, 0.3f, 0.3f);
        private static readonly Color RestColor = new(0.3f, 0.8f, 0.4f);
        private static readonly Color BossColor = new(0.9f, 0.2f, 0.2f);
        private static readonly Color LockedColor = new(0.25f, 0.25f, 0.25f);
        private static readonly Color CompletedColor = new(0.4f, 0.4f, 0.4f);
        private static readonly Color AccessibleGlow = new(1f, 1f, 0.6f);
        private static readonly Color LineColor = new(0.4f, 0.4f, 0.4f, 0.6f);
        private static readonly Color LineCompletedColor = new(0.7f, 0.7f, 0.7f, 0.8f);

        public void BuildMap(MapData mapData)
        {
            _mapData = mapData;
            ClearMap(); CreateCanvas(); CreateHUD(); CreateLines();
            CreateNodes(); CreatePlayerMarker(); CreateInfoPanel();
            SetupNavigationComponents(); UpdateVisuals();
        }

        public void UpdateVisuals()
        {
            if (_mapData == null) return;
            foreach (var kvp in _nodeVisuals)
            {
                var node = FindNode(kvp.Key);
                if (node != null) UpdateNodeVisual(node, kvp.Value);
            }
            UpdatePlayerMarker(); UpdateHUD();
            if (_infoPanel != null) _infoPanel.SetActive(false);
            _selectedNode = null; RebuildNavigation();
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

        private void CreateHUD()
        {
            // Offset inward to stay inside CRT bezel
            _seedText = MakeText(_canvasRT, "SeedText", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(50, -50), new Vector2(500, 50), 22, TextAnchor.MiddleLeft, new Color(0.6f, 0.6f, 0.6f));
            _seedText.text = $"Seed: {_mapData.Seed}";

            _hpText = MakeText(_canvasRT, "HPText", new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(50, 50), new Vector2(400, 50), 26, TextAnchor.MiddleLeft, Color.white);
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

        private void CreateLines()
        {
            foreach (var node in _mapData.AllNodes)
            {
                Vector2 from = NodeToCanvas(node.Position);
                foreach (var conn in node.Connections)
                    CreateLine(from, NodeToCanvas(conn.Position), node.IsCompleted);
            }
        }

        private void CreateLine(Vector2 from, Vector2 to, bool completed)
        {
            var lineGO = new GameObject("Line", typeof(RectTransform), typeof(Image));
            lineGO.transform.SetParent(_canvasRT, false);
            lineGO.transform.SetAsFirstSibling();
            lineGO.GetComponent<Image>().color = completed ? LineCompletedColor : LineColor;
            var rt = lineGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
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
            rootGO.transform.SetParent(_canvasRT, false);
            var rt = rootGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = NodeToCanvas(node.Position);
            rt.sizeDelta = new Vector2(size, size);
            Image bg = rootGO.GetComponent<Image>(); bg.color = GetNodeColor(node);
            Button btn = rootGO.GetComponent<Button>();
            int nodeId = node.Id; btn.onClick.AddListener(() => OnNodeClicked(nodeId));

            Text label = MakeText(rt, "Label", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(size + 20, size),
                node.Type == NodeType.Boss ? 36 : 30, TextAnchor.MiddleCenter, Color.white);
            label.text = GetNodeIcon(node.Type); label.fontStyle = FontStyle.Bold;

            var glowGO = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            glowGO.transform.SetParent(rootGO.transform, false);
            var glowRT = glowGO.GetComponent<RectTransform>();
            glowRT.anchorMin = Vector2.zero; glowRT.anchorMax = Vector2.one;
            glowRT.offsetMin = new Vector2(-10, -10); glowRT.offsetMax = new Vector2(10, 10);
            glowRT.SetAsFirstSibling();
            Image glow = glowGO.GetComponent<Image>(); glow.color = new Color(0, 0, 0, 0);

            Text check = MakeText(rt, "Check", new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(10, 10), new Vector2(50, 50), 26, TextAnchor.MiddleCenter, Color.white);
            check.text = "";

            return new NodeVisual { Root = rootGO, Background = bg, Label = label, Glow = glow, Checkmark = check, Button = btn };
        }

        private void UpdateNodeVisual(MapNode node, NodeVisual vis)
        {
            if (node.IsCompleted) { vis.Background.color = CompletedColor; vis.Glow.color = new Color(0,0,0,0); vis.Checkmark.text = "✓"; vis.Button.interactable = false; }
            else if (node.IsAccessible) { vis.Background.color = GetNodeColor(node); vis.Glow.color = AccessibleGlow; vis.Checkmark.text = ""; vis.Button.interactable = true; }
            else { vis.Background.color = LockedColor; vis.Glow.color = new Color(0,0,0,0); vis.Checkmark.text = ""; vis.Button.interactable = false; }
        }

        private void CreatePlayerMarker()
        {
            var markerGO = new GameObject("PlayerMarker", typeof(RectTransform), typeof(Image));
            markerGO.transform.SetParent(_canvasRT, false);
            var rt = markerGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(30, 30);
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
                _playerMarker.rectTransform.anchoredPosition = pos + new Vector2(0, -(_nodeSize * 0.5f + 25f));
                _playerMarker.gameObject.SetActive(true);
            }
            else
            {
                _playerMarker.rectTransform.anchoredPosition = new Vector2(0, -(_canvasRT.rect.height * 0.5f - _mapPaddingBottom - 50f));
                _playerMarker.gameObject.SetActive(true);
            }
        }

        private void CreateInfoPanel()
        {
            _infoPanel = new GameObject("InfoPanel", typeof(RectTransform), typeof(Image));
            _infoPanel.transform.SetParent(_canvasRT, false);
            var rt = _infoPanel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = new Vector2(0, 100);
            rt.sizeDelta = new Vector2(700, 180);
            _infoPanel.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

            _infoTitle = MakeText(rt, "Title", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -20), new Vector2(650, 60), 32, TextAnchor.MiddleCenter, Color.white);
            _infoTitle.fontStyle = FontStyle.Bold;

            _infoSub = MakeText(rt, "Sub", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -75), new Vector2(650, 50), 22, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.7f));

            var btnGO = new GameObject("ConfirmBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(rt, false);
            var btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.anchorMin = btnRT.anchorMax = new Vector2(0.5f, 0);
            btnRT.pivot = new Vector2(0.5f, 0);
            btnRT.anchoredPosition = new Vector2(0, 10);
            btnRT.sizeDelta = new Vector2(250, 60);
            btnGO.GetComponent<Image>().color = new Color(0.2f, 0.5f, 0.2f);

            Text btnText = MakeText(btnRT, "BtnText", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(250, 60), 26, TextAnchor.MiddleCenter, Color.white);
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
                    kvp.Value.Glow.color = (n.Id == nodeId) ? Color.white : AccessibleGlow;
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
                if (n != null && n.IsAccessible && !n.IsCompleted) kvp.Value.Glow.color = AccessibleGlow;
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

        private Vector2 NodeToCanvas(Vector2 normalized)
        {
            float mapWidth = 1920f - _mapPadding * 2f;
            float mapHeight = 1080f - _mapPaddingBottom - _mapPaddingTop;
            float x = (normalized.x - 0.5f) * mapWidth;
            float y = (normalized.y - 0.5f) * mapHeight;
            y += (_mapPaddingBottom - _mapPaddingTop) * 0.5f;
            return new Vector2(x, y);
        }

        private static Color GetNodeColor(MapNode node) => node.Type switch
        {
            NodeType.Enemy => EnemyColor, NodeType.Rest => RestColor, NodeType.Boss => BossColor,
            NodeType.Elite => new Color(0.7f, 0.3f, 0.7f), NodeType.Shop => new Color(0.3f, 0.5f, 0.8f), _ => EnemyColor
        };

        private static string GetNodeIcon(NodeType type) => type switch
        {
            NodeType.Enemy => "⚔", NodeType.Rest => "♥", NodeType.Boss => "☠",
            NodeType.Elite => "★", NodeType.Shop => "$", NodeType.Event => "?", _ => "?"
        };

        private static string GetNodeTitle(MapNode node)
        {
            if (node.Type == NodeType.Rest) return "Rest";
            if (node.Type == NodeType.Elite && node.EnemyData != null) return $"Elite {node.EnemyData.enemyName}";
            if (node.EnemyData != null) return node.EnemyData.enemyName;
            return node.Type.ToString();
        }

        private static string GetNodeSubtitle(MapNode node) => node.Type switch
        {
            NodeType.Enemy => node.EnemyData != null ? $"HP: {node.EnemyData.maxHP}" : "Battle",
            NodeType.Elite => node.EnemyData != null ? $"ELITE  |  HP: ~{Mathf.RoundToInt(node.EnemyData.maxHP * 1.75f)}  |  Harder patterns" : "Elite Battle",
            NodeType.Rest => "Heal 30% HP",
            NodeType.Boss => node.EnemyData != null ? $"BOSS  |  HP: {node.EnemyData.maxHP}" : "Boss Battle",
            _ => ""
        };

        private MapNode FindNode(int id) { foreach (var n in _mapData.AllNodes) if (n.Id == id) return n; return null; }

        private void ClearMap()
        {
            _nodeVisuals.Clear(); _lineObjects.Clear(); _selectedNode = null;
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
            public GameObject Root; public Image Background; public Text Label;
            public Image Glow; public Text Checkmark; public Button Button;
        }
    }
}
