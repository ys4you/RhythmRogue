using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Battle HUD controller. Creates and manages all UI elements:
    ///   - Player HP bar (bottom-left)
    ///   - Enemy HP bar (top-center) with name and BOSS label
    ///   - Combo counter + multiplier (right of highway)
    ///   - Score counter (top-right)
    ///   - Victory/Defeated overlay on battle end
    /// 
    /// Fully event-driven — subscribes to health, combo, damage,
    /// and battle manager events. No Update polling for values.
    /// 
    /// Sized for 384×216 reference resolution. Uses Unity UI Canvas
    /// with Screen Space - Overlay.
    /// </summary>
    public class BattleUI : MonoBehaviour
    {
        // =================================================================
        // REFERENCES
        // =================================================================

        [Header("Systems")]
        [SerializeField] private BattleManager _battleManager;
        [SerializeField] private ComboSystem _comboSystem;
        [SerializeField] private DamagePipeline _damagePipeline;
        [SerializeField] private EnemyHealth _enemyHealth;

        [Header("Enemy Info")]
        [Tooltip("Fallback if BattleConfig.Enemy is null (testing standalone).")]
        [SerializeField] private Data.EnemyData _fallbackEnemyData;

        // =================================================================
        // GENERATED UI REFERENCES
        // =================================================================

        private Canvas _canvas;

        // Player HP
        private Image _playerHPFill;
        private Image _playerHPGhost;
        private Text _playerHPText;

        // Enemy HP
        private Image _enemyHPFill;
        private Image _enemyHPGhost;
        private Text _enemyNameText;
        private Text _bossLabel;

        // Combo
        private Text _comboText;
        private Text _multiplierText;
        private RectTransform _comboRect;

        // Score
        private Text _scoreText;
        private int _currentScore;
        private int _displayScore;

        // Result overlay
        private Text _resultText;
        private Image _resultBG;

        // Lerp targets
        private float _playerHPTarget = 1f;
        private float _playerHPDisplay = 1f;
        private float _playerGhostTarget = 1f;
        private float _enemyHPTarget = 1f;
        private float _enemyHPDisplay = 1f;
        private float _enemyGhostTarget = 1f;

        // Animation
        private float _comboPopScale = 1f;
        private float _playerFlash;
        private float _enemyFlash;

        private PlayerHealth _playerHealth;

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Awake()
        {
            _playerHealth = PlayerHealth.Instance;
            CreateUI();
        }

        private void OnEnable()
        {
            if (_playerHealth != null && _playerHealth.Health != null)
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
                _damagePipeline.OnDamageDealt += OnDamageDealt;
        }

        private void Start()
        {
            // Subscribe to enemy health AFTER BattleManager.Start initializes it
            if (_enemyHealth != null && _enemyHealth.Health != null)
            {
                _enemyHealth.Health.OnHPChanged += OnEnemyHPChanged;
                _enemyHealth.Health.OnDamaged += OnEnemyDamaged;
            }

            // Set initial values
            UpdatePlayerHP();
            UpdateEnemyHP();
            SetCombo(0, 1f);
            SetScore(0);

            // Enemy name and boss indicator
            var enemyData = BattleConfig.Enemy ?? _fallbackEnemyData;
            if (enemyData != null)
            {
                _enemyNameText.text = enemyData.enemyName;

                if (enemyData.IsBoss)
                {
                    _bossLabel.text = "BOSS";
                    _bossLabel.gameObject.SetActive(true);
                    _enemyNameText.color = new Color(1f, 0.3f, 0.3f);
                }
                else
                {
                    _bossLabel.gameObject.SetActive(false);
                }
            }
        }

        private void OnDisable()
        {
            if (_playerHealth != null && _playerHealth.Health != null)
            {
                _playerHealth.Health.OnDamaged -= OnPlayerDamaged;
                _playerHealth.Health.OnHealed -= OnPlayerHealed;
                _playerHealth.Health.OnHPChanged -= OnPlayerHPChanged;
            }

            if (_comboSystem != null)
            {
                _comboSystem.OnComboChanged -= OnComboChanged;
                _comboSystem.OnComboReset -= OnComboReset;
                _comboSystem.OnComboMilestone -= OnComboMilestone;
            }

            if (_damagePipeline != null)
                _damagePipeline.OnDamageDealt -= OnDamageDealt;

            if (_enemyHealth != null && _enemyHealth.Health != null)
            {
                _enemyHealth.Health.OnHPChanged -= OnEnemyHPChanged;
                _enemyHealth.Health.OnDamaged -= OnEnemyDamaged;
            }
        }

        private void LateUpdate()
        {
            // Smooth HP bar lerps
            _playerHPDisplay = Mathf.Lerp(_playerHPDisplay, _playerHPTarget, Time.deltaTime * 8f);
            _playerHPFill.fillAmount = _playerHPDisplay;
            _playerGhostTarget = Mathf.Lerp(_playerGhostTarget, _playerHPTarget, Time.deltaTime * 3f);
            _playerHPGhost.fillAmount = _playerGhostTarget;

            _enemyHPDisplay = Mathf.Lerp(_enemyHPDisplay, _enemyHPTarget, Time.deltaTime * 8f);
            _enemyHPFill.fillAmount = _enemyHPDisplay;
            _enemyGhostTarget = Mathf.Lerp(_enemyGhostTarget, _enemyHPTarget, Time.deltaTime * 3f);
            _enemyHPGhost.fillAmount = _enemyGhostTarget;

            // HP bar colors based on percentage
            _playerHPFill.color = HPColor(_playerHPDisplay);
            _enemyHPFill.color = HPColor(_enemyHPDisplay);

            // Flash white on damage (overrides color briefly)
            if (_playerFlash > 0f)
            {
                _playerFlash -= Time.deltaTime * 4f;
                _playerHPFill.color = Color.Lerp(_playerHPFill.color, Color.white, _playerFlash);
            }

            if (_enemyFlash > 0f)
            {
                _enemyFlash -= Time.deltaTime * 4f;
                _enemyHPFill.color = Color.Lerp(_enemyHPFill.color, Color.white, _enemyFlash);
            }

            // Combo pop decay
            if (_comboPopScale > 1f)
            {
                _comboPopScale = Mathf.Lerp(_comboPopScale, 1f, Time.deltaTime * 10f);
                _comboRect.localScale = Vector3.one * _comboPopScale;
            }

            // Score roll
            if (_displayScore < _currentScore)
            {
                _displayScore = (int)Mathf.Lerp(_displayScore, _currentScore, Time.deltaTime * 12f);
                if (_currentScore - _displayScore < 2) _displayScore = _currentScore;
                _scoreText.text = _displayScore.ToString();
            }
        }

        // =================================================================
        // EVENT HANDLERS
        // =================================================================

        private void OnPlayerDamaged(int amount, int current) => _playerFlash = 1f;
        private void OnPlayerHealed(int amount, int current) { }
        private void OnPlayerHPChanged(int current, int max) => UpdatePlayerHP();

        private void OnEnemyDamaged(int amount, int current) => _enemyFlash = 1f;
        private void OnEnemyHPChanged(int current, int max) => UpdateEnemyHP();

        private void OnComboChanged(int combo, float mult) => SetCombo(combo, mult);

        private void OnComboReset(int lost)
        {
            SetCombo(0, 1f);
            _comboText.color = new Color(1f, 0.3f, 0.3f);
        }

        private void OnComboMilestone(int milestone)
        {
            _comboPopScale = 1.5f;
            _comboText.color = new Color(1f, 0.85f, 0f);
        }

        private void OnDamageDealt(DamageResult result)
        {
            if (!result.IsPlayerDamage)
                _currentScore += result.Amount;
        }

        // =================================================================
        // UI UPDATES
        // =================================================================

        private void UpdatePlayerHP()
        {
            if (_playerHealth == null) return;
            _playerHPTarget = _playerHealth.HPPercent;
            _playerHPText.text = $"{_playerHealth.CurrentHP}/{_playerHealth.MaxHP}";
        }

        private void UpdateEnemyHP()
        {
            if (_enemyHealth == null || _enemyHealth.Health == null) return;
            _enemyHPTarget = _enemyHealth.HPPercent;
        }

        private void SetCombo(int combo, float mult)
        {
            _comboText.text = combo > 0 ? combo.ToString() : "";
            _multiplierText.text = combo > 0 ? $"x{mult:F1}" : "";
            _comboText.color = Color.white;

            if (combo > 0)
                _comboPopScale = 1.2f;
        }

        private void SetScore(int score)
        {
            _currentScore = score;
            _displayScore = score;
            _scoreText.text = score.ToString();
        }

        // =================================================================
        // RESULT OVERLAY — called by BattleManager
        // =================================================================

        /// <summary>Show victory or defeat text.</summary>
        public void ShowResult(bool victory)
        {
            _resultBG.gameObject.SetActive(true);

            _resultText.text = victory ? "VICTORY" : "DEFEATED";
            _resultText.color = victory
                ? new Color(0.3f, 1f, 0.3f)
                : new Color(1f, 0.3f, 0.3f);

            StartCoroutine(AnimateResult());
        }

        private IEnumerator AnimateResult()
        {
            RectTransform rt = _resultText.GetComponent<RectTransform>();
            float t = 0f;

            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                float s = Mathf.Lerp(2f, 1f, t / 0.3f);
                rt.localScale = Vector3.one * s;

                Color c = _resultBG.color;
                c.a = Mathf.Lerp(0f, 0.6f, t / 0.3f);
                _resultBG.color = c;

                yield return null;
            }

            rt.localScale = Vector3.one;
        }

        // =================================================================
        // HELPER
        // =================================================================

        private static Color HPColor(float pct)
        {
            if (pct > 0.5f) return Color.Lerp(Color.yellow, Color.green, (pct - 0.5f) * 2f);
            if (pct > 0.25f) return Color.Lerp(Color.red, Color.yellow, (pct - 0.25f) * 4f);
            return Color.red;
        }

        // =================================================================
        // UI CREATION — all code, no prefabs needed
        // =================================================================

        private void CreateUI()
        {
            // Canvas
            GameObject canvasGO = new GameObject("BattleCanvas");
            canvasGO.transform.SetParent(transform);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(384, 216);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();

            // --- ENEMY HP BAR (top center) ---
            _enemyNameText = CreateText(canvasRT, "EnemyName", "Enemy",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -2), new Vector2(120, 10), 7, TextAnchor.MiddleCenter);

            // BOSS label — red, larger, above enemy name. Hidden for non-bosses.
            _bossLabel = CreateText(canvasRT, "BossLabel", "",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -2), new Vector2(50, 10), 7, TextAnchor.MiddleRight);
            _bossLabel.fontStyle = FontStyle.Bold;
            _bossLabel.color = new Color(1f, 0.2f, 0.2f);
            // Position left of enemy name
            _bossLabel.rectTransform.anchoredPosition = new Vector2(-65, -2);
            _bossLabel.gameObject.SetActive(false);

            CreateHPBar(canvasRT, "EnemyHP",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -12), new Vector2(120, 6),
                Color.red, out _enemyHPFill, out _enemyHPGhost);

            // --- PLAYER HP BAR (bottom-left) ---
            _playerHPText = CreateText(canvasRT, "PlayerHPText", "100/100",
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(4, 16), new Vector2(60, 10), 6, TextAnchor.MiddleLeft);

            CreateHPBar(canvasRT, "PlayerHP",
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(4, 6), new Vector2(80, 5),
                Color.green, out _playerHPFill, out _playerHPGhost);

            // --- COMBO (right side, middle) ---
            GameObject comboGroup = CreatePanel(canvasRT, "ComboGroup",
                new Vector2(1, 0.4f), new Vector2(1, 0.4f), new Vector2(1, 0.4f),
                new Vector2(-8, 0), new Vector2(40, 24), new Color(0, 0, 0, 0));
            _comboRect = comboGroup.GetComponent<RectTransform>();

            _comboText = CreateText(_comboRect, "ComboNum", "",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -1), new Vector2(40, 14), 10, TextAnchor.MiddleCenter);
            _comboText.fontStyle = FontStyle.Bold;

            _multiplierText = CreateText(_comboRect, "MultText", "",
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 1), new Vector2(40, 10), 6, TextAnchor.MiddleCenter);
            _multiplierText.color = new Color(1f, 0.85f, 0f);

            // --- SCORE (top-right) ---
            _scoreText = CreateText(canvasRT, "Score", "0",
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-4, -3), new Vector2(60, 10), 7, TextAnchor.MiddleRight);

            // --- RESULT OVERLAY (center, hidden by default) ---
            GameObject resultBGObj = CreatePanel(canvasRT, "ResultBG",
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.6f));
            _resultBG = resultBGObj.GetComponent<Image>();
            RectTransform resultBGRT = resultBGObj.GetComponent<RectTransform>();
            resultBGRT.offsetMin = Vector2.zero;
            resultBGRT.offsetMax = Vector2.zero;

            _resultText = CreateText(resultBGRT, "ResultText", "",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(200, 30), 14, TextAnchor.MiddleCenter);
            _resultText.fontStyle = FontStyle.Bold;

            resultBGObj.SetActive(false);
        }

        private void CreateHPBar(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 pos, Vector2 size,
            Color fillColor, out Image fill, out Image ghost)
        {
            // Background
            GameObject bgObj = CreatePanel(parent, name + "_BG",
                anchorMin, anchorMax, pivot, pos, size, new Color(0.15f, 0.15f, 0.15f, 0.8f));
            RectTransform bgRT = bgObj.GetComponent<RectTransform>();

            // Ghost bar (white, trails behind)
            GameObject ghostObj = CreatePanel(bgRT, name + "_Ghost",
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f),
                Vector2.zero, Vector2.zero, new Color(1, 1, 1, 0.3f));
            ghost = ghostObj.GetComponent<Image>();
            ghost.type = Image.Type.Filled;
            ghost.fillMethod = Image.FillMethod.Horizontal;
            ghost.fillAmount = 1f;
            RectTransform ghostRT = ghostObj.GetComponent<RectTransform>();
            ghostRT.offsetMin = Vector2.one;
            ghostRT.offsetMax = -Vector2.one;

            // Fill bar
            GameObject fillObj = CreatePanel(bgRT, name + "_Fill",
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f),
                Vector2.zero, Vector2.zero, fillColor);
            fill = fillObj.GetComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 1f;
            RectTransform fillRT = fillObj.GetComponent<RectTransform>();
            fillRT.offsetMin = Vector2.one;
            fillRT.offsetMax = -Vector2.one;
        }

        private static GameObject CreatePanel(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 pos, Vector2 size, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = obj.GetComponent<Image>();
            img.color = color;

            return obj;
        }

        private static Text CreateText(RectTransform parent, string name, string text,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 pos, Vector2 size, int fontSize, TextAnchor alignment)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Text t = obj.GetComponent<Text>();
            t.text = text;
            t.fontSize = fontSize;
            t.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            t.alignment = alignment;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            return t;
        }
    }
}