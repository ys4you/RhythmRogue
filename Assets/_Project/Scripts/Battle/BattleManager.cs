using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;
using RhythmRogue.Map;
using RhythmRogue.Util.FSM;
using RhythmRogue.Util;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Orchestrates the full battle lifecycle.
    /// 
    /// Uses StateMachine&lt;BattlePhase&gt; from Util to manage phases:
    ///   Intro → Playing → Won or Lost
    /// 
    /// On Awake, reads RunState.SelectedNode for enemy/chart data.
    /// Falls back to serialized defaults for testing without scene transitions.
    /// 
    /// Supports two chart modes:
    ///   - Legacy: single JSON chart loaded via ChartLoader (original system)
    ///   - Assembled: dual highway charts built from PatternLibrary via ChartAssembler
    /// 
    /// Elite scaling: when the selected node is NodeType.Elite, applies
    /// EliteConfig modifiers to HP, BPM, and pattern difficulty at init time.
    /// The base EnemyData is never mutated.
    /// 
    /// Relic modifiers: at battle start, aggregates all active relics into
    /// a RelicModifiers struct and distributes numeric bonuses to each
    /// gameplay system. No system knows about individual relics.
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("Run State")]
        [SerializeField] private RunState _runState;

        [Header("Chart Mode")]
        [Tooltip("True = load from JSON TextAsset (original system). False = assemble from patterns (dual highway).")]
        [SerializeField] private bool _useLegacyChart = false;

        [Header("Legacy Defaults (used if _useLegacyChart is true)")]
        [SerializeField] private EnemyData _defaultEnemy;
        [SerializeField] private TextAsset _defaultChart;

        [Header("Chart System (used if _useLegacyChart is false)")]
        [Tooltip("Fallback pattern library if enemy has none assigned.")]
        [SerializeField] private PatternLibrary _defaultLibrary;

        [Tooltip("Fallback chart template if enemy has none assigned.")]
        [SerializeField] private ChartTemplate _defaultTemplate;

        [Tooltip("The enemy's auto-playing highway (left side).")]
        [SerializeField] private EnemyHighway _enemyHighway;

        [Header("Elite Scaling")]
        [Tooltip("Elite encounter config. Applied when node type is Elite.")]
        [SerializeField] private EliteConfig _eliteConfig;

        [Header("Relic Effects")]
        [SerializeField] private RelicEffectHandler _relicEffectHandler;

        [Header("Intro")]
        [Tooltip("Seconds of countdown before the song starts.")]
        [SerializeField] private float _introDelay = 2f;

        [Header("End")]
        [Tooltip("Seconds to pause on win/lose screen before transitioning.")]
        [SerializeField] private float _endDelay = 2f;

        [Header("System References")]
        [SerializeField] private NoteHighway _highway;
        [SerializeField] private InputHandler _inputHandler;
        [SerializeField] private NoteMatcher _noteMatcher;
        [SerializeField] private HoldTracker _holdTracker;
        [SerializeField] private JudgmentSystem _judgmentSystem;
        [SerializeField] private ComboSystem _comboSystem;
        [SerializeField] private AccuracyTracker _accuracyTracker;
        [SerializeField] private DamagePipeline _damagePipeline;
        [SerializeField] private EnemyHealth _enemyHealth;

        [Header("Enemy Display")]
        [SerializeField] private SpriteRenderer _enemyRenderer;

        [Header("UI")]
        [SerializeField] private BattleUI _battleUI;

        // =================================================================
        // STATE
        // =================================================================

        private StateMachine<BattlePhase> _fsm;
        private Conductor _conductor;
        private PlayerHealth _playerHealth;

        private EnemyData _currentEnemy;
        private LoadedChart _currentChart;       // Legacy mode
        private BattleChart _battleChart;         // Assembled mode
        private float _phaseTimer;
        private bool _battleEnded;
        private bool _isElite;

        /// <summary>Results from the last battle. Read by summary screen.</summary>
        public static BattleStats LastBattleStats { get; private set; }

        /// <summary>Current battle phase for debug/UI.</summary>
        public BattlePhase CurrentPhase => _fsm != null && _fsm.IsRunning ? _fsm.CurrentStateKey : BattlePhase.Intro;

        /// <summary>Whether the battle is paused.</summary>
        public bool IsPaused => _conductor != null && _conductor.IsPaused;

        /// <summary>The current enemy being fought. Read by BattleUI.</summary>
        public EnemyData CurrentEnemy => _currentEnemy;

        /// <summary>Whether this is an elite encounter. Read by BattleUI.</summary>
        public bool IsElite => _isElite;

        /// <summary>Fired when the battle is fully complete (after result overlay).</summary>
        public event System.Action<bool> OnBattleCompleted;

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Awake()
        {
            _conductor = Conductor.Instance;
            _playerHealth = PlayerHealth.Instance;

            // Resolve enemy data
            _currentEnemy = (_runState?.SelectedNode?.EnemyData) ?? _defaultEnemy;
            _isElite = _runState?.SelectedNode?.Type == NodeType.Elite;

            if (_currentEnemy == null)
            {
                GameLog.Error("[BattleManager] No enemy data! Assign a default in Inspector.");
                return;
            }

            if (_useLegacyChart)
            {
                LoadLegacyChart();
            }
            else
            {
                AssembleChart();
            }

            SetupFSM();
        }

        private void Start()
        {
            InitializeBattle();
            _fsm.Start(BattlePhase.Intro);
        }

        private void OnEnable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.OnLanePressed += OnReceptorPress;
                _inputHandler.OnLaneReleased += OnReceptorRelease;
            }
        }

        private void OnDisable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.OnLanePressed -= OnReceptorPress;
                _inputHandler.OnLaneReleased -= OnReceptorRelease;
            }
        }

        private void Update()
        {
            _fsm.Update();
            HandlePauseInput();
        }

        private void OnReceptorPress(int lane) => _highway.SetReceptorPressed(lane, true);
        private void OnReceptorRelease(int lane) => _highway.SetReceptorPressed(lane, false);

        // =================================================================
        // CHART LOADING
        // =================================================================

        private void LoadLegacyChart()
        {
            TextAsset chartAsset = _runState?.SelectedChart ?? _defaultChart;

            if (chartAsset == null)
            {
                GameLog.Error("[BattleManager] No chart data! Assign a default in Inspector.");
                return;
            }

            _currentChart = ChartLoader.Load(chartAsset);

            if (_currentChart == null)
            {
                GameLog.Error("[BattleManager] Failed to load chart.");
                return;
            }
        }

        private void AssembleChart()
        {
            ChartTemplate template = _currentEnemy.chartTemplate ?? _defaultTemplate;
            PatternLibrary library = _currentEnemy.patternLibrary ?? _defaultLibrary;

            if (template == null)
            {
                GameLog.Error("[BattleManager] No chart template! Assign one on EnemyData or set a default.");
                return;
            }

            if (library == null)
            {
                GameLog.Error("[BattleManager] No pattern library! Assign one on EnemyData or set a default.");
                return;
            }

            ISeededRandom chartRng;
            if (_runState?.RunSeed != null)
            {
                chartRng = _runState.RunSeed.GetRandom(RandomDomain.Charts, _runState.SelectedNode?.Id ?? 0);
            }
            else
            {
                chartRng = new SeededRandom(42);
                GameLog.Warn("[BattleManager] No RunSeed available, using fallback seed 42.");
            }

            float baseBPM = (_defaultChart != null)
                ? ChartLoader.Load(_defaultChart)?.BPM ?? 135f
                : 135f;

            float bpmModifier = _currentEnemy.bpmModifier;
            if (_isElite && _eliteConfig != null)
                bpmModifier = _eliteConfig.ScaleBPMModifier(bpmModifier);

            float effectiveBPM = baseBPM * bpmModifier;

            float targetDurationBeats = CalculateTargetBeats(effectiveBPM);

            _battleChart = ChartAssembler.Assemble(
                template, library, chartRng, effectiveBPM, targetDurationBeats);

            if (_battleChart == null)
            {
                GameLog.Error("[BattleManager] ChartAssembler returned null.");
                return;
            }
        }

        private float CalculateTargetBeats(float bpm)
        {
            if (_conductor == null) return 0f;

            AudioSource audioSource = _conductor.GetComponent<AudioSource>();
            if (audioSource == null || audioSource.clip == null) return 0f;

            float clipLengthSeconds = audioSource.clip.length;
            float secPerBeat = 60f / bpm;
            float targetBeats = clipLengthSeconds / secPerBeat;

            GameLog.Info($"[BattleManager] Audio clip: {clipLengthSeconds:F1}s → {targetBeats:F0} beats at {bpm} BPM");

            return targetBeats;
        }

        // =================================================================
        // INITIALIZATION
        // =================================================================

        private void InitializeBattle()
        {
            // Apply elite HP scaling
            int enemyHP = _currentEnemy.maxHP;
            if (_isElite && _eliteConfig != null)
                enemyHP = _eliteConfig.ScaleHP(enemyHP);

            _enemyHealth.InitForBattle(enemyHP);

            _enemyHealth.Health.OnDeath += OnEnemyDied;
            _playerHealth.Health.OnDeath += OnPlayerDied;

            if (_enemyRenderer != null && _currentEnemy.sprite != null)
                _enemyRenderer.sprite = _currentEnemy.sprite;

            // Feed notes to highways based on chart mode
            if (_useLegacyChart)
            {
                _highway.LoadChart(_currentChart);
            }
            else
            {
                _highway.LoadNotes(_battleChart.AllPlayerNotes);

                if (_enemyHighway != null)
                    _enemyHighway.LoadNotes(_battleChart.AllEnemyNotes);
            }

            _damagePipeline.SetEnemyHealth(_enemyHealth);

            _comboSystem.ResetAll();
            _accuracyTracker.Reset();

            // --- Relic modifiers ---
            RelicModifiers mods = RelicEffectAggregator.Aggregate(_runState?.ActiveRelics);

            _judgmentSystem.ApplyRelicModifiers(mods.BonusPerfectWindowMs);
            _comboSystem.ApplyRelicModifiers(mods.ComboRateBoost, mods.ComboCapBoost);
            _damagePipeline.ApplyRelicModifiers(mods.BonusPerfectDamage, mods.MissDamageReduction);

            if (_relicEffectHandler != null)
                _relicEffectHandler.Initialize(mods);

            if (mods.HasAnyEffect)
                GameLog.Info($"[BattleManager] Relic modifiers active: {mods}");

            // --- Logging ---
            string eliteTag = _isElite ? " [ELITE]" : "";

            if (_useLegacyChart)
            {
                GameLog.Info($"[BattleManager] Battle initialized (legacy){eliteTag}: {_currentEnemy.enemyName} " +
                          $"({enemyHP} HP) at {_currentChart.BPM * _currentEnemy.bpmModifier} BPM");
            }
            else
            {
                GameLog.Info($"[BattleManager] Battle initialized (assembled){eliteTag}: {_currentEnemy.enemyName} " +
                          $"({enemyHP} HP) at {_battleChart.BPM} BPM, " +
                          $"{_battleChart.PlayerNoteCount} player notes, " +
                          $"{_battleChart.EnemyNoteCount} enemy notes");
            }
        }

        // =================================================================
        // FSM SETUP
        // =================================================================

        private void SetupFSM()
        {
            _fsm = new StateMachine<BattlePhase>();

            _fsm.AddState(new LambdaState<BattlePhase>(
                BattlePhase.Intro,
                enter: _ =>
                {
                    _phaseTimer = _introDelay;
                    _battleEnded = false;
                    string eliteTag = _isElite ? " [ELITE]" : "";
                    GameLog.Info($"[BattleManager] INTRO{eliteTag} — {_currentEnemy.enemyName} appears!");
                },
                update: () =>
                {
                    _phaseTimer -= Time.deltaTime;
                    if (_phaseTimer <= 0f)
                        _fsm.TransitionTo(BattlePhase.Playing);
                }
            ));

            _fsm.AddState(new LambdaState<BattlePhase>(
                BattlePhase.Playing,
                enter: _ =>
                {
                    float effectiveBPM;
                    float offset;

                    if (_useLegacyChart)
                    {
                        float bpmMod = _currentEnemy.bpmModifier;
                        if (_isElite && _eliteConfig != null)
                            bpmMod = _eliteConfig.ScaleBPMModifier(bpmMod);

                        effectiveBPM = _currentChart.BPM * bpmMod;
                        offset = _currentChart.Offset;
                    }
                    else
                    {
                        effectiveBPM = _battleChart.BPM;
                        offset = 0f;
                    }

                    _conductor.OnSongFinished += OnSongEnded;
                    _conductor.Play(effectiveBPM, offset);
                    GameLog.Info("[BattleManager] PLAYING — song started!");
                },
                exit: _ =>
                {
                    _conductor.OnSongFinished -= OnSongEnded;
                    if (_conductor.IsPlaying)
                        _conductor.Stop();
                    _highway.ClearAllNotes();
                    _holdTracker.ClearAll();

                    if (!_useLegacyChart && _enemyHighway != null)
                        _enemyHighway.Clear();
                }
            ));

            _fsm.AddState(new LambdaState<BattlePhase>(
                BattlePhase.Won,
                enter: _ =>
                {
                    _phaseTimer = _endDelay;
                    CollectStats(true);
                    _battleUI?.ShowResult(true);
                    GameLog.Info("<color=green>[BattleManager] VICTORY!</color>");
                },
                update: () =>
                {
                    _phaseTimer -= Time.deltaTime;
                    if (_phaseTimer <= 0f)
                        OnBattleComplete();
                }
            ));

            _fsm.AddState(new LambdaState<BattlePhase>(
                BattlePhase.Lost,
                enter: _ =>
                {
                    _phaseTimer = _endDelay;
                    CollectStats(false);
                    _battleUI?.ShowResult(false);
                    GameLog.Info("<color=red>[BattleManager] DEFEATED!</color>");
                },
                update: () =>
                {
                    _phaseTimer -= Time.deltaTime;
                    if (_phaseTimer <= 0f)
                        OnBattleComplete();
                }
            ));
        }

        // =================================================================
        // WIN/LOSE CONDITIONS
        // =================================================================

        private void OnEnemyDied()
        {
            if (_battleEnded) return;
            EndBattle(true);
        }

        private void OnPlayerDied()
        {
            if (_battleEnded) return;
            EndBattle(false);
        }

        private void OnSongEnded()
        {
            if (_battleEnded) return;

            if (_enemyHealth.IsAlive)
            {
                GameLog.Info("[BattleManager] Song ended — enemy survived!");
                EndBattle(false);
            }
        }

        private void EndBattle(bool victory)
        {
            _battleEnded = true;
            _fsm.TransitionTo(victory ? BattlePhase.Won : BattlePhase.Lost);
        }

        // =================================================================
        // STATS COLLECTION
        // =================================================================

        private void CollectStats(bool victory)
        {
            LastBattleStats = new BattleStats
            {
                Victory = victory,
                PlayerHPRemaining = _playerHealth.CurrentHP,
                EnemyHPRemaining = _enemyHealth.CurrentHP,
                MaxCombo = _comboSystem.MaxCombo,
                ComboResets = _comboSystem.TotalResets,
                Accuracy = _accuracyTracker.Accuracy,
                TotalNotes = _accuracyTracker.TotalNotes,
                NotesHit = _accuracyTracker.PerfectCount +
                           _accuracyTracker.GoodCount +
                           _accuracyTracker.BadCount,
                EnemyName = _currentEnemy.enemyName,
                WasBoss = _currentEnemy.IsBoss
            };
        }

        // =================================================================
        // BATTLE COMPLETE
        // =================================================================

        private void OnBattleComplete()
        {
            if (_enemyHealth.Health != null)
                _enemyHealth.Health.OnDeath -= OnEnemyDied;
            _playerHealth.Health.OnDeath -= OnPlayerDied;

            GameLog.Info("[BattleManager] Battle complete — ready for scene transition.");

            OnBattleCompleted?.Invoke(LastBattleStats.Victory);
        }

        // =================================================================
        // PAUSE
        // =================================================================

        private void HandlePauseInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_fsm.CurrentStateKey != BattlePhase.Playing)
                    return;

                if (_conductor.IsPaused)
                    ResumeBattle();
                else
                    PauseBattle();
            }
        }

        private void PauseBattle()
        {
            _conductor.Pause();
            GameLog.Info("[BattleManager] PAUSED");
        }

        private void ResumeBattle()
        {
            _conductor.Resume();
            GameLog.Info("[BattleManager] RESUMED");
        }

        // =================================================================
        // CLEANUP
        // =================================================================

        private void OnDestroy()
        {
            if (_enemyHealth != null && _enemyHealth.Health != null)
                _enemyHealth.Health.OnDeath -= OnEnemyDied;

            if (_playerHealth != null && _playerHealth.Health != null)
                _playerHealth.Health.OnDeath -= OnPlayerDied;
        }
    }
}