#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using RhythmRogue.Core;
using RhythmRogue.Data;
using RhythmRogue.Battle;
using RhythmRogue.Util;

namespace RhythmRogue.DevTools
{
    /// <summary>
    /// Dev-only cheat panel. Toggle with F10.
    ///
    /// Mutates run and battle state so you can skip the grind while testing: add or spend
    /// currency, own or disown any relic in the pool, heal / hurt / revive the player, instant
    /// win (kill the enemy) or lose (kill the player), and scale time. Read-only state already
    /// lives in the per-system overlays (BattleDebugOverlay on F3, etc.); this is the ACTIONS
    /// counterpart, not a duplicate of those.
    ///
    /// Self-bootstrapping: a RuntimeInitializeOnLoadMethod spawns one DontDestroyOnLoad instance,
    /// so there is nothing to place or wire in any scene. The ENTIRE file is wrapped in
    /// UNITY_EDITOR || DEVELOPMENT_BUILD, so it does not exist in release builds at all.
    ///
    /// How it reaches state without scene references (intentional, and only acceptable because
    /// this is an excluded-from-build dev tool):
    ///   - RunState and RelicPool are ScriptableObjects -> located via DevAssets (a loaded instance,
    ///     or the project asset in-editor), so they resolve even on scenes that do not reference them.
    ///   - PlayerHealth / EnemyHealth -> FindFirstObjectByType (NON-creating: PlayerHealth.Instance
    ///     would auto-spawn a stray singleton in scenes that have no battle, so it is avoided here).
    ///   - Any cheat whose target is absent simply shows a note instead of a button.
    ///
    /// A shipping feature must never reach across systems like this; the isolation + build
    /// exclusion is what keeps it from polluting the real architecture.
    /// </summary>
    public class DevCheatPanel : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("[DevCheatPanel]");
            go.AddComponent<DevCheatPanel>();
            DontDestroyOnLoad(go);
        }

        private bool _visible;
        private Rect _window = new Rect(12, 44, 260, 120);
        private Vector2 _relicScroll;
        private GUIStyle _header;

        // Lazily resolved shared state. Re-resolved whenever the cached reference goes null
        // (Unity reports destroyed objects as null), so scene changes are handled for free.
        private RunState _runState;
        private RelicPool _relicPool;
        private PlayerHealth _player;
        private EnemyHealth _enemy;

        private float _fps;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f10Key.wasPressedThisFrame)
            {
                _visible = !_visible;
                GameLog.Info($"[DevCheatPanel] {(_visible ? "shown" : "hidden")}");
            }

            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) _fps = Mathf.Lerp(_fps, 1f / dt, 0.1f);
        }

        private RunState RunStateRef
        {
            get
            {
                if (_runState == null) _runState = DevAssets.FindScriptableObject<RunState>();
                return _runState;
            }
        }

        private RelicPool RelicPoolRef
        {
            get
            {
                if (_relicPool == null) _relicPool = DevAssets.FindScriptableObject<RelicPool>();
                return _relicPool;
            }
        }

        private PlayerHealth Player
        {
            get
            {
                if (_player == null) _player = Object.FindFirstObjectByType<PlayerHealth>();
                return _player;
            }
        }

        private EnemyHealth Enemy
        {
            get
            {
                if (_enemy == null) _enemy = Object.FindFirstObjectByType<EnemyHealth>();
                return _enemy;
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;
            if (_header == null)
                _header = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = Color.yellow } };

            _window = GUILayout.Window(0x5EED, _window, DrawWindow, "DEV CHEATS (F10)");
        }

        private void DrawWindow(int id)
        {
            var rs = RunStateRef;
            GUILayout.Label($"FPS {_fps:F0}   scene {SceneManager.GetActiveScene().name}");
            if (rs != null) GUILayout.Label($"seed {(string.IsNullOrEmpty(rs.Seed) ? "-" : rs.Seed)}   run {(rs.IsRunActive ? "active" : "idle")}");

            DrawCurrency(rs);
            DrawHealth();
            DrawTime();
            DrawRelics(rs);
            DrawOnboarding();

            GUI.DragWindow(new Rect(0, 0, 100000, 20));
        }

        private void DrawCurrency(RunState rs)
        {
            GUILayout.Space(4);
            GUILayout.Label("CURRENCY", _header);
            if (rs == null) { GUILayout.Label("no RunState loaded"); return; }
            GUILayout.Label($"Beats: {rs.Currency}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+50")) rs.AddCurrency(50);
            if (GUILayout.Button("+200")) rs.AddCurrency(200);
            if (GUILayout.Button("-50")) rs.TrySpendCurrency(50);
            GUILayout.EndHorizontal();
        }

        private void DrawHealth()
        {
            GUILayout.Space(4);
            GUILayout.Label("HEALTH", _header);

            var ph = Player;
            if (ph != null)
            {
                GUILayout.Label($"Player {ph.CurrentHP}/{ph.MaxHP}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Heal full")) ph.Heal(ph.MaxHP);
                if (GUILayout.Button("Hurt 10")) ph.TakeDamage(10);
                if (GUILayout.Button("Revive")) ph.ResetForNewRun();
                GUILayout.EndHorizontal();
            }
            else GUILayout.Label("no PlayerHealth (start a battle)");

            var en = Enemy;
            if (en != null && en.Health != null)
            {
                GUILayout.Label($"Enemy {en.CurrentHP}/{en.MaxHP}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Kill enemy (win)")) en.TakeDamage(en.CurrentHP);
                if (ph != null && GUILayout.Button("Kill player (lose)")) ph.TakeDamage(ph.CurrentHP);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawTime()
        {
            GUILayout.Space(4);
            GUILayout.Label($"TIME  x{Time.timeScale:F2}", _header);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("0.25")) Time.timeScale = 0.25f;
            if (GUILayout.Button("0.5")) Time.timeScale = 0.5f;
            if (GUILayout.Button("1x")) Time.timeScale = 1f;
            if (GUILayout.Button("2x")) Time.timeScale = 2f;
            GUILayout.EndHorizontal();
        }

        private void DrawRelics(RunState rs)
        {
            GUILayout.Space(4);
            GUILayout.Label("RELICS  (own / disown)", _header);
            var pool = RelicPoolRef;
            if (rs == null) { GUILayout.Label("no RunState"); return; }
            if (pool == null) { GUILayout.Label("no RelicPool asset found"); return; }

            GUILayout.Label($"owned: {rs.ActiveRelics.Count}   (effects apply next battle)");
            _relicScroll = GUILayout.BeginScrollView(_relicScroll, GUILayout.Height(150));
            foreach (var relic in pool.AllRelics)
            {
                if (relic == null) continue;
                bool owned = rs.ActiveRelics.Contains(relic);
                bool now = GUILayout.Toggle(owned, $" {relic.relicName}");
                if (now && !owned) rs.ActiveRelics.Add(relic);
                else if (!now && owned) rs.ActiveRelics.Remove(relic);
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button("Clear all relics")) rs.ActiveRelics.Clear();
        }

        private void DrawOnboarding()
        {
            GUILayout.Space(4);
            GUILayout.Label("ONBOARDING", _header);
            GUILayout.Label($"first-launch flag: {(OnboardingState.IsComplete ? "set (won't auto-show)" : "unset (will auto-show)")}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset (force next launch)"))
            {
                OnboardingState.Reset();
                GameLog.Info("[DevCheatPanel] Onboarding flag cleared; next launch will force the onboarding.");
            }
            if (GUILayout.Button("Mark seen"))
            {
                OnboardingState.MarkComplete();
                GameLog.Info("[DevCheatPanel] Onboarding flag set.");
            }
            GUILayout.EndHorizontal();
        }
    }
}
#endif
