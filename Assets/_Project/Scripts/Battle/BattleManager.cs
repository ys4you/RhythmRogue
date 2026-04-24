using System.Collections.Generic;
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
    /// Chart generation (priority order):
    ///   1. Legacy: _useLegacyChart toggle -> single JSON chart
    ///   2. Beat map: EnemyData.songBeatMap -> ShapeAssembler
    ///   3. Auto-detect: AudioClip -> RuntimeBeatAnalyzer -> ShapeAssembler
    /// 
    /// Both paths 2 and 3 use the same ShapeAssembler pipeline:
    ///   timing markers (from beat map or audio analysis)
    ///   + shape library (lane movement patterns)
    ///   + seeded RNG (different chart per seed)
    ///   + difficulty (controls note density and shape complexity)
    ///   = BattleChart
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
        [Tooltip("Fallback shape library if enemy has none assigned.")]
        [SerializeField] private ShapeLibrary _defaultShapeLibrary;

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
        private string _chartMode;

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

            if (_useLegacyChart)
            {
                LoadLegacyChart();
                _chartMode = "legacy";
            }
            else if (_currentEnemy.songBeatMap != null)
            {
                AssembleFromBeatMap();
                _chartMode = "beat-map";
            }
            else if (HasAudioClip())
            {
                AssembleFromAudioAnalysis();
                _chartMode = "auto-detect";
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
        // CHART LOADING - BEAT MAP (curated songs)
        // =================================================================

        private void AssembleFromBeatMap()
        {
            SongBeatMap beatMap = _currentEnemy.songBeatMap;

            ISeededRandom chartRng = GetChartRng();
            ShapeLibrary library = GetShapeLibrary();

            float difficulty = GetEffectiveDifficulty();
            float effectiveBPM = beatMap.bpm * GetEffectiveBPMModifier();

            var markers = new List<BeatMarker>(beatMap.markers);
            var sections = beatMap.sections != null
                ? new List<SongSection>(beatMap.sections)
                : null;

            float totalBeats = markers.Count > 0
                ? markers[markers.Count - 1].beat + 4f
                : 0f;

            _battleChart = ShapeAssembler.Assemble(
                markers, sections, library, chartRng,
                difficulty, effectiveBPM, totalBeats);

            if (_battleChart == null)
                GameLog.Error("[BattleManager] ShapeAssembler returned null (beat map path).");
        }

        // =================================================================
        // CHART LOADING - AUTO-DETECT (imported songs)
        // =================================================================

        private bool HasAudioClip()
        {
            if (_conductor == null) return false;
            AudioSource source = _conductor.GetComponent<AudioSource>();
            return source != null && source.clip != null;
        }

        private void AssembleFromAudioAnalysis()
        {
            AudioSource source = _conductor.GetComponent<AudioSource>();
            AudioClip clip = source.clip;

            ISeededRandom chartRng = GetChartRng();
            ShapeLibrary library = GetShapeLibrary();

            float difficulty = GetEffectiveDifficulty();
            float sensitivity = Mathf.Lerp(0.3f, 0.8f, difficulty);

            // Derive BPM from legacy chart if available, otherwise default
            float baseBPM = (_defaultChart != null)
                ? ChartLoader.Load(_defaultChart)?.BPM ?? 135f
                : 135f;

            float effectiveBPM = baseBPM * GetEffectiveBPMModifier();

            // Run onset detection
            var analysis = RuntimeBeatAnalyzer.Analyze(clip, effectiveBPM, sensitivity);

            if (!analysis.Success || analysis.Markers.Count == 0)
            {
                GameLog.Error("[BattleManager] Runtime analysis produced no markers.");
                return;
            }

            _battleChart = ShapeAssembler.Assemble(
                analysis.Markers, analysis.Sections, library, chartRng,
                difficulty, effectiveBPM, analysis.TotalBeats);

            if (_battleChart == null)
                GameLog.Error("[BattleManager] ShapeAssembler returned null (auto-detect path).");
        }

        // =================================================================
        // CHART HELPERS
        // =================================================================

        private ShapeLibrary GetShapeLibrary()
        {
            ShapeLibrary library = _currentEnemy.shapeLibrary ?? _defaultShapeLibrary;

            if (library == null || library.shapes.Count == 0)
            {
                GameLog.Error("[BattleManager] No ShapeLibrary available! " +
                              "Assign one on EnemyData or set a default in BattleManager.");
            }

            return library;
        }

        private float GetEffectiveDifficulty()
        {
            float difficulty = _currentEnemy.markerDifficulty;
            if (_isElite && _eliteConfig != null)
                difficulty = Mathf.Clamp01(difficulty + _eliteConfig.difficultyBoost * 0.1f);
            return difficulty;
        }

        private float GetEffectiveBPMModifier()
        {
            float bpmModifier = _currentEnemy.bpmModifier;
            if (_isElite && _eliteConfig != null)
                bpmModifier = _eliteConfig.ScaleBPMModifier(bpmModifier);
            return bpmModifier;
        }

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

            if (_useLegacyChart)
            {
                GameLog.Info($"[BattleManager] Battle initialized (legacy){eliteTag}: {_currentEnemy.enemyName} " +
                          $"({enemyHP} HP) at {_currentChart.BPM * _currentEnemy.bpmModifier} BPM");
            }
            else
            {
                GameLog.Info($"[BattleManager] Battle initialized ({_chartMode}){eliteTag}: {_currentEnemy.enemyName} " +
                          $"({enemyHP} HP) at {_battleChart.BPM} BPM, " +
                          $"{_battleChart.PlayerNoteCount}P + {_battleChart.EnemyNoteCount}E notes");
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
                        float bpmMod = GetEffectiveBPMModifier();
                        effectiveBPM = _currentChart.BPM * bpmMod;
                        offset = _currentChart.Offset;
                    }
                    else
                    {
                        effectiveBPM = _battleChart.BPM;
                        offset = _currentEnemy.songBeatMap != null
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