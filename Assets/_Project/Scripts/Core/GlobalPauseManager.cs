using UnityEngine;
using UnityEngine.SceneManagement;
using RhythmRogue.Battle;
using RhythmRogue.Util;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RhythmRogue.Core
{
    /// <summary>
    /// One persistent pause/menu overlay for the whole game. Lives across scene loads
    /// (DontDestroyOnLoad singleton), creates its own PauseMenu, and shows it on Escape
    /// in every scene EXCEPT the main menu and the battle scene.
    ///
    /// Why this design: adding a PauseMenu + controller to every scene by hand is tedious
    /// and easy to forget. Instead this single object bootstraps once and handles pause
    /// everywhere, so no scene needs any pause wiring at all.
    ///
    /// Scene policy:
    ///   - MainMenuScene: no pause (you're already at the menu).
    ///   - BattleScene: SKIPPED here. The battle has its own pause flow in BattleManager
    ///     because it must also pause/resume the Conductor (the song). A global overlay
    ///     that didn't touch the conductor would let the song keep playing while "paused".
    ///   - All other scenes (Map, Rest, Shop, Event, Summary): this manager handles pause.
    ///
    /// Setup: drop ONE GlobalPauseManager into your first-loaded scene (or let the Singleton
    /// auto-create it). That's the only setup. It persists and covers everything after.
    ///
    /// Input: reads Escape / gamepad Start via the Input System (project runs
    /// "Input System Only", so legacy UnityEngine.Input is disabled).
    ///
    /// SOLID:
    ///   S - Bridges global Escape input to a shared PauseMenu, with a scene-eligibility
    ///       policy. It does not itself build settings UI (PauseMenu does) or change scenes
    ///       beyond routing Quit.
    ///   D - Depends on PauseMenu + SceneTransitionManager abstractions.
    /// </summary>
    public class GlobalPauseManager : Singleton<GlobalPauseManager>
    {
        [Tooltip("Scenes where Escape should NOT open this overlay. Battle is excluded because " +
                 "it runs its own conductor-aware pause; main menu needs no pause.")]
        [SerializeField] private string[] _excludedScenes =
        {
            SceneTransitionManager.MAIN_MENU_SCENE,
            SceneTransitionManager.BATTLE_SCENE
        };

        private PauseMenu _pauseMenu;
        private bool _isOpen;
        private bool _eligibleScene;

        protected override void Awake()
        {
            base.Awake();
            // Only the surviving singleton instance proceeds; duplicates are destroyed by base.
            if (GlobalPauseManager.Instance != this) return;

            EnsurePauseMenu();
            SceneManager.activeSceneChanged += OnSceneChanged;
            EvaluateScene(SceneManager.GetActiveScene());
        }

        protected override void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            base.OnDestroy();
        }

        private void EnsurePauseMenu()
        {
            if (_pauseMenu != null) return;

            // Create the PauseMenu as a child so it travels with this persistent object.
            var go = new GameObject("GlobalPauseMenu");
            go.transform.SetParent(transform);
            _pauseMenu = go.AddComponent<PauseMenu>();
            _pauseMenu.OnResumeRequested += Close;
            _pauseMenu.OnQuitRequested += QuitToMenu;
            _pauseMenu.SetTitle("MENU");
        }

        private void OnSceneChanged(Scene previous, Scene next)
        {
            // Always close the overlay on a scene change so it never lingers into a new scene.
            if (_isOpen) Close();
            EvaluateScene(next);
        }

        private void EvaluateScene(Scene scene)
        {
            _eligibleScene = !IsExcluded(scene.name);
        }

        private bool IsExcluded(string sceneName)
        {
            if (_excludedScenes == null) return false;
            for (int i = 0; i < _excludedScenes.Length; i++)
                if (_excludedScenes[i] == sceneName) return true;
            return false;
        }

        private void Update()
        {
            if (!_eligibleScene) return;
            if (!MenuKeyPressedThisFrame()) return;

            if (_isOpen) Close();
            else Open();
        }

        private void Open()
        {
            if (_pauseMenu == null) return;
            _isOpen = true;
            _pauseMenu.SetTitle("MENU");
            _pauseMenu.Show();
            GameLog.Info("[GlobalPauseManager] Menu opened.");
        }

        private void Close()
        {
            if (_pauseMenu == null) return;
            _isOpen = false;
            _pauseMenu.Hide();
        }

        private void QuitToMenu()
        {
            _isOpen = false;
            if (_pauseMenu != null) _pauseMenu.Hide();
            GameLog.Info("[GlobalPauseManager] Quit to main menu.");

            var tm = SceneTransitionManager.Instance;
            if (tm != null) tm.GoTo(SceneTransitionManager.MAIN_MENU_SCENE);
            else SceneManager.LoadScene(SceneTransitionManager.MAIN_MENU_SCENE);
        }

        private static bool MenuKeyPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            if (gp != null && gp.startButton.wasPressedThisFrame) return true;
            return false;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
