using System;
using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.Data;
using RhythmRogue.UI;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// In-battle pause menu. Code-generated UI at 1920x1080
    /// reference resolution for crisp text rendering.
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseMenu : MonoBehaviour
    {
        public event Action OnResumeRequested;
        public event Action OnQuitRequested;

        private Canvas _canvas;
        private GameObject _root;
        private Slider _scrollSpeedSlider;
        private Text _scrollSpeedValue;
        private bool _isVisible;

        public bool IsVisible => _isVisible;

        private void Awake()
        {
            CreateUI();
            _root.SetActive(false);
        }

        public void Show()
        {
            _isVisible = true;
            _root.SetActive(true);
            _scrollSpeedSlider.SetValueWithoutNotify(ScrollSpeedSetting.Multiplier);
            _scrollSpeedValue.text = ScrollSpeedSetting.DisplayString;
        }

        public void Hide()
        {
            _isVisible = false;
            _root.SetActive(false);
        }

        // =================================================================
        // UI CREATION (1920x1080)
        // =================================================================

        private void CreateUI()
        {
            GameObject canvasGO = new GameObject("PauseCanvas");
            canvasGO.transform.SetParent(transform);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();
            RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();

            _root = MakePanel(canvasRT, "PauseRoot",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                new Color(0f, 0f, 0f, 0.75f));
            RectTransform rootRT = _root.GetComponent<RectTransform>();
            rootRT.offsetMin = Vector2.zero;
            rootRT.offsetMax = Vector2.zero;

            GameObject card = MakePanel(rootRT, "Card",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(800, 600),
                new Color(0.1f, 0.1f, 0.15f, 0.95f));
            RectTransform cardRT = card.GetComponent<RectTransform>();

            Text title = MakeText(cardRT, "Title",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -50), new Vector2(700, 70),
                42, TextAnchor.MiddleCenter, Color.white);
            title.fontStyle = FontStyle.Bold;
            title.text = "PAUSED";

            // Scroll speed
            float rowY = -150f;

            MakeText(cardRT, "ScrollLabel",
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(50, rowY), new Vector2(300, 50),
                24, TextAnchor.MiddleLeft, new Color(0.7f, 0.7f, 0.7f)).text = "Scroll Speed";

            _scrollSpeedValue = MakeText(cardRT, "ScrollValue",
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-50, rowY), new Vector2(150, 50),
                24, TextAnchor.MiddleRight, Color.white);
            _scrollSpeedValue.text = ScrollSpeedSetting.DisplayString;

            GameObject sliderGO = CreateSliderGO(cardRT, "ScrollSlider",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, rowY - 50), new Vector2(600, 40));

            _scrollSpeedSlider = sliderGO.GetComponent<Slider>();
            _scrollSpeedSlider.minValue = 0.5f;
            _scrollSpeedSlider.maxValue = 3.0f;
            _scrollSpeedSlider.value = ScrollSpeedSetting.Multiplier;
            _scrollSpeedSlider.onValueChanged.AddListener(OnScrollSpeedChanged);

            // Audio offset display
            float offsetY = rowY - 130f;
            float savedOffset = PlayerPrefs.GetFloat("audioOffset", 0f);

            MakeText(cardRT, "OffsetLabel",
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(50, offsetY), new Vector2(300, 50),
                24, TextAnchor.MiddleLeft, new Color(0.7f, 0.7f, 0.7f)).text = "Audio Offset";

            MakeText(cardRT, "OffsetValue",
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-50, offsetY), new Vector2(200, 50),
                24, TextAnchor.MiddleRight, new Color(0.5f, 0.5f, 0.5f)).text = $"{savedOffset:+0;-0;0} ms";

            // Buttons
            float btnW = 400f;
            float btnH = 70f;
            float btnY = -400f;

            GameObject resumeGO = MakePanel(cardRT, "ResumeBtn",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f),
                new Vector2(0, btnY), new Vector2(btnW, btnH),
                new Color(0.2f, 0.45f, 0.2f));
            Button resumeBtn = resumeGO.AddComponent<Button>();
            resumeBtn.onClick.AddListener(() => OnResumeRequested?.Invoke());

            Text resumeText = MakeText(resumeGO.GetComponent<RectTransform>(), "Text",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(btnW, btnH),
                28, TextAnchor.MiddleCenter, Color.white);
            resumeText.fontStyle = FontStyle.Bold;
            resumeText.text = "Resume";

            float quitY = btnY - 90f;

            GameObject quitGO = MakePanel(cardRT, "QuitBtn",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f),
                new Vector2(0, quitY), new Vector2(btnW, btnH),
                new Color(0.4f, 0.2f, 0.2f));
            Button quitBtn = quitGO.AddComponent<Button>();
            quitBtn.onClick.AddListener(() => OnQuitRequested?.Invoke());

            Text quitText = MakeText(quitGO.GetComponent<RectTransform>(), "Text",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(btnW, btnH),
                28, TextAnchor.MiddleCenter, Color.white);
            quitText.fontStyle = FontStyle.Bold;
            quitText.text = "Quit to Menu";
        }

        private void OnScrollSpeedChanged(float value)
        {
            float rounded = Mathf.Round(value * 10f) / 10f;
            ScrollSpeedSetting.Multiplier = rounded;
            _scrollSpeedValue.text = ScrollSpeedSetting.DisplayString;
        }

        // =================================================================
        // UI HELPERS
        // =================================================================

        private static GameObject MakePanel(RectTransform parent, string name,
            Vector2 ancMin, Vector2 ancMax, Vector2 pivot,
            Vector2 pos, Vector2 size, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = ancMin;
            rt.anchorMax = ancMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            obj.GetComponent<Image>().color = color;
            return obj;
        }

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
            t.font = UIHelpers.GetDefaultFont(fontSize);
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            return t;
        }

        private static GameObject CreateSliderGO(RectTransform parent, string name,
            Vector2 ancMin, Vector2 ancMax, Vector2 pivot,
            Vector2 pos, Vector2 size)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);

            RectTransform rootRT = root.GetComponent<RectTransform>();
            rootRT.anchorMin = ancMin;
            rootRT.anchorMax = ancMax;
            rootRT.pivot = pivot;
            rootRT.anchoredPosition = pos;
            rootRT.sizeDelta = size;

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(root.transform, false);
            RectTransform bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            RectTransform fillAreaRT = fillArea.GetComponent<RectTransform>();
            fillAreaRT.anchorMin = Vector2.zero;
            fillAreaRT.anchorMax = Vector2.one;
            fillAreaRT.offsetMin = Vector2.zero;
            fillAreaRT.offsetMax = Vector2.zero;

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = new Color(0.4f, 0.5f, 0.7f);

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(root.transform, false);
            RectTransform handleAreaRT = handleArea.GetComponent<RectTransform>();
            handleAreaRT.anchorMin = Vector2.zero;
            handleAreaRT.anchorMax = Vector2.one;
            handleAreaRT.offsetMin = Vector2.zero;
            handleAreaRT.offsetMax = Vector2.zero;

            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            RectTransform handleRT = handle.GetComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(30, size.y + 10);
            handle.GetComponent<Image>().color = Color.white;

            Slider slider = root.GetComponent<Slider>();
            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.targetGraphic = handle.GetComponent<Image>();

            return root;
        }

        private void OnDestroy()
        {
            OnResumeRequested = null;
            OnQuitRequested = null;
        }
    }
}
