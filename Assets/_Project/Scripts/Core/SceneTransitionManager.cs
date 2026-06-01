using System;
using System.Collections;
using RhythmRogue.Util;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RhythmRogue.Core
{
    /// <summary>
    /// Scene transitions with fade-to-black overlay. Singleton with DontDestroyOnLoad.
    /// </summary>
    public class SceneTransitionManager : Util.Singleton<SceneTransitionManager>
    {
        [Header("Fade")]
        [SerializeField] private float _fadeDuration = 0.3f;

        private Image _fadeImage;
        private Canvas _fadeCanvas;
        private bool _isTransitioning;

        public bool IsTransitioning => _isTransitioning;

        public const string MAIN_MENU_SCENE = "MainMenuScene";
        public const string MAP_SCENE = "MapScene";
        public const string BATTLE_SCENE = "BattleScene";
        public const string REST_SCENE = "RestScene";
        public const string SHOP_SCENE = "ShopScene";
        public const string EVENT_SCENE = "EventScene";
        public const string SUMMARY_SCENE = "SummaryScene";

        protected override void Awake() { base.Awake(); CreateFadeOverlay(); }

        public void GoTo(string sceneName, Action onBeforeLoad = null)
        {
            if (_isTransitioning) return;
            StartCoroutine(TransitionCoroutine(sceneName, onBeforeLoad));
        }

        public void GoToMap(Action onBeforeLoad = null) => GoTo(MAP_SCENE, onBeforeLoad);
        public void GoToBattle(Action onBeforeLoad = null) => GoTo(BATTLE_SCENE, onBeforeLoad);
        public void GoToRest(Action onBeforeLoad = null) => GoTo(REST_SCENE, onBeforeLoad);
        public void GoToShop(Action onBeforeLoad = null) => GoTo(SHOP_SCENE, onBeforeLoad);
        public void GoToEvent(Action onBeforeLoad = null) => GoTo(EVENT_SCENE, onBeforeLoad);
        public void GoToSummary(Action onBeforeLoad = null) => GoTo(SUMMARY_SCENE, onBeforeLoad);

        private IEnumerator TransitionCoroutine(string sceneName, Action onBeforeLoad)
        {
            _isTransitioning = true;
            yield return FadeCoroutine(0f, 1f, _fadeDuration);
            onBeforeLoad?.Invoke();

            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null) { GameLog.Error($"[SceneTransition] Failed: {sceneName}"); _isTransitioning = false; yield break; }
            while (!op.isDone) yield return null;
            yield return null;

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
                Color c = _fadeImage.color; c.a = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                _fadeImage.color = c;
                yield return null;
            }
            Color f = _fadeImage.color; f.a = to; _fadeImage.color = f;
            if (to <= 0f) _fadeCanvas.gameObject.SetActive(false);
        }

        private void CreateFadeOverlay()
        {
            var canvasGO = new GameObject("FadeOverlay");
            canvasGO.transform.SetParent(transform);
            _fadeCanvas = canvasGO.AddComponent<Canvas>();
            _fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _fadeCanvas.sortingOrder = 9999;
            canvasGO.AddComponent<CanvasScaler>();

            var imgGO = new GameObject("BlackScreen", typeof(RectTransform), typeof(Image));
            imgGO.transform.SetParent(canvasGO.transform, false);
            var rt = imgGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            _fadeImage = imgGO.GetComponent<Image>();
            _fadeImage.color = new Color(0, 0, 0, 0);
            _fadeImage.raycastTarget = true;
            _fadeCanvas.gameObject.SetActive(false);
        }
    }
}
