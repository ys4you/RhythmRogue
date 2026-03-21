using System;
using System.Collections;
using RhythmRogue.Util;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmRogue.Core
{
    /// <summary>
    /// Manages scene transitions with a fade-to-black overlay.
    /// 
    /// Singleton with DontDestroyOnLoad. Creates a persistent Canvas
    /// with a full-screen black Image for fade animations.
    /// 
    /// Prevents double scene loads by disabling input during transitions.
    /// 
    /// Usage:
    ///   SceneTransitionManager.Instance.GoTo("BattleScene");
    ///   SceneTransitionManager.Instance.GoTo("MapScene", onBeforeLoad: () => { ... });
    /// </summary>
    public class SceneTransitionManager : Util.Singleton<SceneTransitionManager>
    {
        // =================================================================
        // CONFIG
        // =================================================================

        [Header("Fade")]
        [SerializeField] private float _fadeDuration = 0.3f;

        // =================================================================
        // STATE
        // =================================================================

        private Image _fadeImage;
        private Canvas _fadeCanvas;
        private bool _isTransitioning;

        /// <summary>Whether a scene transition is currently in progress.</summary>
        public bool IsTransitioning => _isTransitioning;

        // =================================================================
        // SCENE NAMES — central place to change them
        // =================================================================

        public const string MAIN_MENU_SCENE = "MainMenuScene";
        public const string MAP_SCENE = "MapScene";
        public const string BATTLE_SCENE = "BattleScene";
        public const string REST_SCENE = "RestScene";
        public const string SUMMARY_SCENE = "SummaryScene";

        // =================================================================
        // LIFECYCLE
        // =================================================================

        protected override void Awake()
        {
            base.Awake();
            CreateFadeOverlay();
        }

        // =================================================================
        // PUBLIC API
        // =================================================================

        /// <summary>
        /// Transition to a scene with fade.
        /// </summary>
        /// <param name="sceneName">Target scene name.</param>
        /// <param name="onBeforeLoad">
        /// Optional callback invoked after fade-out, before scene load.
        /// Use to set up data for the target scene.
        /// </param>
        public void GoTo(string sceneName, Action onBeforeLoad = null)
        {
            if (_isTransitioning)
            {
                GameLog.Warn("[SceneTransition] Already transitioning, ignoring.");
                return;
            }

            StartCoroutine(TransitionCoroutine(sceneName, onBeforeLoad));
        }

        /// <summary>
        /// Convenience: go to map scene.
        /// </summary>
        public void GoToMap(Action onBeforeLoad = null)
        {
            GoTo(MAP_SCENE, onBeforeLoad);
        }

        /// <summary>
        /// Convenience: go to battle scene.
        /// </summary>
        public void GoToBattle(Action onBeforeLoad = null)
        {
            GoTo(BATTLE_SCENE, onBeforeLoad);
        }

        /// <summary>
        /// Convenience: go to rest scene.
        /// </summary>
        public void GoToRest(Action onBeforeLoad = null)
        {
            GoTo(REST_SCENE, onBeforeLoad);
        }

        /// <summary>
        /// Convenience: go to summary scene.
        /// </summary>
        public void GoToSummary(Action onBeforeLoad = null)
        {
            GoTo(SUMMARY_SCENE, onBeforeLoad);
        }

        // =================================================================
        // FADE COROUTINE
        // =================================================================

        private IEnumerator TransitionCoroutine(string sceneName, Action onBeforeLoad)
        {
            _isTransitioning = true;

            // Fade out (transparent → black)
            yield return FadeCoroutine(0f, 1f, _fadeDuration);

            // Callback before load
            onBeforeLoad?.Invoke();

            // Load scene
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);

            if (op == null)
            {
                GameLog.Error($"[SceneTransition] Failed to load scene: {sceneName}");
                _isTransitioning = false;
                yield break;
            }

            while (!op.isDone)
                yield return null;

            // Wait one frame for scene to initialize
            yield return null;

            // Fade in (black → transparent)
            yield return FadeCoroutine(1f, 0f, _fadeDuration);

            _isTransitioning = false;
        }

        private IEnumerator FadeCoroutine(float from, float to, float duration)
        {
            _fadeCanvas.gameObject.SetActive(true);

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float alpha = Mathf.Lerp(from, to, t);

                Color c = _fadeImage.color;
                c.a = alpha;
                _fadeImage.color = c;

                yield return null;
            }

            // Ensure final value
            Color final_ = _fadeImage.color;
            final_.a = to;
            _fadeImage.color = final_;

            // Hide overlay when fully transparent
            if (to <= 0f)
                _fadeCanvas.gameObject.SetActive(false);
        }

        // =================================================================
        // FADE OVERLAY CREATION
        // =================================================================

        private void CreateFadeOverlay()
        {
            GameObject canvasGO = new GameObject("FadeOverlay");
            canvasGO.transform.SetParent(transform);

            _fadeCanvas = canvasGO.AddComponent<Canvas>();
            _fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _fadeCanvas.sortingOrder = 9999; // Always on top

            canvasGO.AddComponent<CanvasScaler>();

            // Full-screen black image
            GameObject imgGO = new GameObject("BlackScreen", typeof(RectTransform), typeof(Image));
            imgGO.transform.SetParent(canvasGO.transform, false);

            RectTransform rt = imgGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _fadeImage = imgGO.GetComponent<Image>();
            _fadeImage.color = new Color(0, 0, 0, 0);
            _fadeImage.raycastTarget = true; // Block clicks during fade

            _fadeCanvas.gameObject.SetActive(false);
        }
    }
}