#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using RhythmRogue.Core;
using RhythmRogue.Battle;
using RhythmRogue.UI;
using RhythmRogue.Util;

namespace RhythmRogue.DevTools
{
    /// <summary>
    /// Playtest aid. Keeps a rolling buffer of the session's log output and, on F8, writes a bug
    /// report (build + run context + the recent log) to a file AND the clipboard, so a tester can
    /// paste it straight into the bug channel. Also shows a small dim corner label with the build
    /// version and the F8 hint, which doubles as the build stamp and makes the key discoverable.
    ///
    /// Why a rolling buffer: a useful report is the lead-up, not just the final frame. Unity already
    /// writes Player.log, but this gives the tester a one-key, in-context snapshot they can share
    /// without hunting for that file, and it stamps the seed + build so the run is reproducible.
    ///
    /// Self-bootstrapping and persistent (DontDestroyOnLoad via Singleton). The ENTIRE file is
    /// wrapped in UNITY_EDITOR || DEVELOPMENT_BUILD, exactly like DevCheatPanel, so it does not
    /// exist in a release build at all.
    ///
    /// How it reaches run context without scene references (acceptable only because this is an
    /// excluded-from-build dev tool): RunState is a ScriptableObject located via DevAssets (a loaded
    /// instance, or the project asset in-editor); PlayerHealth via FindFirstObjectByType (NON-
    /// creating, since PlayerHealth.Instance would spawn a stray singleton on non-battle scenes).
    ///
    /// SOLID: single responsibility is "capture and emit a playtest report". It owns only its own
    /// overlay and reads state through existing abstractions; nothing depends back on it.
    /// </summary>
    public class PlaytestReporter : Singleton<PlaytestReporter>
    {
        private const int MaxLines = 250;        // rolling log lines held in memory
        private const int ReportLineCount = 140; // how many of those go into a report

        private readonly Queue<string> _log = new();
        private Canvas _canvas;
        private Text _toast;
        private GameObject _hintGO;
        private Coroutine _toastRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => _ = Instance;

        protected override void Awake()
        {
            base.Awake();
            BuildOverlay();
        }

        // Subscribe in OnEnable / unsubscribe in OnDestroy. logMessageReceived (not the threaded
        // variant) is delivered on the main thread, so the queue needs no locking.
        private void OnEnable()
        {
            Application.logMessageReceived += OnLog;
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        protected override void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            SceneManager.activeSceneChanged -= OnSceneChanged;
            base.OnDestroy();
        }

        // The build-stamp hint is shown on the main menu only, so it never overlaps the in-game
        // HUD. F8 and the confirmation toast still work on every scene.
        private void OnSceneChanged(Scene from, Scene to) => SetHintVisible(to.name);

        private void SetHintVisible(string sceneName)
        {
            if (_hintGO != null) _hintGO.SetActive(sceneName == SceneTransitionManager.MAIN_MENU_SCENE);
        }

        // Append each log line to the ring. MUST NOT log from here, or it recurses forever.
        private void OnLog(string condition, string stackTrace, LogType type)
        {
            string tag = type switch
            {
                LogType.Error => "[ERR] ",
                LogType.Exception => "[EXC] ",
                LogType.Assert => "[ASR] ",
                LogType.Warning => "[WRN] ",
                _ => ""
            };
            _log.Enqueue(tag + condition);

            // For exceptions the stack is the useful part; keep a few frames so the report points
            // at the source without dumping the whole trace.
            if (type == LogType.Exception && !string.IsNullOrEmpty(stackTrace))
            {
                var frames = stackTrace.Split('\n');
                int n = Mathf.Min(4, frames.Length);
                for (int i = 0; i < n; i++)
                    if (!string.IsNullOrWhiteSpace(frames[i])) _log.Enqueue("    " + frames[i].Trim());
            }

            while (_log.Count > MaxLines) _log.Dequeue();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f8Key.wasPressedThisFrame) WriteReport();
        }

        private void WriteReport()
        {
            string report = BuildReport();

            // Clipboard first, so even if the file write fails the tester can still paste it.
            GUIUtility.systemCopyBuffer = report;

            // Timestamped file next to Player.log / save data, so reports never overwrite.
            string savedAt;
            try
            {
                string fileName = $"playtest_report_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt";
                savedAt = System.IO.Path.Combine(Application.persistentDataPath, fileName);
                System.IO.File.WriteAllText(savedAt, report);
            }
            catch (System.Exception e)
            {
                savedAt = "(file write failed: " + e.Message + ")";
            }

            ShowToast($"Bug report copied to clipboard and saved.\nPaste it into the bug channel.\n{savedAt}");
        }

        private string BuildReport()
        {
            var sb = new StringBuilder(4096);
            sb.AppendLine("=== RhythmRogue playtest report ===");
            sb.AppendLine($"When:    {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Version: {Application.version}  build {Application.buildGUID}");
            sb.AppendLine($"Unity:   {Application.unityVersion}  ({Application.platform})");
            sb.AppendLine($"Scene:   {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            int fps = Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.smoothDeltaTime));
            sb.AppendLine($"Screen:  {Screen.width}x{Screen.height}  ~{fps} fps");

            AppendRunContext(sb);

            sb.AppendLine();
            sb.AppendLine("--- recent log (oldest first) ---");
            int skip = Mathf.Max(0, _log.Count - ReportLineCount);
            int i = 0;
            foreach (var line in _log)
            {
                if (i++ < skip) continue;
                sb.AppendLine(line);
            }
            sb.AppendLine("--- end ---");
            return sb.ToString();
        }

        private void AppendRunContext(StringBuilder sb)
        {
            RunState run = DevAssets.FindScriptableObject<RunState>();
            if (run == null) { sb.AppendLine("Run:     (no RunState loaded)"); return; }

            sb.AppendLine($"Seed:    {(string.IsNullOrEmpty(run.Seed) ? "(none)" : run.Seed)}");
            sb.AppendLine($"Run:     active={run.IsRunActive}  tier={run.Tier}  won={run.WasVictory}");
            string area = run.CurrentArea != null ? run.CurrentArea.areaName : "(none)";
            sb.AppendLine($"Area:    {area}  battlesWon={run.BattlesWon}  currency={run.Currency}");

            if (run.SelectedNode != null)
                sb.AppendLine($"Node:    L{run.SelectedNode.Layer} {run.SelectedNode.Type}");

            if (run.ActiveRelics != null && run.ActiveRelics.Count > 0)
            {
                sb.Append("Relics:  ");
                for (int i = 0; i < run.ActiveRelics.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(run.ActiveRelics[i] != null ? run.ActiveRelics[i].relicName : "null");
                }
                sb.AppendLine();
            }

            var ph = Object.FindFirstObjectByType<PlayerHealth>();
            if (ph != null) sb.AppendLine($"Player:  HP {ph.CurrentHP}/{ph.MaxHP}");
        }

        // ---- overlay (persistent hint label + transient toast) ----

        private void BuildOverlay()
        {
            var canvasGO = new GameObject("PlaytestReporterCanvas");
            canvasGO.transform.SetParent(transform, false);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 9000; // above the HUD, below the CRT bezel (10000)
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            // No GraphicRaycaster: nothing here is interactive, and leaving it off guarantees the
            // overlay can never eat a click meant for the game beneath.
            var canvasRT = canvasGO.GetComponent<RectTransform>();

            // Persistent build stamp + key hint, bottom-right, dim and small so it stays out of the
            // way. Doubles as the version stamp; only ever present in editor / dev builds.
            string build = string.IsNullOrEmpty(Application.buildGUID)
                ? "editor"
                : Application.buildGUID.Substring(0, Mathf.Min(7, Application.buildGUID.Length));
            var hint = MakeText(canvasRT, "Hint", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-40f, 40f), new Vector2(700f, 30f), 18, TextAnchor.MiddleRight,
                new Color(UIHelpers.OffWhite.r, UIHelpers.OffWhite.g, UIHelpers.OffWhite.b, 0.5f));
            hint.text = $"RhythmRogue v{Application.version} ({build})   F8 = report bug";
            _hintGO = hint.gameObject;

            // Transient confirmation toast, bottom-center, hidden until F8.
            var panelGO = new GameObject("ToastPanel", typeof(RectTransform), typeof(Image));
            panelGO.transform.SetParent(canvasRT, false);
            var pRT = panelGO.GetComponent<RectTransform>();
            pRT.anchorMin = new Vector2(0.5f, 0f); pRT.anchorMax = new Vector2(0.5f, 0f); pRT.pivot = new Vector2(0.5f, 0f);
            pRT.anchoredPosition = new Vector2(0f, 130f);
            pRT.sizeDelta = new Vector2(1200f, 150f);
            var pImg = panelGO.GetComponent<Image>();
            pImg.color = new Color(UIHelpers.BgDeep.r, UIHelpers.BgDeep.g, UIHelpers.BgDeep.b, 0.94f);
            pImg.raycastTarget = false;

            _toast = MakeText(pRT, "ToastText", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, 26, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
            _toast.horizontalOverflow = HorizontalWrapMode.Wrap;
            var tRT = _toast.rectTransform;
            tRT.offsetMin = new Vector2(24, 16); tRT.offsetMax = new Vector2(-24, -16);

            panelGO.SetActive(false);
            _toastPanel = panelGO;

            // Show the hint only if we booted straight into the main menu; scene changes update it.
            SetHintVisible(SceneManager.GetActiveScene().name);
        }

        private GameObject _toastPanel;

        private void ShowToast(string message)
        {
            if (_toast == null || _toastPanel == null) return;
            _toast.text = message;
            _toastPanel.SetActive(true);
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(HideToastAfter(4.5f));
        }

        private IEnumerator HideToastAfter(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
            if (_toastPanel != null) _toastPanel.SetActive(false);
            _toastRoutine = null;
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
            t.font = UIHelpers.GetDefaultFont(fontSize);
            t.fontSize = fontSize; t.alignment = align; t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }
    }
}
#endif
