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
    /// Chart modes (auto-detected, priority order):
    ///   1. Legacy: _useLegacyChart toggle -> single JSON chart
    ///   2. Beat map: EnemyData has SongBeatMap -> hand-crafted markers
    ///   3. Hybrid: AudioClip + PatternLibrary -> human patterns placed by algorithm
    ///   4. Algorithmic: AudioClip only -> pure marker-driven fallback
    /// 
    /// The hybrid path (3) is the primary path for the roguelike:
    /// audio analysis detects section energy, hand-crafted patterns are
    /// selected by density match, and the run seed controls which pattern
    /// is picked. Same song, different chart every seed.
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("Run State")]
        [SerializeField] private RunState _runState;

        [Header("Chart Mode")]
        [Tooltip("True = force legacy JSON chart mode. False = auto-detect from EnemyData.")]
        [SerializeField] private bool _useLegacyChart = false;

        [Header("Legacy Defaults (used if _useLegacyChart is true)")]
        [SerializeField] private EnemyData _defaultEnemy;
        [SerializeField] private TextAsset _defaultChart;

        [Header("Chart System")]
        [Tooltip("Fallback pattern library if enemy has none assigned.")]
        [SerializeField] private PatternLibrary _defaultLibrary;

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
        private LoadedChart _currentChart;
        private BattleChart _battleChart;
        private float _phaseTimer;
        private bool _battleEnded;
        private bool _isElite;
        private bool _useBeatMap;

        public static BattleStats LastBattleStats { get; private set; }

        public BattlePhase CurrentPhase => _fsm != null && _fsm.IsRunning ? _fsm.CurrentStateKey : BattlePhase.Intro;
        public bool IsPaused => _conductor != null && _conductor.IsPaused;
        public EnemyData CurrentEnemy => _currentEnemy;
        public bool IsElite => _isElite;

        public event System.Action<bool> OnBattleCompleted;

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Awake()
        {
            _conductor = Conductor.Instance;
            _playerHealth = PlayerHealth.Instance;

            _currentEnemy = (_runState?.SelectedNode?.EnemyData) ?? _defaultEnemy;
            _isElite = _runState?.SelectedNode?.Type == NodeType.Elite;

            if (_currentEnemy == null)
            {
                GameLog.Error("[BattleManager] No enemy data! Assign a default in Inspector.");
                return;
            }

            // Chart mode priority:
            // 1. Legacy toggle -> JSON chart
            // 2. Enemy has SongBeatMap -> hand-crafted marker-driven
            // 3. AudioClip available -> hybrid (human patterns + audio analysis)
            // 4. No audio -> error
            if (_useLegacyChart)
            {
                LoadLegacyChart();
            }
            else if (_currentEnemy.songBeatMap != null)
            {
                AssembleFromBeatMap();
            }
            else if (HasAudioClip())
            {
                AssembleFromAudioAnalysis();
            }
            else
            {
                GameLog.Error("[BattleManager] No chart source available! " +
                              "Enemy needs a SongBeatMap, or the Conductor needs an AudioClip.");
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
        // CHART LOADING - LEGACY (JSON)
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
                GameLog.Error("[BattleManager] Failed to load chart.");
        }

        // =================================================================
        // CHART LOADING - MARKER-DRIVEN (hand-crafted SongBeatMap)
        // =================================================================

        private void AssembleFromBeatMap()
        {
            SongBeatMap beatMap = _currentEnemy.songBeatMap;
            _useBeatMap = true;

            ISeededRandom chartRng = GetChartRng();

            float difficulty = _currentEnemy.markerDifficulty;
            if (_isElite && _eliteConfig != null)
                difficulty = Mathf.Clamp01(difficulty + _eliteConfig.difficultyBoost * 0.1f);

            float bpmModifier = _currentEnemy.bpmModifier;
            if (_isElite && _eliteConfig != null)
                bpmModifier = _eliteConfig.ScaleBPMModifier(bpmModifier);

            float effectiveBPM = beatMap.bpm * bpmModifier;

            _battleChart = MarkerDrivenAssembler.Assemble(beatMap, chartRng, difficulty, effectiveBPM);

            if (_battleChart == null)
                GameLog.Error("[BattleManager] MarkerDrivenAssembler returned null.");
        }

        // =================================================================
        // CHART LOADING - HYBRID (audio analysis + human patterns)
        // =================================================================

        private bool HasAudioClip()
        {
            if (_conductor == null) return false;
            AudioSource source = _conductor.GetComponent<AudioSource>();
            return source != null && source.clip != null;
        }

        /// <summary>
        /// Hybrid path: analyze the AudioClip, then pick hand-crafted patterns
        /// from the PatternLibrary based on what the analysis detected.
        /// If no PatternLibrary exists, falls back to pure algorithmic.
        /// </summary>
        private void AssembleFromAudioAnalysis()
        {
            AudioSource source = _conductor.GetComponent<AudioSource>();
            AudioClip clip = source.clip;

            ISeededRandom chartRng = GetChartRng();

            // Calculate effective BPM
            float baseBPM = (_defaultChart != null)
                ? ChartLoader.Load(_defaultChart)?.BPM ?? 135f
                : 135f;

            float bpmModifier = _currentEnemy.bpmModifier;
            if (_isElite && _eliteConfig != null)
                bpmModifier = _eliteConfig.ScaleBPMModifier(bpmModifier);

            float effectiveBPM = baseBPM * bpmModifier;

            // Calculate difficulty
            float difficulty = _currentEnemy.markerDifficulty;
            if (_isElite && _eliteConfig != null)
                difficulty = Mathf.Clamp01(difficulty + _eliteConfig.difficultyBoost * 0.1f);

            float sensitivity = Mathf.Lerp(0.3f, 0.8f, difficulty);

            // Run onset detection on the audio
            var analysis = RuntimeBeatAnalyzer.Analyze(clip, effectiveBPM, sensitivity);

            if (!analysis.Success || analysis.Markers.Count == 0)
            {
                GameLog.Error("[BattleManager] Runtime analysis produced no markers. " +
                              "Cannot generate chart for this audio.");
                return;
            }

            // HYBRID PATH: if we have a pattern library, use human-crafted patterns
            // placed at algorithmically-detected positions
            PatternLibrary library = _currentEnemy.patternLibrary ?? _defaultLibrary;

            if (library != null && library.patterns.Count > 0)
            {
                _battleChart = HybridAssembler.Assemble(
                    analysis, library, chartRng, difficulty,
                    effectiveBPM, analysis.Markers);

                if (_battleChart != null)
                {
                    _useBeatMap = true;
                    return;
                }

                GameLog.Warn("[BattleManager] Hybrid assembly failed. Falling back to marker-driven.");
            }

            // FALLBACK: pure algorithmic (no pattern library available)
            _battleChart = MarkerDrivenAssembler.Assemble(
                analysis.Markers,
                analysis.Sections,
                leadInBeats: 4f,
                tailBeats: 2f,
                totalBeats: analysis.TotalBeats,
                chartRng,
                difficulty,
                effectiveBPM,
                sourceName: clip.name);

            if (_battleChart == null)
            {
                GameLog.Error("[BattleManager] All chart assembly paths failed.");
                return;
            }

            _useBeatMap = true;
        }

        // =================================================================
        // CHART HELPERS
        // =================================================================

        private ISeededRandom GetChartRng()
        {
            if (_runState?.RunSeed != null)
                return _runState.RunSeed.GetRandom(RandomDomain.Charts, _runState.SelectedNode?.Id ?? 0);

            GameLog.Warn("[BattleManager] No RunSeed available, using fallback seed 42.");
            return new SeededRandom(42);
        }

        // =================================================================
        // INITIALIZATION
        // =================================================================

        private void InitializeBattle()
        {
            int enemyHP = _currentEnemy.maxHP;
            if (_isElite && _eliteConfig != null)
                enemyHP = _eliteConfig.ScaleHP(enemyHP);

            _enemyHealth.InitForBattle(enemyHP);

            _enemyHealth.Health.OnDeath += OnEnemyDied;
            _playerHealth.Health.OnDeath += OnPlayerDied;

            if (_enemyRenderer != null && _currentEnemy.sprite != null)
                _enemyRenderer.sprite = _currentEnemy.sprite;

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
            string chartMode = _useLegacyChart ? "legacy"
                : _useBeatMap && _currentEnemy.songBeatMap != null ? "beat-map"
                : _useBeatMap ? "hybrid"
                : "unknown";

            if (_useLegacyChart)
            {
                GameLog.Info($"[BattleManager] Battle initialized ({chartMode}){eliteTag}: {_currentEnemy.enemyName} " +
                          $"({enemyHP} HP) at {_currentChart.BPM * _currentEnemy.bpmModifier} BPM");
            }
            else
            {
                GameLog.Info($"[BattleManager] Battle initialized ({chartMode}){eliteTag}: {_currentEnemy.enemyName} " +
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
                    GameLog.Info($"[BattleManager] INTRO{eliteTag} - {_currentEnemy.enemyName} appears!");
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
                        offset = _useBeatMap && _currentEnemy.songBeatMap != null
                            ? _currentEnemy.songBeatMap.audioOffsetSeconds
                            : 0f;
                    }

                    _conductor.OnSongFinished += OnSongEnded;
                    _conductor.Play(effectiveBPM, offset);
                    GameLog.Info("[BattleManager] PLAYING - song started!");
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
                GameLog.Info("[BattleManager] Song ended - enemy survived!");
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

            GameLog.Info("[BattleManager] Battle complete - ready for scene transition.");

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