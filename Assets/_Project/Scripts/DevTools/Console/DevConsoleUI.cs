#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using RhythmRogue.Util.Console;

namespace RhythmRogue.DevTools.Console
{
    /// <summary>
    /// IMGUI view for the DeveloperConsole. Toggle with the backquote / tilde key. Self-bootstraps
    /// one DontDestroyOnLoad instance, so there is nothing to wire in any scene, and the whole file
    /// is compiled out of release builds. The view owns only presentation and input; all behaviour
    /// lives in DeveloperConsole and the registered IConsoleCommands.
    ///
    /// Keys: backquote toggles, Enter runs, Up/Down recall history, Esc closes. Input is read via
    /// IMGUI events; note the new Input System reads devices directly, so gameplay bound to the same
    /// keys can still fire while you type. Fine for editor testing.
    /// </summary>
    public class DevConsoleUI : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("[DevConsole]");
            go.AddComponent<DevConsoleUI>();
            DontDestroyOnLoad(go);
        }

        private const float Height = 260f;
        private const string InputControl = "DevConsoleInput";

        private DeveloperConsole _console;
        private DevConsoleContext _context;

        private bool _visible;
        private string _input = "";
        private Vector2 _scroll;
        private bool _focusInput;
        private bool _caretToEnd;
        private int _historyIndex = -1;

        private void Awake()
        {
            _context = new DevConsoleContext();
            _console = new DeveloperConsole();
            GameConsoleCommands.RegisterAll(_console, _context);
            _console.Print("dev console ready. type 'help'. toggle with ` (backquote).");
        }

        private void OnGUI()
        {
            var e = Event.current;

            // Toggle on backquote, consuming the event so the glyph never reaches the text field.
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.BackQuote)
            {
                _visible = !_visible;
                _focusInput = _visible;
                e.Use();
                return;
            }

            if (!_visible) return;

            // Handle submit / history / close before the text field consumes the key.
            if (e.type == EventType.KeyDown)
            {
                switch (e.keyCode)
                {
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        Submit(); e.Use(); break;
                    case KeyCode.UpArrow:
                        RecallHistory(-1); e.Use(); break;
                    case KeyCode.DownArrow:
                        RecallHistory(+1); e.Use(); break;
                    case KeyCode.Tab:
                        AutoComplete(); e.Use(); break;
                    case KeyCode.Escape:
                        _visible = false; e.Use(); return;
                }
            }

            GUI.Box(new Rect(0, 0, Screen.width, Height), GUIContent.none);
            GUILayout.BeginArea(new Rect(6, 4, Screen.width - 12, Height - 8));

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(Height - 40));
            var log = _console.Log;
            for (int i = 0; i < log.Count; i++) GUILayout.Label(log[i]);
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            GUILayout.Label(">", GUILayout.Width(12));
            GUI.SetNextControlName(InputControl);
            _input = GUILayout.TextField(_input);
            GUILayout.EndHorizontal();

            GUILayout.EndArea();

            // Strip any stray backquote that slipped in, keep focus, and pin scroll to the bottom.
            if (_input.IndexOf('`') >= 0) _input = _input.Replace("`", "");
            if (_focusInput) { GUI.FocusControl(InputControl); _focusInput = false; }

            // After a tab-completion, move the text caret to the end of the filled-in text.
            if (_caretToEnd && GUI.GetNameOfFocusedControl() == InputControl)
            {
                var editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                if (editor != null) { editor.text = _input; editor.MoveTextEnd(); }
                _caretToEnd = false;
            }

            if (e.type == EventType.Repaint) _scroll.y = Mathf.Infinity;
        }

        private void AutoComplete()
        {
            var result = _console.Complete(_input);
            if (result.Matches.Count > 1)
            {
                _console.Print("> " + _input);
                _console.Print(string.Join("   ", result.Matches));
            }
            _input = result.Completed;
            _focusInput = true;
            _caretToEnd = true;
        }

        private void Submit()
        {
            string line = _input;
            _input = "";
            _historyIndex = -1;
            if (!string.IsNullOrWhiteSpace(line)) _console.Execute(line);
            _focusInput = true;
        }

        // dir -1 = older, +1 = newer. Index == count means the empty current line.
        private void RecallHistory(int dir)
        {
            var history = _console.History;
            if (history.Count == 0) return;
            if (_historyIndex == -1) _historyIndex = history.Count;
            _historyIndex = Mathf.Clamp(_historyIndex + dir, 0, history.Count);
            _input = _historyIndex >= history.Count ? "" : history[_historyIndex];
        }
    }
}
#endif
