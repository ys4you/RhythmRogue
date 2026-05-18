using System;
using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.UI;
using RhythmRogue.UI.Navigation;

namespace RhythmRogue.Battle
{
    public class PauseMenu : MonoBehaviour
    {
        public event Action OnResumeRequested;
        public event Action OnQuitRequested;

        private Canvas _canvas;
        private RectTransform _canvasRT;
        private GameObject _panel;
        private Button _resumeBtn, _quitBtn;
        private UIFocusSetter _focusSetter;
        private UICancelHandler _cancelHandler;

        private void Awake() { CreateUI(); Hide(); }

        public void Show()
        {
            _panel.SetActive(true);
            _focusSetter.FocusOn(_resumeBtn.gameObject);
        }

        public void Hide() { _panel.SetActive(false); }

        private void OnResume() => OnResumeRequested?.Invoke();
        private void OnQuit() => OnQuitRequested?.Invoke();

        private void CreateUI()
        {
            var canvasGO = new GameObject("PauseCanvas");
            canvasGO.transform.SetParent(transform);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            _canvasRT = canvasGO.GetComponent<RectTransform>();
            UIEventSystemProvider.EnsureEventSystem();

            // Dim overlay over the battle
            _panel = MakePanel(_canvasRT, "PausePanel", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(UIHelpers.BgDeep.r, UIHelpers.BgDeep.g, UIHelpers.BgDeep.b, 0.85f));
            _panel.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            _panel.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            var panelRT = _panel.GetComponent<RectTransform>();

            // Card behind the buttons
            MakePanel(panelRT, "Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 500), UIHelpers.BgSurface);

            var title = MakeText(panelRT, "Title", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(600, 100), 60, TextAnchor.MiddleCenter, UIHelpers.WarmGold);
            title.fontStyle = FontStyle.Bold;
            title.text = "PAUSED";

            float btnW = 400f, btnH = 80f, btnGap = 20f;
            _resumeBtn = MakeButton(panelRT, "ResumeBtn", "Resume", new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(btnW, btnH), UIHelpers.RustOrange);
            _resumeBtn.onClick.AddListener(OnResume);

            _quitBtn = MakeButton(panelRT, "QuitBtn", "Quit to Menu", new Vector2(0.5f, 0.5f), new Vector2(0, 20 - btnH - btnGap), new Vector2(btnW, btnH), UIHelpers.Shadow);
            _quitBtn.onClick.AddListener(OnQuit);

            // Tip text
            var tip = MakeText(panelRT, "Tip", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -200), new Vector2(800, 50), 20, TextAnchor.MiddleCenter, UIHelpers.AmberOrange);
            tip.text = "Press Escape to resume";

            // Navigation
            UINavigationHelper.WireVerticalNoWrap(_resumeBtn, _quitBtn);
            UISelectableStyle.Apply(_resumeBtn); UISelectableStyle.Apply(_quitBtn);

            _focusSetter = gameObject.AddComponent<UIFocusSetter>();
            _focusSetter.SetDefault(_resumeBtn.gameObject);

            _cancelHandler = gameObject.AddComponent<UICancelHandler>();
            _cancelHandler.SetBaseAction(OnResume);
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
            return t;
        }

        private static Button MakeButton(RectTransform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color bgColor)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            obj.GetComponent<Image>().color = bgColor;

            var txtGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtGO.transform.SetParent(obj.transform, false);
            var txtRT = txtGO.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;
            var t = txtGO.GetComponent<Text>();
            t.font = UIHelpers.GetDefaultFont(28);
            t.fontSize = 28; t.alignment = TextAnchor.MiddleCenter;
            t.color = UIHelpers.OffWhite; t.fontStyle = FontStyle.Bold; t.text = label;
            return obj.GetComponent<Button>();
        }
    }
}