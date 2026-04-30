using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.Core;
using RhythmRogue.Battle;
using RhythmRogue.UI.Navigation;

namespace RhythmRogue.UI
{
    public class SummaryScreen : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RunState _runState;
        [Header("Timing")]
        [SerializeField] private float _headerAnimDuration = 0.5f;
        [SerializeField] private float _statRollDuration = 1.2f;
        [SerializeField] private float _statStaggerDelay = 0.15f;

        private Canvas _canvas;
        private RectTransform _canvasRT;
        private Text _headerText, _subHeaderText, _seedText;
        private Text[] _statLabels, _statValues;
        private Button _newRunBtn, _retryBtn, _menuBtn;
        private Image _bgOverlay;
        private UIFocusSetter _focusSetter;
        private UICancelHandler _cancelHandler;

        private int _battlesWon, _totalScore, _maxCombo, _nodesCompleted, _totalNodes, _finalHP, _maxHP;
        private float _bestAccuracy;
        private bool _isVictory;
        private string _seed;

        private void Start()
        {
            GatherStats(); CreateUI();
            _focusSetter = gameObject.AddComponent<UIFocusSetter>();
            _cancelHandler = gameObject.AddComponent<UICancelHandler>();
            _cancelHandler.SetBaseAction(OnMenu);
            StartCoroutine(AnimateEntrance());
        }

        private void GatherStats()
        {
            if (_runState != null)
            {
                _isVictory = _runState.WasVictory; _battlesWon = _runState.BattlesWon;
                _totalScore = _runState.TotalScore; _maxCombo = _runState.MaxCombo;
                _bestAccuracy = _runState.BestAccuracy; _seed = _runState.Seed ?? "???";
                if (_runState.MapData != null)
                {
                    _totalNodes = _runState.MapData.AllNodes.Count; _nodesCompleted = 0;
                    foreach (var n in _runState.MapData.AllNodes) if (n.IsCompleted) _nodesCompleted++;
                }
            }
            var ph = PlayerHealth.Instance;
            if (ph != null) { _finalHP = ph.CurrentHP; _maxHP = ph.MaxHP; }
        }

        private IEnumerator AnimateEntrance()
        {
            float t = 0f;
            Color bgTarget = _isVictory ? new Color(0.05f, 0.08f, 0.02f, 0.9f) : new Color(0.1f, 0.02f, 0.02f, 0.9f);
            while (t < _headerAnimDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / _headerAnimDuration);
                _headerText.rectTransform.localScale = Vector3.one * Mathf.Lerp(2f, 1f, Mathf.SmoothStep(0, 1, p));
                Color hc = _headerText.color; hc.a = p; _headerText.color = hc;
                Color bgc = _bgOverlay.color; bgc.a = Mathf.Lerp(0, bgTarget.a, p); _bgOverlay.color = bgc;
                yield return null;
            }
            yield return FadeText(_subHeaderText, 0.3f);
            for (int i = 0; i < _statLabels.Length; i++)
            {
                StartCoroutine(FadeText(_statLabels[i], 0.2f));
                StartCoroutine(RollStat(i));
                yield return new WaitForSecondsRealtime(_statStaggerDelay);
            }
            yield return new WaitForSecondsRealtime(0.2f);
            yield return FadeText(_seedText, 0.3f);
            yield return new WaitForSecondsRealtime(0.3f);
            _newRunBtn.gameObject.SetActive(true); _retryBtn.gameObject.SetActive(true); _menuBtn.gameObject.SetActive(true);
            UINavigationHelper.WireHorizontal(_newRunBtn, _retryBtn, _menuBtn);
            UISelectableStyle.Apply(_newRunBtn); UISelectableStyle.Apply(_retryBtn); UISelectableStyle.Apply(_menuBtn);
            _focusSetter.SetDefault(_newRunBtn.gameObject); _focusSetter.ApplyFocus();
        }

        private IEnumerator FadeText(Text text, float duration)
        {
            float t = 0f; Color c = text.color;
            while (t < duration) { t += Time.unscaledDeltaTime; c.a = Mathf.Clamp01(t / duration); text.color = c; yield return null; }
            c.a = 1f; text.color = c;
        }

        private IEnumerator RollStat(int index)
        {
            int target = GetStatTarget(index);
            float elapsed = 0f; Text val = _statValues[index]; Color c = val.color;
            while (elapsed < _statRollDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / _statRollDuration));
                val.text = FormatStatValue(index, Mathf.RoundToInt(Mathf.Lerp(0, target, t)));
                c.a = Mathf.Clamp01(elapsed / 0.2f); val.color = c;
                yield return null;
            }
            val.text = FormatStatValue(index, target); c.a = 1f; val.color = c;
        }

        private int GetStatTarget(int i) => i switch { 0 => _battlesWon, 1 => _nodesCompleted, 2 => _totalScore, 3 => _maxCombo, 4 => Mathf.RoundToInt(_bestAccuracy * 100f), 5 => _finalHP, _ => 0 };
        private string FormatStatValue(int i, int v) => i switch { 1 => $"{v} / {_totalNodes}", 4 => $"{v}%", 5 => $"{v} / {_maxHP}", _ => v.ToString() };

        private void OnNewRun() { if (_runState != null) _runState.StartNewRun(); ResetAndGo(SceneTransitionManager.MAP_SCENE); }
        private void OnRetry() { if (_runState != null) _runState.StartNewRun(_seed); ResetAndGo(SceneTransitionManager.MAP_SCENE); }
        private void OnMenu() { if (_runState != null) _runState.StartNewRun(); ResetAndGo(SceneTransitionManager.MAIN_MENU_SCENE); }

        private void ResetAndGo(string scene)
        {
            var ph = PlayerHealth.Instance; if (ph != null) ph.ResetForNewRun();
            _newRunBtn.interactable = false; _retryBtn.interactable = false; _menuBtn.interactable = false;
            var tm = SceneTransitionManager.Instance;
            if (tm != null) tm.GoTo(scene); else UnityEngine.SceneManagement.SceneManager.LoadScene(scene);
        }

        private void CreateUI()
        {
            var canvasGO = new GameObject("SummaryCanvas");
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

            var bgGO = MakePanel(_canvasRT, "BG", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
                _isVictory ? new Color(0.05f, 0.08f, 0.02f, 0f) : new Color(0.1f, 0.02f, 0.02f, 0f));
            bgGO.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            bgGO.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            _bgOverlay = bgGO.GetComponent<Image>();

            _headerText = MakeText(_canvasRT, "Header", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -90), new Vector2(1500, 100), 60, TextAnchor.MiddleCenter,
                _isVictory ? new Color(1f, 0.85f, 0f, 0f) : new Color(1f, 0.25f, 0.25f, 0f));
            _headerText.fontStyle = FontStyle.Bold;
            _headerText.text = _isVictory ? "VICTORY" : "DEFEATED";

            string subText = _isVictory ? "You conquered the dungeon!" : $"You were defeated. Made it through {_nodesCompleted} nodes.";
            _subHeaderText = MakeText(_canvasRT, "SubHeader", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -160), new Vector2(1500, 60), 26, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.7f, 0f));
            _subHeaderText.text = subText;

            string[] labels = { "Battles Won", "Nodes Cleared", "Score", "Max Combo", "Best Accuracy", "Final HP" };
            _statLabels = new Text[labels.Length];
            _statValues = new Text[labels.Length];
            float startY = -240f, rowH = 65f;

            for (int i = 0; i < labels.Length; i++)
            {
                float y = startY - i * rowH;

                // Label: pivot at right edge, so anchoredPosition.x = right edge position
                _statLabels[i] = MakeTextPivoted(_canvasRT, $"SL_{i}",
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1f, 1f),
                    new Vector2(-20, y), new Vector2(400, 55), 26, TextAnchor.MiddleRight, new Color(0.7f, 0.7f, 0.7f, 0f));
                _statLabels[i].text = labels[i];

                // Value: pivot at left edge, so anchoredPosition.x = left edge position
                _statValues[i] = MakeTextPivoted(_canvasRT, $"SV_{i}",
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f),
                    new Vector2(20, y), new Vector2(400, 55), 26, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0f));
                _statValues[i].fontStyle = FontStyle.Bold;
                _statValues[i].text = "0";
            }

            _seedText = MakeText(_canvasRT, "Seed", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, startY - labels.Length * rowH - 40), new Vector2(1000, 50),
                22, TextAnchor.MiddleCenter, new Color(0.5f, 0.5f, 0.5f, 0f));
            _seedText.text = $"Seed: {_seed}";

            float btnY = 90f, btnW = 300f, btnH = 70f, btnGap = 40f;
            _newRunBtn = MakeButton(_canvasRT, "NewRunBtn", "New Run", new Vector2(0.5f, 0), new Vector2(-(btnW + btnGap), btnY), new Vector2(btnW, btnH), new Color(0.2f, 0.5f, 0.2f));
            _newRunBtn.onClick.AddListener(OnNewRun); _newRunBtn.gameObject.SetActive(false);
            _retryBtn = MakeButton(_canvasRT, "RetryBtn", "Retry Seed", new Vector2(0.5f, 0), new Vector2(0, btnY), new Vector2(btnW, btnH), new Color(0.3f, 0.3f, 0.6f));
            _retryBtn.onClick.AddListener(OnRetry); _retryBtn.gameObject.SetActive(false);
            _menuBtn = MakeButton(_canvasRT, "MenuBtn", "Menu", new Vector2(0.5f, 0), new Vector2(btnW + btnGap, btnY), new Vector2(btnW, btnH), new Color(0.4f, 0.2f, 0.2f));
            _menuBtn.onClick.AddListener(OnMenu); _menuBtn.gameObject.SetActive(false);
        }

        private static GameObject MakePanel(RectTransform parent, string name, Vector2 ancMin, Vector2 ancMax, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = ancMin; rt.anchorMax = ancMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            obj.GetComponent<Image>().color = color; return obj;
        }

        private static Text MakeText(RectTransform parent, string name, Vector2 ancMin, Vector2 ancMax, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color color)
        {
            return MakeTextPivoted(parent, name, ancMin, ancMax, pivot, pos, size, fontSize, align, color);
        }

        private static Text MakeTextPivoted(RectTransform parent, string name, Vector2 ancMin, Vector2 ancMax, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color color)
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
            t.color = Color.white; t.fontStyle = FontStyle.Bold; t.text = label;
            return obj.GetComponent<Button>();
        }
    }
}
