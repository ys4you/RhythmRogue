using System.Collections;
using RhythmRogue.Core.Audio;
using RhythmRogue.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmRogue.Battle
{
    public class BattleUI : MonoBehaviour
    {
        [Header("Systems")]
        [SerializeField] private BattleManager _battleManager;
        [SerializeField] private ComboSystem _comboSystem;
        [SerializeField] private DamagePipeline _damagePipeline;
        [SerializeField] private EnemyHealth _enemyHealth;

        [Header("Enemy Info")]
        [SerializeField] private Data.EnemyData _fallbackEnemyData;

        [Header("Guard")]
        [Tooltip("Optional shield sprites for the guard indicator. Assign BOTH to show a shield icon " +
                 "instead of the GUARDED/EXPOSED text box. Up = guard intact, Down = guard broken.")]
        [SerializeField] private Sprite _guardUpSprite;
        [SerializeField] private Sprite _guardDownSprite;

        private Canvas _canvas;
        // HP bars use direct RectTransform width scaling rather than Image.Type.Filled,
        // because Filled mode has rendering edge cases when no sprite is assigned to the
        // Image component. Width scaling works unconditionally and produces the cleaner
        // 'traditional HP bar' look (just shrinks from right to left, no ghost trail).
        private RectTransform _playerHPFillRT;
        private Image _playerHPFill;
        private float _playerHPBarWidth;
        private Text _playerHPText;
        private RectTransform _enemyHPFillRT;
        private Image _enemyHPFill;
        private float _enemyHPBarWidth;
        private Text _enemyNameText, _bossLabel;
        private Text _enemyHPText;
        private Text _comboText, _multiplierText;
        private RectTransform _comboRect;
        private Text _guardText;
        private Image _guardBadgeBG;
        private Image _guardImage;
        private RectTransform _guardRect;
        private bool _guardUpVisual = true;
        private float _guardPopScale = 1f;
        private Color _guardDownColor;
        private Text _scoreText;
        private int _currentScore, _displayScore;
        private Text _resultText;
        private Image _resultBG;
        private float _playerHPTarget = 1f, _playerHPDisplay = 1f;
        private float _enemyHPTarget = 1f, _enemyHPDisplay = 1f;
        private float _comboPopScale = 1f, _playerFlash, _enemyFlash;
        private PlayerHealth _playerHealth;

        private void Awake() { _playerHealth = PlayerHealth.Instance; CreateUI(); }

        private void OnEnable()
        {
            if (_playerHealth?.Health != null)
            {
                _playerHealth.Health.OnDamaged += OnPlayerDamaged;
                _playerHealth.Health.OnHealed += OnPlayerHealed;
                _playerHealth.Health.OnHPChanged += OnPlayerHPChanged;
            }
            if (_comboSystem != null)
            {
                _comboSystem.OnComboChanged += OnComboChanged;
                _comboSystem.OnComboReset += OnComboReset;
                _comboSystem.OnComboMilestone += OnComboMilestone;
            }
            if (_damagePipeline != null)
            {
                _damagePipeline.OnDamageDealt += OnDamageDealt;
                _damagePipeline.OnGuardChanged += OnGuardChanged;
            }
        }

        private void Start()
        {
            // NOTE: We deliberately do NOT subscribe to _enemyHealth.Health events here.
            // _enemyHealth.Health is created inside EnemyHealth.InitForBattle(), which is
            // called from BattleManager.Start() - and Unity does not guarantee that runs
            // before BattleUI.Start(). Any subscription attempt here would silently no-op
            // against the null Health, then never get retried once Health is created.
            // Instead, the enemy HP bar polls _enemyHealth.HPPercent every frame in
            // LateUpdate. The cost is one property access; the gain is no race condition.
            UpdatePlayerHP(); UpdateEnemyHP(); SetCombo(0, 1f); SetScore(0);
            SetGuardVisual(_damagePipeline == null || _damagePipeline.GuardUp, animate: false);

            var enemyData = _battleManager?.CurrentEnemy ?? _fallbackEnemyData;
            if (enemyData != null)
            {
                _enemyNameText.text = enemyData.enemyName;
                if (enemyData.IsBoss) { _bossLabel.text = "BOSS"; _bossLabel.gameObject.SetActive(true); _enemyNameText.color = UIHelpers.RustOrange; }
                else _bossLabel.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            if (_playerHealth?.Health != null)
            {
                _playerHealth.Health.OnDamaged -= OnPlayerDamaged;
                _playerHealth.Health.OnHealed -= OnPlayerHealed;
                _playerHealth.Health.OnHPChanged -= OnPlayerHPChanged;
            }
            if (_comboSystem != null) { _comboSystem.OnComboChanged -= OnComboChanged; _comboSystem.OnComboReset -= OnComboReset; _comboSystem.OnComboMilestone -= OnComboMilestone; }
            if (_damagePipeline != null)
            {
                _damagePipeline.OnDamageDealt -= OnDamageDealt;
                _damagePipeline.OnGuardChanged -= OnGuardChanged;
            }
            // Enemy events were never subscribed (see Start() comment), so nothing to unwire.
        }

        private void LateUpdate()
        {
            // Poll enemy HP each frame. EnemyHealth.Health is created mid-lifecycle
            // (during BattleManager.Start), so subscribing at BattleUI.Start would race
            // the creation and silently no-op. Polling sidesteps the order entirely.
            if (_enemyHealth?.Health != null)
            {
                float newTarget = _enemyHealth.HPPercent;
                if (newTarget < _enemyHPTarget - 0.001f) _enemyFlash = 1f;
                _enemyHPTarget = newTarget;
                if (_enemyHPText != null) _enemyHPText.text = $"{_enemyHealth.CurrentHP}/{_enemyHealth.MaxHP}";
            }

            // Lerp displayed HP toward the target. Lerp factor 8 gives a quick but visible
            // animation: a Perfect-tier hit drops the bar over ~150ms which reads as
            // 'something happened' without feeling lazy.
            _playerHPDisplay = Mathf.Lerp(_playerHPDisplay, _playerHPTarget, Time.deltaTime * 8f);
            _enemyHPDisplay = Mathf.Lerp(_enemyHPDisplay, _enemyHPTarget, Time.deltaTime * 8f);

            // Direct width scaling. Pivot of the fill RectTransform is (0, 0.5) so changing
            // sizeDelta.x shrinks the bar from the right toward the left, which is the
            // 'traditional HP bar' visual the player expects.
            ApplyHPBarWidth(_playerHPFillRT, _playerHPBarWidth, _playerHPDisplay);
            ApplyHPBarWidth(_enemyHPFillRT, _enemyHPBarWidth, _enemyHPDisplay);

            _playerHPFill.color = UIHelpers.HPColor(_playerHPDisplay);
            _enemyHPFill.color = UIHelpers.HPColor(_enemyHPDisplay);

            if (_playerFlash > 0f) { _playerFlash -= Time.deltaTime * 4f; _playerHPFill.color = Color.Lerp(_playerHPFill.color, UIHelpers.OffWhite, _playerFlash); }
            if (_enemyFlash > 0f) { _enemyFlash -= Time.deltaTime * 4f; _enemyHPFill.color = Color.Lerp(_enemyHPFill.color, UIHelpers.OffWhite, _enemyFlash); }
            if (_comboPopScale > 1f) { _comboPopScale = Mathf.Lerp(_comboPopScale, 1f, Time.deltaTime * 10f); _comboRect.localScale = Vector3.one * _comboPopScale; }
            if (_guardPopScale > 1f) { _guardPopScale = Mathf.Lerp(_guardPopScale, 1f, Time.deltaTime * 10f); if (_guardRect != null) _guardRect.localScale = Vector3.one * _guardPopScale; }

            if (_displayScore < _currentScore)
            {
                _displayScore = (int)Mathf.Lerp(_displayScore, _currentScore, Time.deltaTime * 12f);
                if (_currentScore - _displayScore < 2) _displayScore = _currentScore;
                _scoreText.text = _displayScore.ToString();
            }
        }

        /// <summary>
        /// Set the width of an HP-bar fill RectTransform based on a 0-1 percentage of
        /// the original maximum width. RectTransform anchored to top-left + bottom-left
        /// of its parent with pivot (0, 0.5), so sizeDelta.x is the literal pixel width
        /// of the bar (no anchor math needed).
        /// </summary>
        private static void ApplyHPBarWidth(RectTransform fillRT, float maxWidth, float percent)
        {
            if (fillRT == null) return;
            float w = Mathf.Max(0f, maxWidth * Mathf.Clamp01(percent));
            var sd = fillRT.sizeDelta;
            fillRT.sizeDelta = new Vector2(w, sd.y);
        }

        private void OnPlayerDamaged(int amount, int current) => _playerFlash = 1f;
        private void OnPlayerHealed(int amount, int current) { }
        private void OnPlayerHPChanged(int current, int max) => UpdatePlayerHP();
        private void OnEnemyDamaged(int amount, int current) => _enemyFlash = 1f;
        private void OnEnemyHPChanged(int current, int max) => UpdateEnemyHP();
        private void OnComboChanged(int combo, float mult) => SetCombo(combo, mult);
        private void OnComboReset(int lost) { SetCombo(0, 1f); _comboText.color = UIHelpers.RustOrange; }
        private void OnComboMilestone(int milestone)
        {
            _comboPopScale = 1.5f;
            _comboText.color = UIHelpers.WarmGold;
            var mgr = AudioManager.Instance;
            if (mgr != null) mgr.Play(SfxId.ComboMilestone);
        }
        private void OnDamageDealt(DamageResult result) { if (!result.IsPlayerDamage) _currentScore += result.Amount; }

        private void OnGuardChanged(bool up) => SetGuardVisual(up, animate: true);

        /// <summary>
        /// Reflect guard state on the bottom-left badge. Guarded reads as a bright, filled badge
        /// with dark text; exposed flips it to a dim badge with an amber "EXPOSED" warning (no red
        /// in the palette). The transition pops the badge so a guard break catches the eye even
        /// while the player is watching the highway.
        /// </summary>
        private void SetGuardVisual(bool up, bool animate)
        {
            _guardUpVisual = up;
            bool haveSprites = _guardUpSprite != null && _guardDownSprite != null;

            // Sprite mode: show the shield icon, hide the text box. Text mode: the reverse.
            if (_guardImage != null)
            {
                _guardImage.enabled = haveSprites;
                if (haveSprites)
                {
                    _guardImage.sprite = up ? _guardUpSprite : _guardDownSprite;
                    _guardImage.color = Color.white;
                }
            }
            if (_guardBadgeBG != null) _guardBadgeBG.enabled = !haveSprites;
            if (_guardText != null) _guardText.enabled = !haveSprites;

            if (!haveSprites && _guardText != null && _guardBadgeBG != null)
            {
                if (up)
                {
                    _guardText.text = "GUARDED";
                    _guardText.color = UIHelpers.BgDeep;
                    _guardBadgeBG.color = UIHelpers.WarmGold;
                }
                else
                {
                    _guardText.text = "EXPOSED";
                    _guardText.color = UIHelpers.AmberOrange;
                    _guardBadgeBG.color = _guardDownColor;
                }
            }

            if (animate) _guardPopScale = 1.4f;
        }

        private void UpdatePlayerHP()
        {
            if (_playerHealth == null) return;
            _playerHPTarget = _playerHealth.HPPercent;
            _playerHPText.text = $"{_playerHealth.CurrentHP}/{_playerHealth.MaxHP}";
        }

        private void UpdateEnemyHP()
        {
            if (_enemyHealth?.Health == null) return;
            _enemyHPTarget = _enemyHealth.HPPercent;
        }

        private void SetCombo(int combo, float mult)
        {
            _comboText.text = combo > 0 ? combo.ToString() : "";
            _multiplierText.text = combo > 0 ? $"x{mult:F1}" : "";
            _comboText.color = UIHelpers.OffWhite;
            if (combo > 0) _comboPopScale = 1.2f;
        }

        private void SetScore(int score) { _currentScore = score; _displayScore = score; _scoreText.text = score.ToString(); }

        public void ShowResult(bool victory)
        {
            _resultBG.gameObject.SetActive(true);
            _resultText.text = victory ? "VICTORY" : "DEFEATED";
            _resultText.color = victory ? UIHelpers.WarmGold : UIHelpers.RustOrange;
            StartCoroutine(AnimateResult());
        }

        private IEnumerator AnimateResult()
        {
            RectTransform rt = _resultText.GetComponent<RectTransform>();
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                rt.localScale = Vector3.one * Mathf.Lerp(2f, 1f, t / 0.3f);
                Color c = _resultBG.color; c.a = Mathf.Lerp(0f, 0.6f, t / 0.3f); _resultBG.color = c;
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        private void CreateUI()
        {
            var canvasGO = new GameObject("BattleCanvas");
            canvasGO.transform.SetParent(transform);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();

            // Enemy name + HP (top center, inset from CRT bezel)
            _enemyNameText = CreateText(canvasRT, "EnemyName", "Enemy",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -50), new Vector2(600, 50), 32, TextAnchor.MiddleCenter, UIHelpers.OffWhite);

            _bossLabel = CreateText(canvasRT, "BossLabel", "",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-325, -50), new Vector2(250, 50), 32, TextAnchor.MiddleRight, UIHelpers.RustOrange);
            _bossLabel.fontStyle = FontStyle.Bold;
            _bossLabel.gameObject.SetActive(false);

            _enemyHPBarWidth = CreateHPBar(canvasRT, "EnemyHP",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -95), new Vector2(600, 30),
                UIHelpers.RustOrange, out _enemyHPFill, out _enemyHPFillRT);

            // Absolute HP number centered on the enemy bar, mirroring the player's "100/100"
            // readout. With flat authored HP this is a real, legible value (not a percentage),
            // so the player can see exactly how close the enemy is to dead.
            _enemyHPText = CreateText(canvasRT, "EnemyHPText", "",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -95), new Vector2(600, 30), 20, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
            _enemyHPText.fontStyle = FontStyle.Bold;

            // Player HP (bottom-left, inset from CRT bezel)
            _playerHPText = CreateText(canvasRT, "PlayerHPText", "100/100",
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(50, 100), new Vector2(300, 50), 26, TextAnchor.MiddleLeft, UIHelpers.OffWhite);

            _playerHPBarWidth = CreateHPBar(canvasRT, "PlayerHP",
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(50, 55), new Vector2(400, 25),
                UIHelpers.WarmGold, out _playerHPFill, out _playerHPFillRT);

            // Guard indicator (top-left, clear of the receptor row so it never covers a lane).
            // Defaults to a bright gold "GUARDED" box that flips to a dim "EXPOSED" the instant a
            // Miss drops the guard. If both shield sprites are assigned in the inspector it shows a
            // shield icon instead (intact vs broken). Rule lives in DamagePipeline (starts up,
            // drops on Miss, restored on the next hit).
            _guardDownColor = new Color(UIHelpers.BgSurface.r, UIHelpers.BgSurface.g, UIHelpers.BgSurface.b, 0.85f);
            var guardBadge = CreatePanel(canvasRT, "GuardBadge",
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(50, -50), new Vector2(200, 46), UIHelpers.WarmGold);
            _guardBadgeBG = guardBadge.GetComponent<Image>();
            _guardRect = guardBadge.GetComponent<RectTransform>();

            _guardText = CreateText(_guardRect, "GuardLabel", "GUARDED",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(200, 46), 24, TextAnchor.MiddleCenter, UIHelpers.BgDeep);
            _guardText.fontStyle = FontStyle.Bold;

            // Shield icon, shown only when both sprites are wired (otherwise the text box is used).
            // Anchored to the badge's left edge so it sits in the corner once the box is hidden.
            var guardIcon = new GameObject("GuardIcon", typeof(RectTransform), typeof(Image));
            guardIcon.transform.SetParent(_guardRect, false);
            var guardIconRT = guardIcon.GetComponent<RectTransform>();
            guardIconRT.anchorMin = new Vector2(0f, 0.5f);
            guardIconRT.anchorMax = new Vector2(0f, 0.5f);
            guardIconRT.pivot = new Vector2(0f, 0.5f);
            guardIconRT.anchoredPosition = Vector2.zero;
            guardIconRT.sizeDelta = new Vector2(64, 64);
            _guardImage = guardIcon.GetComponent<Image>();
            _guardImage.preserveAspect = true;
            _guardImage.enabled = false;

            // Combo (right side, inset from CRT bezel)
            var comboGroup = CreatePanel(canvasRT, "ComboGroup",
                new Vector2(1, 0.4f), new Vector2(1, 0.4f), new Vector2(1, 0.4f),
                new Vector2(-70, 0), new Vector2(200, 120), new Color(0, 0, 0, 0));
            _comboRect = comboGroup.GetComponent<RectTransform>();

            _comboText = CreateText(_comboRect, "ComboNum", "",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -5), new Vector2(200, 70), 48, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
            _comboText.fontStyle = FontStyle.Bold;

            _multiplierText = CreateText(_comboRect, "MultText", "",
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 5), new Vector2(200, 50), 26, TextAnchor.MiddleCenter, UIHelpers.WarmGold);

            // Score (top-right, inset from CRT bezel)
            _scoreText = CreateText(canvasRT, "Score", "0",
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-50, -50), new Vector2(300, 50), 32, TextAnchor.MiddleRight, UIHelpers.AmberOrange);

            // Result overlay (fullscreen, unaffected by bezel)
            var resultBGObj = CreatePanel(canvasRT, "ResultBG",
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(UIHelpers.BgDeep.r, UIHelpers.BgDeep.g, UIHelpers.BgDeep.b, 0.6f));
            _resultBG = resultBGObj.GetComponent<Image>();
            var resultBGRT = resultBGObj.GetComponent<RectTransform>();
            resultBGRT.offsetMin = Vector2.zero; resultBGRT.offsetMax = Vector2.zero;

            _resultText = CreateText(resultBGRT, "ResultText", "",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1000, 150), 72, TextAnchor.MiddleCenter, UIHelpers.OffWhite);
            _resultText.fontStyle = FontStyle.Bold;
            resultBGObj.SetActive(false);
        }

        /// <summary>
        /// Create an HP bar consisting of a background panel + a fill panel.
        /// The fill is anchored to the top-left + bottom-left of the background with
        /// pivot (0, 0.5), so its sizeDelta.x maps directly to displayed pixel width.
        /// We resize that sizeDelta in LateUpdate; no Image.fillAmount, no sprite
        /// requirements, no rendering edge cases.
        ///
        /// Returns the inner-bar maximum width (after the 2px inset padding), which
        /// LateUpdate multiplies by HP percent to get the current displayed width.
        /// </summary>
        private float CreateHPBar(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 pos, Vector2 size, Color fillColor,
            out Image fill, out RectTransform fillRT)
        {
            var bgObj = CreatePanel(parent, name + "_BG", anchorMin, anchorMax, pivot, pos, size, new Color(UIHelpers.BgSurface.r, UIHelpers.BgSurface.g, UIHelpers.BgSurface.b, 0.8f));
            var bgRT = bgObj.GetComponent<RectTransform>();

            const float pad = 2f;
            float innerWidth = size.x - pad * 2f;
            float innerHeight = size.y - pad * 2f;

            var fillObj = new GameObject(name + "_Fill", typeof(RectTransform), typeof(Image));
            fillObj.transform.SetParent(bgRT, false);
            fillRT = fillObj.GetComponent<RectTransform>();
            // Anchor to the left edge of the background so the bar 'grows from the left'.
            // Pivot (0, 0.5) means sizeDelta.x IS the rendered width in pixels.
            fillRT.anchorMin = new Vector2(0, 0.5f);
            fillRT.anchorMax = new Vector2(0, 0.5f);
            fillRT.pivot = new Vector2(0, 0.5f);
            fillRT.anchoredPosition = new Vector2(pad, 0);
            fillRT.sizeDelta = new Vector2(innerWidth, innerHeight);

            fill = fillObj.GetComponent<Image>();
            fill.color = fillColor;
            // Image stays Simple (default). No sprite needed; Unity renders a solid colour
            // rectangle which is exactly what we want.
            return innerWidth;
        }

        private static GameObject CreatePanel(RectTransform parent, string name, Vector2 ancMin, Vector2 ancMax, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = ancMin; rt.anchorMax = ancMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            obj.GetComponent<Image>().color = color; return obj;
        }

        private static Text CreateText(RectTransform parent, string name, string text, Vector2 ancMin, Vector2 ancMax, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize, TextAnchor alignment, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = ancMin; rt.anchorMax = ancMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = obj.GetComponent<Text>();
            t.text = text; t.fontSize = fontSize; t.font = UIHelpers.GetDefaultFont(fontSize);
            t.alignment = alignment; t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}