using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RhythmRogue.Core;
using RhythmRogue.Data;
using RhythmRogue.UI.Navigation;

namespace RhythmRogue.UI
{
    /// <summary>
    /// A horizontal strip of relic icons shown on a HUD (map, battle, etc.), modelled on
    /// Slay the Spire's relic bar. Each held relic is an icon; hovering shows its name in a
    /// small tooltip; clicking opens a full RelicDetailCard with rarity, effect, description,
    /// and flavor.
    ///
    /// Self-contained: it builds its own top-anchored canvas, so adding it to any scene is a
    /// single call - no need to edit that scene's existing HUD layout:
    ///     RelicBar.Create(runState);
    ///
    /// Icons are currently colored swatches keyed off each relic's rarity/cardColor, because
    /// relic art doesn't exist yet. When sprites are added, only BuildIcon needs to change
    /// (swap the swatch Image.sprite); the hover/click behaviour stays identical.
    ///
    /// SOLID:
    ///   S - Lays out the relic icons and routes hover/click. It doesn't define relics, store
    ///       them, or render the detail view (RelicDetailCard does that).
    ///   D - Reads relics through the RunState abstraction; opens the detail card abstraction.
    /// </summary>
    public class RelicBar : MonoBehaviour
    {
        private RunState _runState;
        private Canvas _canvas;
        private RectTransform _canvasRT;
        private RectTransform _iconRow;
        private Text _tooltip;
        private RelicDetailCard _detailCard;
        private readonly List<GameObject> _icons = new();

        private const float IconSize = 56f;
        private const float IconGap = 10f;

        /// <summary>Create a relic bar bound to the given run state. Call once per HUD scene.</summary>
        public static RelicBar Create(RunState runState)
        {
            var go = new GameObject("RelicBar");
            var bar = go.AddComponent<RelicBar>();
            bar._runState = runState;
            bar.BuildCanvas();
            bar.Refresh();
            return bar;
        }

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("RelicBarCanvas");
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above normal HUD (50) so icons sit on top; below pause (500) and detail (800).
            _canvas.sortingOrder = 120;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            _canvasRT = canvasGO.GetComponent<RectTransform>();
            canvasGO.transform.SetParent(transform, false);
            UIEventSystemProvider.EnsureEventSystem();

            // Icon row: top-center of the screen, growing horizontally. Centered so the bar
            // stays balanced as relics are added.
            var rowGO = new GameObject("IconRow", typeof(RectTransform));
            rowGO.transform.SetParent(_canvasRT, false);
            _iconRow = rowGO.GetComponent<RectTransform>();
            _iconRow.anchorMin = new Vector2(0.5f, 1f);
            _iconRow.anchorMax = new Vector2(0.5f, 1f);
            _iconRow.pivot = new Vector2(0.5f, 1f);
            _iconRow.anchoredPosition = new Vector2(0, -16);
            _iconRow.sizeDelta = new Vector2(0, IconSize);

            // Tooltip: a small label that appears under the hovered icon.
            _tooltip = MakeText(_canvasRT, "Tooltip", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -16 - IconSize - 6), new Vector2(400, 32), 20, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
            _tooltip.fontStyle = FontStyle.Bold;
            _tooltip.gameObject.SetActive(false);

            _detailCard = RelicDetailCard.Create();
            _detailCard.transform.SetParent(transform, false);
        }

        private void OnEnable() { if (_canvas != null) Refresh(); }

        /// <summary>Rebuild the icon row from the run's current relics. Call after a relic is gained.</summary>
        public void Refresh()
        {
            foreach (var go in _icons) Destroy(go);
            _icons.Clear();

            if (_runState == null || _runState.ActiveRelics == null) return;
            var relics = _runState.ActiveRelics;
            int n = relics.Count;
            if (n == 0) { _iconRow.sizeDelta = new Vector2(0, IconSize); return; }

            float totalW = n * IconSize + (n - 1) * IconGap;
            _iconRow.sizeDelta = new Vector2(totalW, IconSize);
            float startX = -totalW * 0.5f + IconSize * 0.5f;

            for (int i = 0; i < n; i++)
                _icons.Add(BuildIcon(relics[i], startX + i * (IconSize + IconGap)));
        }

        private GameObject BuildIcon(RelicData relic, float x)
        {
            // Outer button (the icon).
            var iconGO = new GameObject($"Relic_{relic.relicName}", typeof(RectTransform), typeof(Image), typeof(Button));
            iconGO.transform.SetParent(_iconRow, false);
            var rt = iconGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0);
            rt.sizeDelta = new Vector2(IconSize, IconSize);

            // Rarity ring behind the icon.
            var ring = MakePanel(rt, "Ring", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, RelicRarityPalette.Accent(relic.rarity));
            var ringRT = ring.GetComponent<RectTransform>();
            ringRT.offsetMin = new Vector2(-3, -3); ringRT.offsetMax = new Vector2(3, 3);
            ringRT.SetAsFirstSibling();

            // The button's own Image is the background swatch (the relic's accent colour).
            // It stays as the base layer; if the relic has a real icon (or the shared
            // placeholder), that sprite is drawn on top via a child Image. This way a relic
            // with art shows the art, and one without still shows a coloured, clickable tile.
            var bgImage = iconGO.GetComponent<Image>();
            bgImage.color = RelicRarityPalette.IconSwatch(relic.rarity);

            Sprite resolved = relic.ResolvedIcon; // assigned icon, else shared event placeholder
            if (resolved != null)
            {
                var spriteGO = new GameObject("IconSprite", typeof(RectTransform), typeof(Image));
                spriteGO.transform.SetParent(rt, false);
                var sRT = spriteGO.GetComponent<RectTransform>();
                sRT.anchorMin = Vector2.zero; sRT.anchorMax = Vector2.one;
                // Small inset so the sprite sits inside the rarity ring rather than over it.
                sRT.offsetMin = new Vector2(6, 6); sRT.offsetMax = new Vector2(-6, -6);
                var sImg = spriteGO.GetComponent<Image>();
                sImg.sprite = resolved;
                sImg.color = UIHelpers.OffWhite;
                sImg.preserveAspect = true;
                sImg.raycastTarget = false; // clicks go to the button beneath
            }

            var btn = iconGO.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            RelicData captured = relic;
            btn.onClick.AddListener(() => _detailCard.Show(captured));

            // Hover behaviour: show the relic name in the tooltip while pointed at.
            var hover = iconGO.AddComponent<RelicIconHover>();
            hover.Init(relic.relicName, ShowTooltip, HideTooltip);

            return iconGO;
        }

        private void ShowTooltip(string text)
        {
            if (_tooltip == null) return;
            _tooltip.text = text;
            _tooltip.gameObject.SetActive(true);
        }

        private void HideTooltip()
        {
            if (_tooltip != null) _tooltip.gameObject.SetActive(false);
        }

        private static GameObject MakePanel(RectTransform parent, string name, Vector2 ancMin, Vector2 ancMax, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = ancMin; rt.anchorMax = ancMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            obj.GetComponent<Image>().color = color;
            return obj;
        }

        private static Text MakeText(RectTransform parent, string name, Vector2 ancMin, Vector2 ancMax, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = ancMin; rt.anchorMax = ancMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = obj.GetComponent<Text>();
            t.font = UIHelpers.GetDefaultFont(fontSize);
            t.fontSize = fontSize; t.alignment = align; t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            // Subtle background plate so the tooltip stays readable over any HUD.
            var plate = new GameObject("Plate", typeof(RectTransform), typeof(Image));
            plate.transform.SetParent(obj.transform, false);
            var pRT = plate.GetComponent<RectTransform>();
            pRT.anchorMin = Vector2.zero; pRT.anchorMax = Vector2.one;
            pRT.offsetMin = new Vector2(-12, -4); pRT.offsetMax = new Vector2(12, 4);
            plate.GetComponent<Image>().color = new Color(UIHelpers.BgDeep.r, UIHelpers.BgDeep.g, UIHelpers.BgDeep.b, 0.85f);
            plate.transform.SetAsFirstSibling();
            plate.GetComponent<Image>().raycastTarget = false;
            return t;
        }

        /// <summary>
        /// Tiny pointer-hover relay attached to each relic icon. Calls back into the bar to
        /// show/hide the name tooltip. Kept as a nested component so the bar owns all its
        /// behaviour in one file.
        /// </summary>
        private class RelicIconHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            private string _name;
            private System.Action<string> _onEnter;
            private System.Action _onExit;

            public void Init(string relicName, System.Action<string> onEnter, System.Action onExit)
            {
                _name = relicName; _onEnter = onEnter; _onExit = onExit;
            }

            public void OnPointerEnter(PointerEventData e) => _onEnter?.Invoke(_name);
            public void OnPointerExit(PointerEventData e) => _onExit?.Invoke();
        }
    }
}
